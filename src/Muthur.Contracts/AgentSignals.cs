namespace Muthur.Contracts;

/// <summary>Signal sent to the agent workflow to process a user prompt.</summary>
public sealed record PromptSignal(
    string Content,
    string? SystemPrompt = null);

/// <summary>Query response for the current agent state.</summary>
public sealed record AgentState(
    bool IsProcessing,
    int TurnCount,
    string? LastResponse);
