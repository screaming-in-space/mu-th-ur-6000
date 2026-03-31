using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;

namespace Muthur.ServiceDefaults;

public static class AiClientExtensions
{
    /// <summary>
    /// Registers an <see cref="IChatClient"/> using the M.E.AI pipeline.
    /// Reads "AI:Provider" and "AI:Model" from configuration. Defaults to OpenAI-compatible.
    /// </summary>
    public static IHostApplicationBuilder AddAgentChatClient(this IHostApplicationBuilder builder)
    {
        var provider = builder.Configuration["AI:Provider"] ?? "openai";
        var model = builder.Configuration["AI:Model"] ?? "gpt-4.1";
        var apiKey = builder.Configuration["AI:ApiKey"] ?? "";
        var endpoint = builder.Configuration["AI:Endpoint"];

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
    /// Uses OpenAI's text-embedding-3-small (1536 dimensions) by default.
    /// </summary>
    public static IHostApplicationBuilder AddAgentEmbeddingGenerator(this IHostApplicationBuilder builder)
    {
        var apiKey = builder.Configuration["AI:ApiKey"] ?? "";
        var endpoint = builder.Configuration["AI:Endpoint"];
        var embeddingModel = builder.Configuration["AI:EmbeddingModel"] ?? "text-embedding-3-small";

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
        // For Anthropic, use the OpenAI-compatible endpoint or the Anthropic SDK.
        // Using OpenAI-compatible mode for simplicity in this demo.
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://api.anthropic.com/v1/")
        };
        var client = new OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey), options);
        return client.GetChatClient(model).AsIChatClient();
    }
}
