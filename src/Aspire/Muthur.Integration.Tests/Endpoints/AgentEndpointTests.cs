using System.Net;
using System.Net.Http.Json;
using Muthur.Contracts;
using Muthur.Integration.Tests.Infrastructure;

namespace Muthur.Integration.Tests.Endpoints;

[Collection("Muthur")]
public sealed class AgentEndpointTests(MuthurFixture platform)
{
    [Fact]
    public async Task CreateSession_ReturnsAgentIdAndWorkflowId()
    {
        var response = await platform.ApiHttpClient.PostAsJsonAsync(
            "/v1/agent/sessions",
            new CreateSessionRequest("You are a test agent."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.NotNull(session);
        Assert.False(string.IsNullOrEmpty(session.AgentId));
        Assert.StartsWith("agent-", session.WorkflowId);
    }

    [Fact]
    public async Task GetAgentState_AfterCreate_ReturnsState()
    {
        // Create a session first.
        var createResponse = await platform.ApiHttpClient.PostAsJsonAsync(
            "/v1/agent/sessions",
            new CreateSessionRequest());

        var session = await createResponse.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.NotNull(session);

        // Query its state.
        var stateResponse = await platform.ApiHttpClient.GetAsync(
            $"/v1/agent/sessions/{session.AgentId}");

        Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);

        var state = await stateResponse.Content.ReadFromJsonAsync<AgentState>();
        Assert.NotNull(state);
        Assert.False(state.IsProcessing);
        Assert.Equal(0, state.TurnCount);
    }

    [Fact]
    public async Task GetAgentState_UnknownId_ReturnsNotFound()
    {
        var response = await platform.ApiHttpClient.GetAsync(
            "/v1/agent/sessions/nonexistent");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SendPrompt_UnknownId_ReturnsNotFound()
    {
        var response = await platform.ApiHttpClient.PostAsJsonAsync(
            "/v1/agent/sessions/nonexistent/prompt",
            new SendPromptRequest("Hello"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SendPrompt_ValidSession_ReturnsAccepted()
    {
        // Create a session.
        var createResponse = await platform.ApiHttpClient.PostAsJsonAsync(
            "/v1/agent/sessions",
            new CreateSessionRequest("You are a test agent."));

        var session = await createResponse.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.NotNull(session);

        // Send a prompt.
        var promptResponse = await platform.ApiHttpClient.PostAsJsonAsync(
            $"/v1/agent/sessions/{session.AgentId}/prompt",
            new SendPromptRequest("What is 2+2?"));

        Assert.Equal(HttpStatusCode.Accepted, promptResponse.StatusCode);
    }
}
