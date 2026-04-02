using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Muthur.Contracts;
using Temporalio.Activities;

namespace Muthur.Bishop.Worker.Activities;

/// <summary>
/// Publishes relay events to the API's SignalR hub via HTTP.
/// Best-effort: logs failures but does not throw, so workflows
/// complete even if the notification is undeliverable.
/// </summary>
public class NotificationActivities(ILogger<NotificationActivities> logger, IHttpClientFactory httpClientFactory)
{
    /// <summary>Named HttpClient for relay notification requests.</summary>
    public const string HttpClientName = "muthur-relay";

    [Activity]
    public async Task NotifyAsync(RelayEvent relay)
    {
        try
        {
            using var httpClient = httpClientFactory.CreateClient(HttpClientName);
            var response = await httpClient.PostAsJsonAsync(
                $"v1/relay/{relay.AgentId}/events", relay);

            response.EnsureSuccessStatusCode();
            logger.LogInformation("Relay: {EventType} for agent {AgentId}", relay.EventType, relay.AgentId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send relay event {EventType} for agent {AgentId}",
                relay.EventType, relay.AgentId);
        }
    }
}
