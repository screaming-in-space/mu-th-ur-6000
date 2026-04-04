using Microsoft.Extensions.Logging;
using Muthur.Tools;
using Temporalio.Activities;

namespace Muthur.Bishop.Worker.Activities;

/// <summary>
/// Dynamic tool dispatcher. Routes tool calls by name to the appropriate handler.
/// Each tool execution is a separate Temporal activity - individually checkpointed and retried.
/// </summary>
public class ToolActivities(ILogger<ToolActivities> logger, ToolRegistry toolRegistry)
{
    [Activity]
    public async Task<string> ExecuteToolAsync(string toolName, string arguments)
    {
        var cancellationToken = ActivityExecutionContext.Current.CancellationToken;
        logger.LogInformation("Executing tool: {ToolName}", toolName);

        var context = new ToolExecutionContext(
            ToolName: toolName,
            CancellationToken: cancellationToken);

        var result = await toolRegistry.ExecuteAsync(toolName, arguments, context);

        return result.Json;
    }
}
