using Microsoft.AspNetCore.SignalR;

namespace Muthur.Api.Hubs;

/// <summary>
/// SignalR hub for relay events. Clients join groups by agentId
/// and receive broadcast events when ingestion completes.
/// </summary>
public sealed class MuthurHub(ILogger<MuthurHub> logger) : Hub
{
    public const string EventName = "RelayEvent";

    public override async Task OnConnectedAsync()
    {
        var agentId = Context.GetHttpContext()?.Request.Query["agentId"].ToString();

        if (!string.IsNullOrEmpty(agentId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, agentId);
            logger.LogDebug("Connection {ConnectionId} auto-joined agent {AgentId}",
                Context.ConnectionId, agentId);
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinAgent(string agentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, agentId);
        logger.LogDebug("Connection {ConnectionId} joined agent {AgentId}",
            Context.ConnectionId, agentId);
    }

    public async Task LeaveAgent(string agentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, agentId);
        logger.LogDebug("Connection {ConnectionId} left agent {AgentId}",
            Context.ConnectionId, agentId);
    }
}
