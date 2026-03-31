using System.Diagnostics.Metrics;

namespace Muthur.Telemetry;

/// <summary>
/// Shared <see cref="Meter"/> for the Muthur agent.
/// Instruments are created once and reused for the process lifetime.
/// </summary>
public static class MuthurMetrics
{
    public static readonly Meter Meter = new("Muthur");

    /// <summary>Counts agent sessions started.</summary>
    public static readonly Counter<long> AgentSessions =
        Meter.CreateCounter<long>(
            "muthur.agent.sessions",
            unit: "{session}",
            description: "Total agent sessions started.");

    /// <summary>Counts tool executions dispatched by the agent workflow.</summary>
    public static readonly Counter<long> ToolExecutions =
        Meter.CreateCounter<long>(
            "muthur.tool.executions",
            unit: "{execution}",
            description: "Total tool executions dispatched.");

    /// <summary>Counts documents stored and queued for vectorization.</summary>
    public static readonly Counter<long> DocumentsIngested =
        Meter.CreateCounter<long>(
            "muthur.documents.ingested",
            unit: "{document}",
            description: "Total documents ingested.");

    /// <summary>Records LLM call duration in seconds.</summary>
    public static readonly Histogram<double> LlmDuration =
        Meter.CreateHistogram<double>(
            "muthur.llm.duration",
            unit: "s",
            description: "LLM call duration.");
}
