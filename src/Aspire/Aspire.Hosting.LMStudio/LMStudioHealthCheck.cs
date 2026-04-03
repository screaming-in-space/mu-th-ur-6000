using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting.LMStudio;

/// <summary>
/// Health check for an externally-managed LM Studio instance.
/// Calls <c>GET /v1/models</c> (lightweight, returns loaded models).
/// </summary>
internal sealed class LMStudioHealthCheck(string endpoint) : IHealthCheck
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await HttpClient.GetAsync(
                $"{endpoint.TrimEnd('/')}/v1/models", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("LM Studio is responding")
                : HealthCheckResult.Unhealthy($"LM Studio returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("LM Studio is not reachable", ex);
        }
    }
}
