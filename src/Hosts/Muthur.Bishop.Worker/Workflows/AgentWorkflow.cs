using System.Text.Json;
using Microsoft.Extensions.Logging;
using Muthur.Bishop.Worker.Activities;
using Muthur.Contracts;
using Muthur.Telemetry;
using Temporalio.Workflows;

namespace Muthur.Bishop.Worker.Workflows;

/// <summary>
/// Durable agentic loop. Receives user prompts via signal, runs the LLM,
/// dispatches tool calls, and loops until the LLM produces a final response.
/// Each turn builds conversation history locally from the prompt in the signal.
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
            // Wait for a prompt signal.
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

            // Prevent unbounded event history growth.
            if (_turnCount >= AgentConstants.MaxTurnsBeforeContinueAsNew)
            {
                var continueInput = new AgentWorkflowInput(input.AgentId, input.SystemPrompt);
                throw Workflow.CreateContinueAsNewException(
                    (AgentWorkflow wf) => wf.RunAsync(continueInput));
            }
        }
    }

    /// <summary>The agentic loop: LLM → tool calls → LLM → ... → final response.</summary>
    private async Task<string> ProcessPromptAsync(PromptSignal signal, string agentId, string? defaultSystemPrompt)
    {
        var systemPrompt = signal.SystemPrompt ?? defaultSystemPrompt
            ?? "You are MU-TH-UR 6000, a helpful AI assistant. You have tools available. Use them when appropriate.";

        var messages = new List<ConversationMessage>
        {
            new(AgentConstants.RoleUser, signal.Content)
        };

        // The agentic loop - keep calling the LLM until it stops requesting tools.
        while (true)
        {
            var llmInput = new LlmActivityInput(messages, systemPrompt);

            var llmOutput = await Workflow.ExecuteActivityAsync(
                (LlmActivities act) => act.CallLlmAsync(llmInput),
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(2) });

            // No tool calls - the LLM produced a final response.
            if (llmOutput.ToolCalls.Length == 0)
            {
                Workflow.Logger.LogInformation("LLM produced final response ({Length} chars)", llmOutput.Content?.Length ?? 0);
                return llmOutput.Content ?? "(no response)";
            }

            Workflow.Logger.LogInformation("LLM requested {Count} tool call(s): {Tools}",
                llmOutput.ToolCalls.Length, string.Join(", ", llmOutput.ToolCalls.Select(t => t.Name)));

            // Append the assistant's tool-call message to history.
            messages.Add(new ConversationMessage(
                AgentConstants.RoleAssistant,
                llmOutput.Content ?? "",
                llmOutput.ToolCalls));

            // Execute each tool call as a separate Temporal activity.
            foreach (var toolCall in llmOutput.ToolCalls)
            {
                var toolResult = await Workflow.ExecuteActivityAsync(
                    (ToolActivities act) => act.ExecuteToolAsync(toolCall.Name, toolCall.Arguments),
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

                // Append tool result to conversation history.
                messages.Add(new ConversationMessage(
                    AgentConstants.RoleTool,
                    toolResult,
                    ToolCallId: toolCall.Id));

                // Kick off document ingestion as a child workflow when a document is stored.
                // Runs independently - doesn't block the conversation or die with ContinueAsNew.
                if (toolCall.Name == "store_document")
                {
                    await TryStartIngestionAsync(agentId, toolCall.Arguments, toolResult);
                }
            }

            // Loop: send tool results back to the LLM for the next turn.
        }
    }

    [WorkflowSignal]
    public async Task SendPromptAsync(PromptSignal signal)
    {
        _pending.Enqueue(signal);
    }

    [WorkflowQuery]
    public AgentState GetState() => new(_isProcessing, _turnCount, _lastResponse);

    /// <summary>
    /// Starts DocumentIngestionWorkflow as a fire-and-forget child workflow.
    /// ParentClosePolicy.Abandon ensures ingestion survives ContinueAsNew.
    /// </summary>
    private static async Task TryStartIngestionAsync(string agentId, string toolArguments, string toolResult)
    {
        StoreDocumentResult? parsedResult = null;

        try
        {
            var args = JsonSerializer.Deserialize<StoreDocumentArgs>(toolArguments);
            parsedResult = JsonSerializer.Deserialize<StoreDocumentResult>(toolResult);
            if (args is null || parsedResult?.DocumentId is null) return;

            var input = new DocumentIngestionInput(
                agentId,
                parsedResult.DocumentId.Value,
                args.SourcePath ?? "",
                args.Text ?? "",
                args.PageCount,
                args.Metadata ?? []);

            // Fire-and-forget: StartChildWorkflowAsync returns after the child is scheduled,
            // not after it completes. The agent conversation continues immediately.
            // ParentClosePolicy.Abandon ensures ingestion survives ContinueAsNew.
            await Workflow.StartChildWorkflowAsync(
                (DocumentIngestionWorkflow wf) => wf.RunAsync(input),
                new ChildWorkflowOptions
                {
                    Id = $"ingest-{parsedResult.DocumentId}",
                    ParentClosePolicy = Temporalio.Workflows.ParentClosePolicy.Abandon
                });

            MuthurMetrics.DocumentsIngested.Add(1);
        }
        catch (Exception ex)
        {
            // Ingestion failure doesn't break the agent conversation.
            Workflow.Logger.LogWarning(ex, "Failed to start ingestion for document {DocumentId}", parsedResult?.DocumentId);
        }
    }

    private sealed record StoreDocumentArgs(
        string? Title, string? SourcePath, string? Text,
        int PageCount = 0, Dictionary<string, string>? Metadata = null);

    private sealed record StoreDocumentResult(Guid? DocumentId);
}
