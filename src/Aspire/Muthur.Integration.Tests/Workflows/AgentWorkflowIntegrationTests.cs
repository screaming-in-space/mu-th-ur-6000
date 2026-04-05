using Muthur.Integration.Tests.Infrastructure;

namespace Muthur.Integration.Tests.Workflows;

/// <summary>
/// Integration tests for AgentWorkflow scenarios beyond the basic happy path.
/// Uses the full Aspire stack (Temporal, Postgres, Redis, LM Studio).
/// </summary>
[Collection("Muthur")]
public sealed class AgentWorkflowIntegrationTests(MuthurFixture platform)
{
    private const string SimpleSystemPrompt =
        "You are a test agent. Respond briefly to any prompt. Do not use tools.";

    [Fact]
    public async Task Workflow_QueryState_BeforePrompt_IsIdle()
    {
        var session = await platform.CreateSessionAsync(SimpleSystemPrompt);

        var state = await platform.GetAgentStateAsync(session.AgentId);

        Assert.False(state.IsProcessing);
        Assert.Equal(0, state.TurnCount);
        Assert.Null(state.LastResponse);
    }

    [Fact]
    public async Task Workflow_MultiplePrompts_ProcessesSequentially()
    {
        var session = await platform.CreateSessionAsync(SimpleSystemPrompt);

        await platform.SendPromptAsync(session.AgentId, "Say hello.");
        await platform.PollUntilTurnCountAsync(session.AgentId, expectedMinTurns: 1);

        await platform.SendPromptAsync(session.AgentId, "Say goodbye.");
        var finalState = await platform.PollUntilTurnCountAsync(session.AgentId, expectedMinTurns: 2);

        Assert.True(finalState.TurnCount >= 2, $"Expected at least 2 turns, got {finalState.TurnCount}");
        Assert.False(finalState.IsProcessing);
    }

    [Fact]
    public async Task Workflow_RapidPrompts_BothProcessed()
    {
        var session = await platform.CreateSessionAsync(SimpleSystemPrompt);

        await platform.SendPromptAsync(session.AgentId, "First message.");
        await platform.SendPromptAsync(session.AgentId, "Second message.");

        var finalState = await platform.PollUntilTurnCountAsync(session.AgentId, expectedMinTurns: 2);

        Assert.True(finalState.TurnCount >= 2, $"Expected at least 2 turns, got {finalState.TurnCount}");
        Assert.False(finalState.IsProcessing);
    }
}
