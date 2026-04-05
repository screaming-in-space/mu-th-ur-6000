using Microsoft.Extensions.Logging;
using Muthur.Bishop.Worker.Activities;
using Muthur.Contracts;
using Muthur.Telemetry;
using Temporalio.Workflows;

namespace Muthur.Bishop.Worker.Workflows;

/// <summary>
/// Durable agentic loop. Receives user prompts via signal, runs the LLM,
/// dispatches tool calls, and loops until the LLM produces a final response.
/// Pure data-transformation logic lives in <see cref="ToolResultProcessor"/>;
/// this class owns only Temporal orchestration and workflow state.
/// </summary>
[Workflow]
public class AgentWorkflow
{
    private readonly Queue<PromptSignal> _pending = new();
    private int _turnCount;
    private string? _lastResponse;
    private bool _isProcessing;

    [WorkflowRun]
    public async Task RunAsync(AgentWorkflowInput input)
    {
        while (true)
        {
            await Workflow.WaitConditionAsync(() => _pending.Count > 0);
            var signal = _pending.Dequeue();

            _isProcessing = true;
            Workflow.Logger.LogInformation("Processing prompt (turn {Turn}): {Prompt}",
                _turnCount, signal.Content[..Math.Min(signal.Content.Length, 100)]);

            _lastResponse = await ProcessPromptAsync(signal, input.AgentId, input.SystemPrompt);
            _isProcessing = false;
            _turnCount++;

            Workflow.Logger.LogInformation("Turn {Turn} complete — response: {Response}",
                _turnCount, _lastResponse?[..Math.Min(_lastResponse?.Length ?? 0, 100)]);

            if (_turnCount >= AgentConstants.MaxTurnsBeforeContinueAsNew)
            {
                var continueInput = new AgentWorkflowInput(input.AgentId, input.SystemPrompt);
                throw Workflow.CreateContinueAsNewException(
                    (AgentWorkflow wf) => wf.RunAsync(continueInput));
            }
        }
    }

