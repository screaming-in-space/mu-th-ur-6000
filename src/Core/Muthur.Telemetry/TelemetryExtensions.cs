using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;

namespace Muthur.Telemetry;

public static class TelemetryExtensions
{
    /// <summary>
    /// Microsoft.Extensions.AI telemetry source name.
    /// Used by UseOpenTelemetry() middleware on IChatClient pipeline.
    /// </summary>
    private const string MeaiTelemetrySourceName = "Experimental.Microsoft.Extensions.AI";

    public static IHostApplicationBuilder AddMuthurTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => ConfigureServiceResource(resource, builder.Environment))
            .WithTracing(tracing => tracing
                .AddSource(MuthurTrace.Source.Name)
                .AddSource(MeaiTelemetrySourceName))
            .WithMetrics(metrics => metrics
                .AddMeter(MuthurMetrics.Meter.Name)
                .AddMeter(MeaiTelemetrySourceName));

        return builder;
    }

    private static void ConfigureServiceResource(ResourceBuilder resource, IHostEnvironment environment)
    {
        var assembly = Assembly.GetEntryAssembly();
        var informationalVersion = assembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        var version = informationalVersion ?? assembly?.GetName().Version?.ToString() ?? "unknown";
        var commitSha = informationalVersion?.Split('+') is [_, string sha] ? sha : null;

        resource.AddService(
            serviceName: environment.ApplicationName,
            serviceVersion: version,
            serviceInstanceId: Environment.MachineName);

        if (commitSha is not null)
        {
            resource.AddAttributes([new("vcs.repository.ref.revision", commitSha)]);
        }

        resource.AddAttributes(
        [
            new("deployment.environment.name", environment.EnvironmentName),
            new("service.namespace", "muthur"),
        ]);
    }
}
