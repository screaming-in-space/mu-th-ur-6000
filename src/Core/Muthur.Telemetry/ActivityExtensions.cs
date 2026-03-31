using System.Diagnostics;

namespace Muthur.Telemetry;

/// <summary>
/// Fluent extensions on <see cref="Activity"/>? for ergonomic span instrumentation.
/// All methods are null-safe — they no-op when the span is <c>null</c> (no listener attached).
/// </summary>
public static class ActivityExtensions
{
    public static Activity? WithTag(this Activity? activity, string key, object? value)
    {
        activity?.SetTag(key, value);
        return activity;
    }

    /// <summary>
    /// Adds a baggage item that propagates across service boundaries via the W3C <c>baggage</c> header.
    /// Use for correlation values (agentId, documentId) that downstream services need.
    /// For span-local metadata, use <see cref="WithTag"/> instead.
    /// </summary>
    public static Activity? WithBaggage(this Activity? activity, string key, string? value)
    {
        activity?.SetBaggage(key, value);
        return activity;
    }

    public static Activity? RecordError(this Activity? activity, Exception exception)
    {
        if (activity is null) { return null; }

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddException(exception);

        return activity;
    }

    public static Activity? SetSuccess(this Activity? activity)
    {
        activity?.SetStatus(ActivityStatusCode.Ok);
        return activity;
    }
}
