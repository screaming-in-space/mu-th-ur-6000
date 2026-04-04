using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Muthur.Telemetry;

namespace Muthur.Tools;

/// <summary>
/// Central registry for agent tools. Auto-collects all <see cref="IToolHandler"/>
/// implementations via DI. Provides dispatch with execution context and tracing.
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, Func<string, ToolExecutionContext, Task<ToolResult>>> _handlers = [];
    private readonly List<AITool> _tools = [];
    private readonly ILogger<ToolRegistry> _logger;

    public ToolRegistry(ILogger<ToolRegistry> logger, IEnumerable<IToolHandler> handlers)
    {
        _logger = logger;

        foreach (var handler in handlers)
        {
            _tools.Add(handler.Register(_handlers));
        }

        _logger.LogInformation("ToolRegistry initialized — {ToolCount} tools registered: {Tools}",
            _tools.Count, string.Join(", ", _handlers.Keys));
    }

    public IReadOnlyList<AITool> GetTools() => _tools;

    public Func<string, ToolExecutionContext, Task<ToolResult>>? GetHandler(string name) =>
        _handlers.GetValueOrDefault(name);

    /// <summary>
    /// Executes a tool by name with distributed tracing and metrics.
    /// Exceptions propagate to the caller — Temporal's retry policy handles failures.
    /// </summary>
    public async Task<ToolResult> ExecuteAsync(string toolName, string arguments, ToolExecutionContext context)
    {
        var handler = _handlers.GetValueOrDefault(toolName)
            ?? throw new InvalidOperationException($"Unknown tool: {toolName}");

        using var span = MuthurTrace.StartSpan($"tool.{toolName}")
            ?.WithTag("tool.name", toolName)
            ?.WithTag("tool.agent_id", context.AgentId)
            ?.WithTag("tool.call_id", context.CallId);

        var result = await handler(arguments, context).ConfigureAwait(false);

        MuthurMetrics.ToolExecutions.Add(1,
            new KeyValuePair<string, object?>("tool.name", toolName));

        span?.WithTag("tool.result_length", result.Json.Length)
            ?.SetSuccess();

        return result;
    }
}
