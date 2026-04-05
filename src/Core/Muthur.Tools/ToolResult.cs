using System.Text.Json;

namespace Muthur.Tools;

/// <summary>
/// Typed result from a tool execution. Carries both the serialized JSON string
/// (for Temporal activity boundaries) and the typed payload (for in-process consumers
/// that need to inspect the result without redundant deserialization).
/// </summary>
public sealed class ToolResult
{
    /// <summary>Serialized JSON string — used across Temporal activity boundaries.</summary>
    public string Json { get; }

    /// <summary>
    /// Typed payload — available for in-process consumers to avoid deserializing <see cref="Json"/>.
    /// </summary>
    public object? TypedPayload { get; }

    internal ToolResult(string json, object? typedPayload)
    {
        Json = json;
        TypedPayload = typedPayload;
    }

    /// <summary>Creates a <see cref="ToolResult"/> from a typed value, serializing it to JSON.</summary>
    public static ToolResult From<T>(T value) =>
        new(JsonSerializer.Serialize(value), value);
}
