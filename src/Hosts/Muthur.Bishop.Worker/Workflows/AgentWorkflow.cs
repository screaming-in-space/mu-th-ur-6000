using Microsoft.Extensions.Logging;
using Muthur.Bishop.Worker.Activities;
using Muthur.Contracts;
using Muthur.Telemetry;
using Muthur.Tools.Documents;
using System.Text.Json;
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

        // Extraction cache: the workflow is the data plane, the LLM is the control plane.
        // Full document text lives here, not in the LLM's context window.
        var extractions = new Dictionary<string, PdfExtractionResult>();

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
                var response = llmOutput.Content ?? "(no response)";
                Workflow.Logger.LogInformation("LLM produced final response ({Length} chars)", response.Length);

                await EmitRelayEventAsync(agentId, RelayEventType.AgentResponseReady,
                    "Agent response ready.",
                    new() { ["responseLength"] = response.Length.ToString() });

                return response;
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
                // If store_document, enrich arguments with cached extraction text
                // so the LLM never carries document content.
                var arguments = toolCall.Arguments;
                if (toolCall.Name == AgentConstants.ToolStoreDocument)
                {
                    arguments = EnrichStoreArguments(arguments, extractions);
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

                // Cache extraction results; send only a summary to the LLM.
                var llmResult = toolResult;
                if (toolCall.Name == AgentConstants.ToolPdfExtractText)
                {
                    llmResult = CacheExtraction(toolResult, toolCall.Arguments, extractions);
                }

                // Append tool result to conversation history.
                messages.Add(new ConversationMessage(
                    AgentConstants.RoleTool,
                    llmResult,
                    ToolCallId: toolCall.Id));

                // When a document is stored: notify the client, then fork ingestion.
                if (toolCall.Name == AgentConstants.ToolStoreDocument)
                {
                    await EmitRelayEventAsync(agentId, RelayEventType.DocumentStored,
                        "Document stored in Postgres.",
                        ParseDocumentId(toolResult));

                    // Fire-and-forget child workflow for chunking + embedding.
                    // Runs independently - doesn't block the conversation or die with ContinueAsNew.
                    await TryStartIngestionAsync(agentId, arguments, toolResult);
                }
            }

            // Loop: send tool results back to the LLM for the next turn.
        }
    }

    /// <summary>
    /// Caches the full extraction result in workflow state, returns a summary for the LLM.
    /// The LLM needs metadata to decide next steps — not 126K chars of document text.
    /// </summary>
    private static string CacheExtraction(
        string toolResult, Dictionary<string, object?> toolArguments, Dictionary<string, PdfExtractionResult> extractions)
    {
        try
        {
            var extraction = JsonSerializer.Deserialize<PdfExtractionResult>(toolResult, SerializerDefaults.CaseInsensitive);
            var filePath = toolArguments.GetValueOrDefault("filePath")?.ToString();
            if (extraction is null || filePath is null) return toolResult;

            extractions[filePath] = extraction;

            return JsonSerializer.Serialize(new
            {
                Status = "extracted",
                extraction.PageCount,
                extraction.Metadata,
                TextLength = extraction.Text.Length,
                SourcePath = filePath,
                Message = "Text extracted successfully. Call store_document to persist."
            });
        }
        catch (JsonException ex)
        {
            Workflow.Logger.LogWarning(ex, "Failed to deserialize extraction result — passing raw result to LLM");
            return toolResult;
        }
    }

    /// <summary>
    /// Enriches store_document arguments with the cached extraction text.
    /// The LLM sends metadata only; the workflow injects the full content.
    /// </summary>
    private static Dictionary<string, object?> EnrichStoreArguments(
        Dictionary<string, object?> arguments, Dictionary<string, PdfExtractionResult> extractions)
    {
        try
        {
            var sourcePath = arguments.GetValueOrDefault("sourcePath")?.ToString();
            if (sourcePath is null) return arguments;

            // If the LLM already provided text (shouldn't, but defensive), pass through.
            var existingText = arguments.GetValueOrDefault("text")?.ToString();
            if (!string.IsNullOrEmpty(existingText)) return arguments;

            if (!extractions.TryGetValue(sourcePath, out var extraction)) return arguments;

            // Build enriched arguments with cached text and metadata.
            var enriched = new Dictionary<string, object?>(arguments)
            {
                ["text"] = extraction.Text,
                ["metadata"] = extraction.Metadata
            };

            // Fill in defaults from extraction if not provided by LLM.
            if (!enriched.TryGetValue("pageCount", out object? value) || value is null or 0)
            {
                value = extraction.PageCount;
                enriched["pageCount"] = value;
            }

            var title = arguments.GetValueOrDefault("title")?.ToString();
            if (string.IsNullOrEmpty(title))
            {
                enriched["title"] = extraction.Metadata.GetValueOrDefault("title");
            }

            return enriched;
        }
        catch (Exception ex)
        {
            Workflow.Logger.LogWarning(ex, "Failed to enrich store_document arguments — passing raw arguments");
            return arguments;
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
    private static async Task TryStartIngestionAsync(
        string agentId, Dictionary<string, object?> toolArguments, string toolResult)
    {
        StoreDocumentResult? parsedResult = null;

        try
        {
            var sourcePath = toolArguments.GetValueOrDefault("sourcePath")?.ToString() ?? "";
            var text = toolArguments.GetValueOrDefault("text")?.ToString() ?? "";
            var pageCount = toolArguments.GetValueOrDefault("pageCount") switch
            {
                int i => i,
                long l => (int)l,
                JsonElement je when je.TryGetInt32(out var v) => v,
                _ => 0
            };

            // Parse metadata dictionary from arguments.
            var metadata = new Dictionary<string, string>();
            if (toolArguments.GetValueOrDefault("metadata") is Dictionary<string, string> m)
            {
                metadata = m;
            }
            else if (toolArguments.GetValueOrDefault("metadata") is JsonElement metaElement
                     && metaElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in metaElement.EnumerateObject())
                {
                    metadata[prop.Name] = prop.Value.GetString() ?? "";
                }
            }

            parsedResult = JsonSerializer.Deserialize<StoreDocumentResult>(toolResult, SerializerDefaults.CaseInsensitive);
            if (parsedResult?.DocumentId is null) return;

            var input = new DocumentIngestionInput(
                agentId,
                parsedResult.DocumentId.Value,
                sourcePath,
                text,
                pageCount,
                metadata);

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

    /// <summary>Emits a relay event via the notification activity. Best-effort — never blocks the workflow.</summary>
    private static async Task EmitRelayEventAsync(
        string agentId, RelayEventType eventType, string message, Dictionary<string, string>? metadata = null)
    {
        var relay = new RelayEvent(agentId, Guid.NewGuid(), eventType, message, DateTimeOffset.UtcNow, metadata);

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

    private static Dictionary<string, string>? ParseDocumentId(string toolResult)
    {
        try
        {
            var result = JsonSerializer.Deserialize<StoreDocumentResult>(toolResult, SerializerDefaults.CaseInsensitive);
            return result?.DocumentId is { } id
                ? new() { ["documentId"] = id.ToString() }
                : null;
        }
        catch (JsonException ex)
        {
            Workflow.Logger.LogWarning(ex, "Failed to parse document ID from tool result");
            return null;
        }
    }
}
