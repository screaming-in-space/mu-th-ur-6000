using Microsoft.Extensions.Logging;
using Muthur.Tools;
using Temporalio.Activities;

namespace Muthur.Bishop.Worker.Activities;

/// <summary>
/// Dynamic tool dispatcher. Routes tool calls by name to the appropriate handler
/// via <see cref="ToolRegistry"/>. Each tool execution is a separate Temporal activity —
/// individually checkpointed and retried.
/// </summary>
public class ToolActivities(ILogger<ToolActivities> logger, ToolRegistry toolRegistry)
{
    [Activity]
    public async Task<string> ExecuteToolAsync(string toolName, Dictionary<string, object?> arguments)
    {
        var cancellationToken = ActivityExecutionContext.Current.CancellationToken;
        logger.LogInformation("Executing tool: {ToolName}", toolName);

        var result = await toolRegistry.ExecuteAsync(toolName, arguments, cancellationToken);

        return result.Json;
    }
}
