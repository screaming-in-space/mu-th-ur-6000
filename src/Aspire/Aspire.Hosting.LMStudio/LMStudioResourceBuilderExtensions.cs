using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

public static class LMStudioResourceBuilderExtensions
{
    /// <summary>
    /// Registers an externally-managed LM Studio instance as an Aspire resource.
    /// The connection string (<c>Endpoint + Model</c>) is injected into referencing projects
    /// via <c>WithReference</c>. A health check polls <c>GET /v1/models</c> so the
    /// Aspire dashboard reflects whether LM Studio is reachable and
    /// <c>WaitFor</c> can gate dependent resources.
    /// </summary>
    public static IResourceBuilder<LMStudioResource> AddLMStudio(
        this IDistributedApplicationBuilder builder,
        string name,
        string endpoint = "http://localhost:1234",
        string modelName = "default")
    {
        var healthCheckName = $"{name}-health";
        var resource = new LMStudioResource(name, endpoint, modelName);

        builder.Services.AddHealthChecks()
            .AddCheck(healthCheckName, new LMStudioHealthCheck(endpoint));

        // Publish "Running" state when the orchestrator initializes this resource
        // so Aspire begins polling the health check.
        builder.Eventing.Subscribe<InitializeResourceEvent>(
            resource,
            async (@event, cancellationToken) =>
            {
                await @event.Notifications.PublishUpdateAsync(
                    resource,
                    state => state with
                    {
                        State = new ResourceStateSnapshot(
                            KnownResourceStates.Running,
                            KnownResourceStateStyles.Success)
                    });
            });

        return builder.AddResource(resource)
            .ExcludeFromManifest()
            .WithHealthCheck(healthCheckName);
    }

    /// <summary>
    /// Overrides the model name on an existing LM Studio resource.
    /// </summary>
    public static IResourceBuilder<LMStudioResource> WithModel(
        this IResourceBuilder<LMStudioResource> builder,
        string modelName)
    {
        builder.WithAnnotation(new ModelNameAnnotation(modelName));
        return builder;
    }

    /// <summary>
    /// Sets the embedding model name on an LM Studio resource.
    /// Injected into the connection string as <c>EmbeddingModel=...</c>.
    /// </summary>
    public static IResourceBuilder<LMStudioResource> WithEmbeddingModel(
        this IResourceBuilder<LMStudioResource> builder,
        string embeddingModelName)
    {
        builder.WithAnnotation(new EmbeddingModelNameAnnotation(embeddingModelName));
        return builder;
    }
}
