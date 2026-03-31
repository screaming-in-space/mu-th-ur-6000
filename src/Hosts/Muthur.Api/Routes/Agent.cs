using Muthur.Contracts;
using Temporalio.Client;

namespace Muthur.Api.Routes;

public static class AgentRoutes
{
    public static void MapAgentRoutes(this WebApplication app)
    {
        var group = app.MapGroup("/v1/agent")
            .WithTags("Agent");

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

            return Results.Ok(new CreateSessionResponse(agentId, workflowId));
        })
        .WithName("CreateSession")
        .WithDescription("Start a new durable agent session backed by a Temporal workflow.")
        .Produces<CreateSessionResponse>()
        .ProducesValidationProblem();

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
        })
        .WithName("SendPrompt")
        .WithDescription("Send a prompt signal to a running agent session.")
        .Produces(StatusCodes.Status202Accepted)
        .ProducesValidationProblem();

        // Query agent state.
        group.MapGet("/sessions/{agentId}", async (
            string agentId,
            ITemporalClient temporal) =>
        {
            var handle = temporal.GetWorkflowHandle(AgentConstants.WorkflowId(agentId));

            var state = await handle.QueryAsync<AgentState>("GetState", []);
            return Results.Ok(state);
        })
        .WithName("GetAgentState")
        .WithDescription("Query the current state of an agent session.")
        .Produces<AgentState>();
    }
}
