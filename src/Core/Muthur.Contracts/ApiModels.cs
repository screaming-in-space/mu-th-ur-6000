namespace Muthur.Contracts;

/// <summary>Request body for creating a new agent session.</summary>
public sealed record CreateSessionRequest(string? SystemPrompt = null);

/// <summary>Request body for sending a prompt to an agent.</summary>
public sealed record SendPromptRequest(string Content, string? SystemPrompt = null);

/// <summary>Response from creating a new agent session.</summary>
public sealed record CreateSessionResponse(string AgentId, string WorkflowId);

/// <summary>Response containing full document text content.</summary>
public sealed record DocumentContentResponse(string Content);
