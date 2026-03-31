using System.Net;
using System.Net.Http.Json;
using Muthur.Contracts;
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
        // 1. Create a session.
        var createResponse = await platform.ApiHttpClient.PostAsJsonAsync(
            "/v1/agent/sessions",
            new CreateSessionRequest("You are a test agent. Respond with 'hello' to any prompt."));

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var session = await createResponse.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.NotNull(session);

        // 2. Send a simple prompt (no tool calls needed).
        var promptResponse = await platform.ApiHttpClient.PostAsJsonAsync(
            $"/v1/agent/sessions/{session.AgentId}/prompt",
            new SendPromptRequest("Say hello."));

        Assert.Equal(HttpStatusCode.Accepted, promptResponse.StatusCode);

        // 3. Poll for up to 120 seconds — the LLM call via LM Studio may be slow.
        AgentState? finalState = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        while (!cts.Token.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

            var stateResponse = await platform.ApiHttpClient.GetAsync(
                $"/v1/agent/sessions/{session.AgentId}", cts.Token);

            Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);

            var state = await stateResponse.Content.ReadFromJsonAsync<AgentState>(cancellationToken: cts.Token);
            Assert.NotNull(state);

            // TurnCount > 0 means the workflow processed the prompt.
            if (state.TurnCount > 0 && !state.IsProcessing)
            {
                finalState = state;
                break;
            }
        }

        // 4. Verify the workflow actually processed.
        Assert.NotNull(finalState);
        Assert.True(finalState.TurnCount > 0, "Workflow should have processed at least one turn");
        Assert.False(finalState.IsProcessing, "Workflow should not be processing");
        Assert.NotNull(finalState.LastResponse);
        Assert.NotEmpty(finalState.LastResponse);
    }
}
