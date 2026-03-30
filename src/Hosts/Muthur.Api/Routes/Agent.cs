using Muthur.Contracts;
using Temporalio.Client;

namespace Muthur.Api.Routes;

public static class AgentRoutes
{
    public static void MapAgentRoutes(this WebApplication app)
    {
        var group = app.MapGroup("/v1/agent");

        // Start a new agent session.
        group.MapPost("/sessions", async (
            CreateSessionRequest request,
            ITemporalClient temporal) =>
        {
            var agentId = Guid.NewGuid().ToString("N")[..8];
            var workflowId = AgentConstants.WorkflowId(agentId);

            // Start the workflow using untyped handle - avoids referencing Worker project.
            await temporal.StartWorkflowAsync(
                "AgentWorkflow",
                [new AgentWorkflowInput(agentId, request.SystemPrompt)],
                new WorkflowOptions(workflowId, AgentConstants.TaskQueue));

            return Results.Ok(new { AgentId = agentId, WorkflowId = workflowId });
        });

        // Send a prompt to an existing agent.
        group.MapPost("/sessions/{agentId}/prompt", async (
            string agentId,
            SendPromptRequest request,
            ITemporalClient temporal) =>
        {
            var handle = temporal.GetWorkflowHandle(AgentConstants.WorkflowId(agentId));

            await handle.SignalAsync(
                "SendPromptAsync",
                [new PromptSignal(request.Content, request.SystemPrompt)]);

            return Results.Accepted();
        });

        // Query agent state.
        group.MapGet("/sessions/{agentId}", async (
            string agentId,
            ITemporalClient temporal) =>
        {
            var handle = temporal.GetWorkflowHandle(AgentConstants.WorkflowId(agentId));

            var state = await handle.QueryAsync<AgentState>("GetState", []);
            return Results.Ok(state);
        });
    }
}

public sealed record CreateSessionRequest(string? SystemPrompt = null);
public sealed record SendPromptRequest(string Content, string? SystemPrompt = null);
