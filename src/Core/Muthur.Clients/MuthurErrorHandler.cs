using System.Net;

namespace Muthur.Clients;

/// <summary>
/// Delegating handler that intercepts 401/403 responses and throws immediately.
/// All other non-success statuses pass through to the typed client for per-method handling.
/// </summary>
internal sealed class MuthurErrorHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            using (response)
            {
                throw await MuthurApiException.FromResponseAsync(response, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return response;
    }
}
