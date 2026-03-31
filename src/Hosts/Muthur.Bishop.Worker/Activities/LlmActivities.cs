using System.Diagnostics;
using Microsoft.Extensions.AI;
using Muthur.Contracts;
using Muthur.Telemetry;
using Muthur.Tools;
using Temporalio.Activities;

namespace Muthur.Bishop.Worker.Activities;

/// <summary>
/// Calls the LLM via M.E.AI's IChatClient. Each call is a Temporal activity -
/// if it fails, Temporal retries it without re-executing prior activities.
/// </summary>
public class LlmActivities(IChatClient chatClient, ToolRegistry toolRegistry)
{
    [Activity]
    public async Task<LlmActivityOutput> CallLlmAsync(LlmActivityInput input)
    {
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

            var chatMessage = new ChatMessage(role, msg.Content);

            // Reconstruct tool call content for assistant messages.
            if (msg.ToolCalls is { Length: > 0 })
            {
                foreach (var tc in msg.ToolCalls)
                {
                    var args = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(tc.Arguments);
                    chatMessage.Contents.Add(new FunctionCallContent(tc.Id, tc.Name, args));
                }
            }

            // Reconstruct tool result content for tool messages.
            if (msg.ToolCallId is not null)
            {
                chatMessage.Contents.Add(new FunctionResultContent(msg.ToolCallId, msg.Content));
            }

            chatMessages.Add(chatMessage);
        }

        // Configure tool availability.
        var options = new ChatOptions
        {
            Tools = [.. toolRegistry.GetTools()]
        };

        // Call the LLM through the M.E.AI pipeline.
        var response = await chatClient.GetResponseAsync(chatMessages, options);

        stopwatch.Stop();
        MuthurMetrics.LlmDuration.Record(stopwatch.Elapsed.TotalSeconds);

        // Extract tool calls from the response.
        var toolCalls = response.Messages
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .Select(fc => new ToolCallRequest(
                fc.CallId ?? Guid.NewGuid().ToString(),
                fc.Name,
                System.Text.Json.JsonSerializer.Serialize(fc.Arguments)))
            .ToArray();

        var textContent = string.Join("", response.Messages
            .SelectMany(m => m.Contents.OfType<TextContent>())
            .Select(t => t.Text));

        return new LlmActivityOutput(textContent, toolCalls);
    }
}
