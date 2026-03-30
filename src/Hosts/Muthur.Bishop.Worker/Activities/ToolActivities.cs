using Muthur.Tools;
using Temporalio.Activities;

namespace Muthur.Bishop.Worker.Activities;

/// <summary>
/// Dynamic tool dispatcher. Routes tool calls by name to the appropriate handler.
/// Each tool execution is a separate Temporal activity — individually checkpointed and retried.
/// </summary>
public class ToolActivities(ToolRegistry toolRegistry)
{
    [Activity]
    public async Task<string> ExecuteToolAsync(string toolName, string arguments)
    {
        var handler = toolRegistry.GetHandler(toolName)
            ?? throw new InvalidOperationException($"Unknown tool: {toolName}");

        return await handler(arguments);
    }
}
