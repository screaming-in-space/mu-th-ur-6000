using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Muthur.Contracts;
using Temporalio.Activities;

namespace Muthur.Bishop.Worker.Activities;

/// <summary>
/// Publishes relay events to the API's SignalR hub via HTTP.
/// Best-effort: logs failures but does not throw, so ingestion
/// completes even if the notification is undeliverable.
/// </summary>
public class NotificationActivities(ILogger<NotificationActivities> logger, IHttpClientFactory httpClientFactory)
{
    /// <summary>Named HttpClient for relay notification requests.</summary>
    public const string HttpClientName = "muthur-relay";

    [Activity]
    public async Task NotifyIngestionCompleteAsync(string agentId, Guid documentId)
    {
        var relay = new RelayEvent(
            agentId,
            documentId,
            RelayEventType.IngestionCompleted,
            $"Document {documentId} ingestion complete.",
            DateTimeOffset.UtcNow,
            new Dictionary<string, string> { ["documentId"] = documentId.ToString() });

        try
        {
            using var httpClient = httpClientFactory.CreateClient(HttpClientName);
            var response = await httpClient.PostAsJsonAsync(
                $"v1/relay/{agentId}/events", relay);

            response.EnsureSuccessStatusCode();
            logger.LogInformation("Notified ingestion complete for document {DocumentId} on agent {AgentId}",
                documentId, agentId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify ingestion complete for document {DocumentId}", documentId);
        }
    }
}
