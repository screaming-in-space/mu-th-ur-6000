using System.Net;
using System.Net.Http.Json;
using Muthur.Contracts;

namespace Muthur.Integration.Tests.Infrastructure;

/// <summary>
/// Shared test helpers for agent workflow operations.
/// Wraps common HTTP calls with assertions to reduce boilerplate in test classes.
/// </summary>
public static class MuthurFixtureExtensions
{
    public static async Task<CreateSessionResponse> CreateSessionAsync(
        this MuthurFixture platform, string? systemPrompt = null)
    {
        var response = await platform.ApiHttpClient.PostAsJsonAsync(
            "/v1/agent/sessions",
            new CreateSessionRequest(systemPrompt));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.NotNull(session);
        return session;
    }

    public static async Task SendPromptAsync(
        this MuthurFixture platform, string agentId, string content)
    {
        var response = await platform.ApiHttpClient.PostAsJsonAsync(
            $"/v1/agent/sessions/{agentId}/prompt",
            new SendPromptRequest(content));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    public static async Task<AgentState> GetAgentStateAsync(
        this MuthurFixture platform, string agentId)
    {
        var response = await platform.ApiHttpClient.GetAsync(
            $"/v1/agent/sessions/{agentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var state = await response.Content.ReadFromJsonAsync<AgentState>();
        Assert.NotNull(state);
        return state;
    }

    /// <summary>Polls until TurnCount reaches the expected minimum, or times out.</summary>
    public static async Task<AgentState> PollUntilTurnCountAsync(
        this MuthurFixture platform, string agentId, int expectedMinTurns, int timeoutSeconds = 120)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        while (!cts.Token.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

            var state = await platform.GetAgentStateAsync(agentId);

            if (state.TurnCount >= expectedMinTurns && !state.IsProcessing)
            {
                return state;
            }
        }

        throw new TimeoutException(
            $"Workflow did not reach {expectedMinTurns} turns within {timeoutSeconds} seconds");
    }
}
