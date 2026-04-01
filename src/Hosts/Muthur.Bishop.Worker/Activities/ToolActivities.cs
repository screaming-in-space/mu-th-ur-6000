using Microsoft.Extensions.Logging;
using Muthur.Telemetry;
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

        var handler = toolRegistry.GetHandler(toolName)
            ?? throw new InvalidOperationException($"Unknown tool: {toolName}");

        MuthurMetrics.ToolExecutions.Add(1,
            new KeyValuePair<string, object?>("tool.name", toolName));

        var result = await handler(arguments, cancellationToken);

        logger.LogInformation("Tool {ToolName} completed — {ResultLength} chars", toolName, result.Length);
        return result;
    }
}
