using Microsoft.Extensions.AI;

namespace Muthur.Tools;

/// <summary>
/// Contract for tool handlers. Each handler returns its registration —
/// the tool name and the <see cref="AIFunction"/> that serves as both
/// the LLM schema definition and the runtime execution path.
/// </summary>
public interface IToolHandler
{
    /// <summary>
    /// Returns this tool's name and <see cref="AIFunction"/>.
    /// The AIFunction is both the LLM-facing schema and the invocation target.
    /// </summary>
    ToolRegistration Register();
}

/// <summary>
/// Pairs a tool name with its <see cref="AIFunction"/>.
/// The function is the single code path — schema for the LLM and invocation target for dispatch.
/// </summary>
public sealed record ToolRegistration(string Name, AIFunction Function);
