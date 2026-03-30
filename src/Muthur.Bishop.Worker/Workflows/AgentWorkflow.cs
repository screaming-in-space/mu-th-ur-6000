using Muthur.Bishop.Worker.Activities;
using Muthur.Contracts;
using Temporalio.Workflows;

namespace Muthur.Bishop.Worker.Workflows;

/// <summary>
/// Durable agentic loop. Receives user prompts via signal, runs the LLM,
/// dispatches tool calls, and loops until the LLM produces a final response.
/// Stateless — conversation history lives in the signal, not in workflow state.
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
            _lastResponse = await ProcessPromptAsync(signal, input.SystemPrompt);
            _isProcessing = false;
            _turnCount++;

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
    private async Task<string> ProcessPromptAsync(PromptSignal signal, string? defaultSystemPrompt)
    {
        var systemPrompt = signal.SystemPrompt ?? defaultSystemPrompt
            ?? "You are MU-TH-UR 6000, a helpful AI assistant. You have tools available. Use them when appropriate.";

        var messages = new List<ConversationMessage>
        {
            new(AgentConstants.RoleUser, signal.Content)
        };

        // The agentic loop — keep calling the LLM until it stops requesting tools.
        while (true)
        {
            var llmInput = new LlmActivityInput(messages, systemPrompt);

            var llmOutput = await Workflow.ExecuteActivityAsync(
                (LlmActivities act) => act.CallLlmAsync(llmInput),
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(2) });

            // No tool calls — the LLM produced a final response.
            if (llmOutput.ToolCalls.Length == 0)
            {
                return llmOutput.Content ?? "(no response)";
            }

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
}
