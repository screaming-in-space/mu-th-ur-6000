using Muthur.Integration.Tests.Infrastructure;

namespace Muthur.Integration.Tests.Endpoints;

/// <summary>
/// End-to-end test: create session → send prompt → verify workflow processes it.
/// This tests the full Temporal pipeline: API → Temporal → Worker → Activity.
/// </summary>
[Collection("Muthur")]
public sealed class AgentWorkflowTests(MuthurFixture platform)
{
    [Fact]
    public async Task CreateSession_SendPrompt_WorkflowProcesses()
    {
        var session = await platform.CreateSessionAsync(
            "You are a test agent. Respond with 'hello' to any prompt.");

        await platform.SendPromptAsync(session.AgentId, "Say hello.");

        var finalState = await platform.PollUntilTurnCountAsync(session.AgentId, expectedMinTurns: 1);

        Assert.True(finalState.TurnCount > 0, "Workflow should have processed at least one turn");
        Assert.False(finalState.IsProcessing, "Workflow should not be processing");
        Assert.NotNull(finalState.LastResponse);
    }
}
