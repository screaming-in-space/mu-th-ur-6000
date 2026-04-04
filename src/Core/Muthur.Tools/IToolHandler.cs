using Microsoft.Extensions.AI;

namespace Muthur.Tools;

/// <summary>
/// Contract for tool handlers. Each handler registers its dispatch function
/// and returns the LLM tool definition. Implement this interface and register as a
/// singleton — <see cref="ToolRegistry"/> auto-collects all implementations.
/// </summary>
public interface IToolHandler
{
    /// <summary>
    /// Registers this tool's dispatch handler and returns the LLM-facing <see cref="AIFunction"/>.
    /// </summary>
    AIFunction Register(Dictionary<string, Func<string, ToolExecutionContext, Task<ToolResult>>> handlers);
}
