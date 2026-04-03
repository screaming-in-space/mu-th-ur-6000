using System.Text.Json;

namespace Muthur.Contracts;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> instances.
/// Reuse these instead of creating new instances per call.
/// </summary>
public static class SerializerDefaults
{
    /// <summary>Case-insensitive property matching — the standard for tool handler JSON roundtrips.</summary>
    public static readonly JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
