using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Temporal;

/// <summary>
/// Represents a Temporal dev server container resource managed by Aspire.
/// Uses the <c>temporalio/admin-tools</c> image with <c>temporal server start-dev</c>.
/// </summary>
public class TemporalResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string GrpcEndpointName = "grpc";
    internal const string UiEndpointName = "ui";

    /// <summary>
    /// Gets the connection expression for the Temporal gRPC frontend (host:port).
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{this.GetEndpoint(GrpcEndpointName).Property(EndpointProperty.HostAndPort)}");
}
