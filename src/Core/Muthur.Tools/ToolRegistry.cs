using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Muthur.Telemetry;
using System.Text.Json;

namespace Muthur.Tools;

/// <summary>
/// Central registry for agent tools. Auto-collects all <see cref="IToolHandler"/>
/// implementations via DI. Dispatches tool calls through <see cref="AIFunction.InvokeAsync"/>
/// — the same function that provides the LLM schema is the one that executes.
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, AIFunction> _tools = [];
    private readonly ILogger<ToolRegistry> _logger;

    public ToolRegistry(ILogger<ToolRegistry> logger, IEnumerable<IToolHandler> handlers)
    {
        _logger = logger;

        foreach (var handler in handlers)
        {
            var registration = handler.Register();
            _tools[registration.Name] = registration.Function;
        }

        _logger.LogInformation("ToolRegistry initialized — {ToolCount} tools registered: {Tools}",
            _tools.Count, string.Join(", ", _tools.Keys));
    }

    public IReadOnlyList<AITool> GetTools() => [.. _tools.Values];

    public AIFunction? GetFunction(string name) => _tools.GetValueOrDefault(name);

    /// <summary>
    /// Executes a tool by name via <see cref="AIFunction.InvokeAsync"/>.
    /// The AIFunction handles parameter binding from the dictionary.
    /// Exceptions propagate to the caller — Temporal's retry policy handles failures.
    /// </summary>
    public async Task<ToolResult> ExecuteAsync(
        string toolName, IDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        var tool = _tools.GetValueOrDefault(toolName)
            ?? throw new InvalidOperationException($"Unknown tool: {toolName}");

        using var span = MuthurTrace.StartSpan($"tool.{toolName}")
            ?.WithTag("tool.name", toolName);

        var aiArgs = CreateArguments(arguments);
        var result = await tool.InvokeAsync(aiArgs, cancellationToken).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(result);

        MuthurMetrics.ToolExecutions.Add(
            1,
            new KeyValuePair<string, object?>("tool.name", toolName));

        span?.WithTag("tool.result_length", json.Length)
            ?.SetSuccess();

        return new ToolResult(json, result);
    }

    public static AIFunctionArguments CreateArguments(IDictionary<string, object?> arguments)
    {
        var aiArgs = new AIFunctionArguments();
        foreach (var (key, value) in arguments)
        {
            aiArgs[key] = value;
        }
        return aiArgs;
    }
}
