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
        var (lmEndpoint, lmModel) = ParseLMStudioConnectionString(builder);

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
                .Build();
        });

        return builder;
    }

    /// <summary>
    /// Registers an <see cref="IEmbeddingGenerator{String, Embedding}"/> for vector embedding generation.
    /// Uses nomic-embed-text-v1.5 (768 dimensions) by default for local LM Studio.
    /// </summary>
    public static IHostApplicationBuilder AddAgentEmbeddingGenerator(this IHostApplicationBuilder builder)
    {
        var (lmEndpoint, _) = ParseLMStudioConnectionString(builder);

        var apiKey = builder.Configuration["AI:ApiKey"] ?? "lm-studio";
        var endpoint = lmEndpoint ?? builder.Configuration["AI:Endpoint"];
        var embeddingModel = builder.Configuration["AI:EmbeddingModel"] ?? "nomic-embed-text-v1.5";

        builder.Services.AddEmbeddingGenerator(services =>
        {
            var options = new OpenAIClientOptions();

            if (endpoint is not null)
            { options.Endpoint = new Uri(endpoint); }

            var client = new OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey), options);
            return client.GetEmbeddingClient(embeddingModel).AsIEmbeddingGenerator();
        });

        return builder;
    }

    /// <summary>
    /// Parses the LM Studio connection string injected by Aspire: <c>Endpoint=...;Model=...</c>
    /// </summary>
    private static (string? Endpoint, string? Model) ParseLMStudioConnectionString(
        IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("muthur-lmstudio");
        if (connectionString is null) return (null, null);

        string? endpoint = null, model = null;

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;

            var key = kv[0].Trim();
            var value = kv[1].Trim();

            if (key.Equals("Endpoint", StringComparison.OrdinalIgnoreCase)) endpoint = value;
            else if (key.Equals("Model", StringComparison.OrdinalIgnoreCase)) model = value;
        }

        return (endpoint, model);
    }

    private static IChatClient CreateOpenAiClient(string model, string apiKey, string? endpoint)
    {
        var options = new OpenAIClientOptions();
        if (endpoint is not null)
            options.Endpoint = new Uri(endpoint);

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
}
