using System.Diagnostics;

namespace Muthur.Telemetry;

/// <summary>
/// Shared <see cref="ActivitySource"/> for the Muthur agent.
/// Use <see cref="StartSpan"/> to create spans that integrate with OpenTelemetry.
/// <para>
/// <c>using var span = MuthurTrace.StartSpan("my-operation");</c>
/// </para>
/// </summary>
public static class MuthurTrace
{
    public static readonly ActivitySource Source = new("Muthur");

    /// <summary>
    /// Starts a new span. Returns <c>null</c> when no trace listener is attached (zero overhead).
    /// The returned <see cref="Activity"/> is <see cref="IDisposable"/>; wrap with <c>using</c>
    /// to automatically end the span on scope exit.
    /// </summary>
    public static Activity? StartSpan(
        string name,
        ActivityKind kind = ActivityKind.Internal,
        IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        return tags is null
            ? Source.StartActivity(name, kind)
            : Source.StartActivity(name, kind, default(ActivityContext), tags);
    }
}
