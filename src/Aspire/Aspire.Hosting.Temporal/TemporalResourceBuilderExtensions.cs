using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

public static class TemporalResourceBuilderExtensions
{
    /// <summary>
    /// Adds a Temporal dev server running in a container via <c>temporalio/admin-tools</c>.
    /// Uses <c>temporal server start-dev</c> with embedded SQLite storage.
    /// </summary>
    public static IResourceBuilder<TemporalResource> AddTemporalDevServer(
        this IDistributedApplicationBuilder builder,
        string name,
        int? grpcPort = null,
        int? uiPort = null)
    {
        return builder.AddResource(new TemporalResource(name))
            .WithImage("temporalio/admin-tools")
            .WithImageTag("latest")
            .WithEntrypoint("temporal")
            .WithArgs("server", "start-dev", "--ip", "0.0.0.0", "--db-filename", "/tmp/temporal.db")
            .WithEndpoint(port: grpcPort, targetPort: 7233, name: TemporalResource.GrpcEndpointName, scheme: "http")
            .WithEndpoint(port: uiPort, targetPort: 8233, name: TemporalResource.UiEndpointName, scheme: "http")
            .WithHttpHealthCheck(endpointName: TemporalResource.UiEndpointName)
            .WithLifetime(ContainerLifetime.Persistent);
    }
}
