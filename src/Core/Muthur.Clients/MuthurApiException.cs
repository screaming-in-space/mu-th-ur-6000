using System.Net;
using System.Net.Http.Json;

namespace Muthur.Clients;

/// <summary>
/// Exception thrown when the Muthur API returns a non-success status code.
/// Extends <see cref="HttpRequestException"/> so callers can catch either type.
/// </summary>
public class MuthurApiException(string message, HttpStatusCode statusCode, string? detail = null)
    : HttpRequestException(message, inner: null, statusCode)
{
    /// <summary>Parsed error body, if available.</summary>
    public string? Detail { get; } = detail;

    public static async Task<MuthurApiException> FromResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        var detail = await ReadDetailAsync(response, cancellationToken).ConfigureAwait(false);
        var method = response.RequestMessage?.Method;
        var path = response.RequestMessage?.RequestUri?.PathAndQuery;
        var status = (int)response.StatusCode;

        var message = $"{method} {path} -> {status}: {detail ?? response.ReasonPhrase ?? "Unknown error"}";
        return new MuthurApiException(message, response.StatusCode, detail);
    }

    private static async Task<string?> ReadDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            if (response.Content.Headers.ContentLength is 0) { return null; }

            var mediaType = response.Content.Headers.ContentType?.MediaType;

            if (mediaType is "application/problem+json")
            {
                var problem = await response.Content
                    .ReadFromJsonAsync<ProblemDetailsResponse>(cancellationToken)
                    .ConfigureAwait(false);

                return problem?.Detail ?? problem?.Title;
            }

            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return raw.Length > 500 ? string.Concat(raw.AsSpan(0, 500), "...") : raw;
        }
        catch
        {
            return null;
        }
    }
}

internal sealed record ProblemDetailsResponse(
    string? Type,
    string? Title,
    string? Detail,
    int? Status);
