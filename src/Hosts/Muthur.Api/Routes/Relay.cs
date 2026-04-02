using Microsoft.AspNetCore.SignalR;
using Muthur.Api.Hubs;
using Muthur.Contracts;

namespace Muthur.Api.Routes;

public static class Relay
{
    public static WebApplication MapRelayRoutes(this WebApplication app)
    {
        app.MapHub<MuthurHub>("/v1/relay");

        var group = app.MapGroup("/v1/relay")
            .WithTags("Relay");

        group.MapPost("/{agentId}/events", PublishAsync)
            .WithName("PublishRelayEvent");

        return app;
    }

    private static async Task<IResult> PublishAsync(
        string agentId,
        RelayEvent relay,
        IHubContext<MuthurHub> hubContext,
        CancellationToken cancellationToken)
    {
        await hubContext.Clients.Group(agentId)
            .SendAsync(MuthurHub.EventName, relay, cancellationToken);

        return Results.Accepted();
    }
}
