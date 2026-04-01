using Muthur.Contracts;
using Muthur.Telemetry;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Muthur.Api.Routes;

public static class AgentRoutes
{
    public static void MapAgentRoutes(this WebApplication app)
    {
        var group = app.MapGroup("/v1/agent")
            .WithTags("Agent");

        group.MapPost("/sessions", CreateSessionAsync)
            .WithName("CreateSession")
            .WithDescription("Start a new durable agent session backed by a Temporal workflow.")
            .Produces<CreateSessionResponse>()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesValidationProblem();

        group.MapPost("/sessions/{agentId}/prompt", SendPromptAsync)
            .WithName("SendPrompt")
            .WithDescription("Send a prompt signal to a running agent session.")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesValidationProblem();

        group.MapGet("/sessions/{agentId}", GetAgentStateAsync)
            .WithName("GetAgentState")
            .WithDescription("Query the current state of an agent session.")
            .Produces<AgentState>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);
    }

    private static async Task<IResult> CreateSessionAsync(
        CreateSessionRequest request,
        ITemporalClient temporal,
        CancellationToken cancellationToken)
    {
        var agentId = Guid.NewGuid().ToString("N")[..8];
        var workflowId = AgentConstants.WorkflowId(agentId);

        using var span = MuthurTrace.StartSpan("agent.create-session")
            ?.WithTag("agent.id", agentId);

        try
        {
            await temporal.StartWorkflowAsync(
                "AgentWorkflow",
                [new AgentWorkflowInput(agentId, request.SystemPrompt)],
                new WorkflowOptions(workflowId, AgentConstants.TaskQueue)
                {
                    Rpc = new RpcOptions { CancellationToken = cancellationToken }
                });

            MuthurMetrics.AgentSessions.Add(1);
            span?.SetSuccess();

            return Results.Ok(new CreateSessionResponse(agentId, workflowId));
        }
        catch (WorkflowAlreadyStartedException)
        {
            span?.RecordError(new InvalidOperationException($"Session {agentId} already exists"));
            return Results.Conflict(new { Error = $"Session {agentId} already exists" });
        }
        catch (RpcException ex)
        {
            span?.RecordError(ex);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway,
                title: "Temporal unavailable");
        }
    }

    private static async Task<IResult> SendPromptAsync(
        string agentId,
        SendPromptRequest request,
        ITemporalClient temporal,
        CancellationToken cancellationToken)
    {
        var handle = temporal.GetWorkflowHandle(AgentConstants.WorkflowId(agentId));

        try
        {
            await handle.SignalAsync(
                "SendPrompt",
                [new PromptSignal(request.Content, request.SystemPrompt)],
                new WorkflowSignalOptions
                {
                    Rpc = new RpcOptions { CancellationToken = cancellationToken }
                });

            return Results.Accepted();
        }
        catch (RpcException ex) when (ex.Code == RpcException.StatusCode.NotFound)
        {
            return Results.NotFound(new { Error = $"Agent session '{agentId}' not found" });
        }
        catch (RpcException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway,
                title: "Temporal unavailable");
        }
    }

    private static async Task<IResult> GetAgentStateAsync(
        string agentId,
        ITemporalClient temporal,
        CancellationToken cancellationToken)
    {
        var handle = temporal.GetWorkflowHandle(AgentConstants.WorkflowId(agentId));

        try
        {
            var state = await handle.QueryAsync<AgentState>(
                "GetState", [],
                new WorkflowQueryOptions
                {
                    Rpc = new RpcOptions { CancellationToken = cancellationToken }
                });
            return Results.Ok(state);
        }
        catch (RpcException ex) when (ex.Code == RpcException.StatusCode.NotFound)
        {
            return Results.NotFound(new { Error = $"Agent session '{agentId}' not found" });
        }
        catch (RpcException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway,
                title: "Temporal unavailable");
        }
    }
}
