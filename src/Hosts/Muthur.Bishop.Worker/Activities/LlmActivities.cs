using Microsoft.Extensions.AI;
using Muthur.Contracts;
using Muthur.Tools;
using Temporalio.Activities;

namespace Muthur.Bishop.Worker.Activities;

/// <summary>
/// Calls the LLM via M.E.AI's IChatClient. Each call is a Temporal activity —
/// if it fails, Temporal retries it without re-executing prior activities.
/// </summary>
public class LlmActivities(IChatClient chatClient, ToolRegistry toolRegistry)
{
    [Activity]
    public async Task<LlmActivityOutput> CallLlmAsync(LlmActivityInput input)
    {
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

            chatMessages.Add(new ChatMessage(role, msg.Content));
        }

        // Configure tool availability.
        var options = new ChatOptions
        {
            Tools = [.. toolRegistry.GetTools()]
        };

        // Call the LLM through the M.E.AI pipeline.
        var response = await chatClient.GetResponseAsync(chatMessages, options);

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
