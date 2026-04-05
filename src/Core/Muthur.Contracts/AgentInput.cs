namespace Muthur.Contracts;

/// <summary>Input to start the agent workflow.</summary>
public sealed record AgentWorkflowInput(
    string AgentId,
    string? SystemPrompt = null);

/// <summary>Input passed to the LLM activity for a single turn.</summary>
public sealed record LlmActivityInput(
    List<ConversationMessage> Messages,
    string? SystemPrompt,
    string? Model = null);

/// <summary>Result from the LLM activity.</summary>
public sealed record LlmActivityOutput(
    string? Content,
    ToolCallRequest[] ToolCalls);

/// <summary>A tool call requested by the LLM.</summary>
public sealed record ToolCallRequest(
    string Id,
    string Name,
    Dictionary<string, object?> Arguments);

/// <summary>A message in the conversation history.</summary>
public sealed record ConversationMessage(
    string Role,
    string Content,
    ToolCallRequest[]? ToolCalls = null,
    string? ToolCallId = null);