    /// <summary>The agentic loop: LLM → tool calls → LLM → ... → final response.</summary>
    private async Task<string> ProcessPromptAsync(
        PromptSignal signal, string agentId, string? defaultSystemPrompt)
    {
        var systemPrompt = signal.SystemPrompt ?? defaultSystemPrompt
            ?? "You are MU-TH-UR 6000, a helpful AI assistant. You have tools available. Use them when appropriate.";

        var messages = new List<ConversationMessage>
        {
            new(AgentConstants.RoleUser, signal.Content)
        };

        // Extraction cache: the workflow is the data plane, the LLM is the control plane.
        // Full document text lives here, not in the LLM's context window.
        var extractions = new Dictionary<string, PdfExtractionResult>();

        while (true)
        {
            var llmOutput = await Workflow.ExecuteActivityAsync(
                (LlmActivities act) => act.CallLlmAsync(new LlmActivityInput(messages, systemPrompt)),
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(2) });

            if (llmOutput.ToolCalls.Length == 0)
            {
                var response = llmOutput.Content ?? "(no response)";
                Workflow.Logger.LogInformation("LLM produced final response ({Length} chars)", response.Length);

                await EmitRelayEventAsync(agentId, RelayEventType.AgentResponseReady,
                    "Agent response ready.",
                    new() { ["responseLength"] = response.Length.ToString() });

                return response;
            }

            Workflow.Logger.LogInformation("LLM requested {Count} tool call(s): {Tools}",
                llmOutput.ToolCalls.Length, string.Join(", ", llmOutput.ToolCalls.Select(t => t.Name)));

            messages.Add(new ConversationMessage(
                AgentConstants.RoleAssistant, llmOutput.Content ?? "", llmOutput.ToolCalls));

            foreach (var toolCall in llmOutput.ToolCalls)
            {
                var llmResult = await ExecuteToolCallAsync(toolCall, extractions, agentId);

                messages.Add(new ConversationMessage(
                    AgentConstants.RoleTool, llmResult, ToolCallId: toolCall.Id));
            }
        }
    }

    /// <summary>
    /// Executes a single tool call with enrichment, caching, and post-processing.
    /// Returns the LLM-facing result string for conversation history.
    /// </summary>
    private async Task<string> ExecuteToolCallAsync(
        ToolCallRequest toolCall,
        Dictionary<string, PdfExtractionResult> extractions,
        string agentId)
    {
        var arguments = toolCall.Arguments;
        if (toolCall.Name == AgentConstants.ToolStoreDocument)
        {
            arguments = ToolResultProcessor.EnrichStoreArguments(arguments, extractions);
        }

        var toolResult = await Workflow.ExecuteActivityAsync(
            (ToolActivities act) => act.ExecuteToolAsync(toolCall.Name, arguments),
            new ActivityOptions
            {
                StartToCloseTimeout = TimeSpan.FromMinutes(5),
                RetryPolicy = new Temporalio.Common.RetryPolicy
                {
                    MaximumAttempts = 3,
                    InitialInterval = TimeSpan.FromSeconds(2),
                    BackoffCoefficient = 2.0f
                }
            });

        var llmResult = toolResult;
        if (toolCall.Name == AgentConstants.ToolPdfExtractText)
        {
            var cached = ToolResultProcessor.CacheExtraction(toolResult, toolCall.Arguments);
            if (cached.IsCached)
            {
                extractions[cached.FilePath!] = cached.Extraction!;
            }

            llmResult = cached.LlmSummary;
        }

        // When a document is stored: notify the client, then fork ingestion.
        if (toolCall.Name == AgentConstants.ToolStoreDocument)
        {
            var ingestionInput = ToolResultProcessor.ParseIngestionInput(
                agentId, arguments, toolResult);

            await EmitRelayEventAsync(agentId, RelayEventType.DocumentStored,
                "Document stored in Postgres.",
                ingestionInput?.DocumentId is { } id
                    ? new() { ["documentId"] = id.ToString() }
                    : null);

            await TryStartIngestionAsync(ingestionInput);
        }

        return llmResult;
    }

    /// <summary>
    /// Starts DocumentIngestionWorkflow as a fire-and-forget child workflow.
    /// ParentClosePolicy.Abandon ensures ingestion survives ContinueAsNew.
    /// </summary>
    private static async Task TryStartIngestionAsync(DocumentIngestionInput? input)
    {
        try
        {
            if (input is null) return;

            await Workflow.StartChildWorkflowAsync(
                (DocumentIngestionWorkflow wf) => wf.RunAsync(input),
                new ChildWorkflowOptions
                {
                    Id = $"ingest-{input.DocumentId}",
                    ParentClosePolicy = Temporalio.Workflows.ParentClosePolicy.Abandon
                });

            MuthurMetrics.DocumentsIngested.Add(1);
        }
        catch (Exception ex)
        {
            Workflow.Logger.LogWarning(ex, "Failed to start ingestion");
        }
    }

    [WorkflowSignal]
    public async Task SendPromptAsync(PromptSignal signal) => _pending.Enqueue(signal);

    [WorkflowQuery]
    public AgentState GetState() => new(_isProcessing, _turnCount, _lastResponse);

    /// <summary>Emits a relay event via the notification activity. Best-effort — never blocks the workflow.</summary>
    private static async Task EmitRelayEventAsync(
        string agentId, RelayEventType eventType, string message, Dictionary<string, string>? metadata = null)
    {
        var guidBytes = new byte[16];
        Workflow.Random.NextBytes(guidBytes);
        var relay = new RelayEvent(
            agentId, new Guid(guidBytes), eventType, message, Workflow.UtcNow, metadata);

        await Workflow.ExecuteActivityAsync(
            (NotificationActivities act) => act.NotifyAsync(relay),
            new ActivityOptions
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(30),
                RetryPolicy = new Temporalio.Common.RetryPolicy
                {
                    MaximumAttempts = 2,
                    InitialInterval = TimeSpan.FromSeconds(1)
                }
            });
    }
}
