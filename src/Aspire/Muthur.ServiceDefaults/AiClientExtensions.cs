using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;

namespace Muthur.ServiceDefaults;

public static class AiClientExtensions
{
    /// <summary>
    /// Registers an <see cref="IChatClient"/> using the M.E.AI pipeline.
    /// Reads the LM Studio connection string first, then falls back to AI:* config keys.
    /// </summary>
    public static IHostApplicationBuilder AddAgentChatClient(this IHostApplicationBuilder builder)
    {
        var (lmEndpoint, lmModel, _) = ParseLMStudioConnectionString(builder);

        var provider = builder.Configuration["AI:Provider"] ?? "openai";
        var model = lmModel ?? builder.Configuration["AI:Model"] ?? "gpt-4.1";
        var apiKey = builder.Configuration["AI:ApiKey"] ?? "lm-studio";
        var endpoint = lmEndpoint ?? builder.Configuration["AI:Endpoint"];

        builder.Services.AddChatClient(services =>
        {
            IChatClient inner = provider.ToLowerInvariant() switch
            {
                "anthropic" => CreateAnthropicClient(model, apiKey),
                _ => CreateOpenAiClient(model, apiKey, endpoint)
            };

            return new ChatClientBuilder(inner)
                .UseOpenTelemetry(configure: t => t.EnableSensitiveData = true)
                .UseLogging()
                .Build(services);
        });

        return builder;
    }

    /// <summary>
    /// Registers an <see cref="IEmbeddingGenerator{String, Embedding}"/> for vector embedding generation.
    /// Uses nomic-embed-text-v1.5 (768 dimensions) by default for local LM Studio.
    /// </summary>
    public static IHostApplicationBuilder AddAgentEmbeddingGenerator(this IHostApplicationBuilder builder)
    {
        var (lmEndpoint, _, lmEmbeddingModel) = ParseLMStudioConnectionString(builder);

        var apiKey = builder.Configuration["AI:ApiKey"] ?? "lm-studio";
        var endpoint = lmEndpoint ?? builder.Configuration["AI:Endpoint"];
        var embeddingModel = lmEmbeddingModel
            ?? builder.Configuration["AI:EmbeddingModel"]
            ?? "nomic-embed-text-v1.5";

        builder.Services.AddEmbeddingGenerator(services =>
        {
            var options = new OpenAIClientOptions();

            if (endpoint is not null)
            { options.Endpoint = NormalizeEndpoint(endpoint); }

            var client = new OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey), options);
            return client.GetEmbeddingClient(embeddingModel).AsIEmbeddingGenerator();
        });

        return builder;
    }

    /// <summary>
    /// Parses the LM Studio connection string injected by Aspire:
    /// <c>Endpoint=...;Model=...;EmbeddingModel=...</c>
    /// </summary>
    private static (string? Endpoint, string? Model, string? EmbeddingModel) ParseLMStudioConnectionString(
        IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("muthur-lmstudio");
        if (connectionString is null) return (null, null, null);

        string? endpoint = null, model = null, embeddingModel = null;

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;

            var key = kv[0].Trim();
            var value = kv[1].Trim();

            if (key.Equals("Endpoint", StringComparison.OrdinalIgnoreCase)) endpoint = value;
            else if (key.Equals("Model", StringComparison.OrdinalIgnoreCase)) model = value;
            else if (key.Equals("EmbeddingModel", StringComparison.OrdinalIgnoreCase)) embeddingModel = value;
        }

        return (endpoint, model, embeddingModel);
    }

    private static IChatClient CreateOpenAiClient(string model, string apiKey, string? endpoint)
    {
        var options = new OpenAIClientOptions();
        if (endpoint is not null)
            options.Endpoint = NormalizeEndpoint(endpoint);

        var client = new OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey), options);
        return client.GetChatClient(model).AsIChatClient();
    }

    private static IChatClient CreateAnthropicClient(string model, string apiKey)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://api.anthropic.com/v1/")
        };
        var client = new OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey), options);
        return client.GetChatClient(model).AsIChatClient();
    }

    /// <summary>
    /// Ensures the endpoint includes the <c>/v1</c> path that the OpenAI .NET SDK v2 expects.
    /// The SDK appends <c>/chat/completions</c> directly to the endpoint — without <c>/v1</c>,
    /// LM Studio receives <c>POST /chat/completions</c> and rejects it.
    /// </summary>
    private static Uri NormalizeEndpoint(string endpoint)
    {
        var trimmed = endpoint.TrimEnd('/');
        if (!trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            trimmed += "/v1";

        return new Uri(trimmed);
    }
}
