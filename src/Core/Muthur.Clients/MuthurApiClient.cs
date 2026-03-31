using System.Net.Http.Json;
using Muthur.Contracts;

namespace Muthur.Clients;

/// <summary>
/// Typed HTTP client for the Muthur API.
/// Managed by <see cref="IHttpClientFactory"/> — never create or dispose the <see cref="HttpClient"/> yourself.
/// </summary>
public sealed class MuthurApiClient(HttpClient httpClient)
{
    /// <summary>Creates a new agent session. Returns the session identity.</summary>
    public async Task<CreateSessionResponse> CreateSessionAsync(
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var body = new CreateSessionRequest(systemPrompt);
        var response = await httpClient.PostAsJsonAsync("v1/agent/sessions", body, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await DeserializeAsync<CreateSessionResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends a prompt signal to a running agent session.</summary>
    public async Task SendPromptAsync(
        string agentId,
        string content,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var body = new SendPromptRequest(content, systemPrompt);
        var response = await httpClient.PostAsJsonAsync($"v1/agent/sessions/{agentId}/prompt", body, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Queries the current state of an agent session.</summary>
    public async Task<AgentState> GetAgentStateAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"v1/agent/sessions/{agentId}", cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await DeserializeAsync<AgentState>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lists all stored documents.</summary>
    public async Task<List<DocumentSummary>> ListDocumentsAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync("v1/documents", cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await DeserializeAsync<List<DocumentSummary>>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Performs a vector similarity search across document chunks.</summary>
    public async Task<List<SimilarChunk>> SearchDocumentsAsync(
        string query,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"v1/documents/search?q={Uri.EscapeDataString(query)}&limit={limit}",
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await DeserializeAsync<List<SimilarChunk>>(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            using (response)
            {
                throw await MuthurApiException.FromResponseAsync(response, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static async Task<T> DeserializeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false)
            ?? throw new MuthurApiException(
                $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery} -> {(int)response.StatusCode}: Response body was null or empty",
                response.StatusCode);
    }
}
