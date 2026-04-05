using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Muthur.Contracts;
using Muthur.Telemetry;
using Muthur.Tools;
using Temporalio.Activities;

namespace Muthur.Bishop.Worker.Activities;

/// <summary>
/// Calls the LLM via M.E.AI's IChatClient. Each call is a Temporal activity -
/// if it fails, Temporal retries it without re-executing prior activities.
/// </summary>
public class LlmActivities(ILogger<LlmActivities> logger, IChatClient chatClient, ToolRegistry toolRegistry)
{
    [Activity]
    public async Task<LlmActivityOutput> CallLlmAsync(LlmActivityInput input)
    {
        var cancellationToken = ActivityExecutionContext.Current.CancellationToken;
        logger.LogInformation("Calling LLM with {MessageCount} messages", input.Messages.Count);
        var stopwatch = Stopwatch.StartNew();

        // Build the message list for M.E.AI.
        var chatMessages = new List<ChatMessage>();

        if (input.SystemPrompt is not null)
        {
            chatMessages.Add(new ChatMessage(ChatRole.System, input.SystemPrompt));
        }

        foreach (var msg in input.Messages)
        {
            var role = msg.Role switch
            {
                AgentConstants.RoleUser => ChatRole.User,
                AgentConstants.RoleAssistant => ChatRole.Assistant,
                AgentConstants.RoleTool => ChatRole.Tool,
                _ => ChatRole.User
            };

            // Tool results: FunctionResultContent only, no duplicate text content.
            if (msg.ToolCallId is not null)
            {
                var toolMsg = new ChatMessage(role, [new FunctionResultContent(msg.ToolCallId, msg.Content)]);
                chatMessages.Add(toolMsg);
                continue;
            }

            // Assistant messages with tool calls: FunctionCallContent, text only if non-empty.
            if (msg.ToolCalls is { Length: > 0 })
            {
                var assistantMsg = new ChatMessage(role, (IList<AIContent>)[]);
                if (!string.IsNullOrEmpty(msg.Content))
                {
                    assistantMsg.Contents.Add(new TextContent(msg.Content));
                }

                foreach (var tc in msg.ToolCalls)
                {
                    assistantMsg.Contents.Add(new FunctionCallContent(tc.Id, tc.Name, tc.Arguments));
                }

                chatMessages.Add(assistantMsg);
                continue;
            }

            // User and other messages: plain text.
            chatMessages.Add(new ChatMessage(role, msg.Content));
        }

        // Configure tool availability.
        var options = new ChatOptions
        {
            Tools = [.. toolRegistry.GetTools()]
        };

        // Call the LLM through the M.E.AI pipeline.
        var response = await chatClient.GetResponseAsync(chatMessages, options, cancellationToken);

        stopwatch.Stop();
        MuthurMetrics.LlmDuration.Record(stopwatch.Elapsed.TotalSeconds);

        // Extract tool calls from the response.
        var toolCalls = response.Messages
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .Select(fc =>
            {
                if (string.IsNullOrEmpty(fc.CallId))
                {
                    throw new InvalidOperationException($"LLM returned tool call '{fc.Name}' without a CallId — cannot track tool result");
                }

                var args = fc.Arguments is Dictionary<string, object?> dict
                    ? dict
                    : new Dictionary<string, object?>(fc.Arguments ?? new Dictionary<string, object?>());

                return new ToolCallRequest(fc.CallId, fc.Name, args);
            })
            .ToArray();

        var textContent = string.Join("", response.Messages
            .SelectMany(m => m.Contents.OfType<TextContent>())
            .Select(t => t.Text));

        logger.LogInformation("LLM responded in {Duration:F1}s — {ToolCallCount} tool calls, {ContentLength} chars",
            stopwatch.Elapsed.TotalSeconds, toolCalls.Length, textContent.Length);

        return new LlmActivityOutput(textContent, toolCalls);
    }
}
