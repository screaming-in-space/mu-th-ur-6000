namespace Muthur.Tools;

/// <summary>
/// Structured metadata carried through tool dispatch.
/// Provides correlation context for logging, metrics, and tracing
/// without polluting the handler's business logic signature.
/// </summary>
public sealed record ToolExecutionContext(
    string ToolName,
    string? AgentId = null,
    string? WorkflowId = null,
    string? CallId = null,
    CancellationToken CancellationToken = default);
