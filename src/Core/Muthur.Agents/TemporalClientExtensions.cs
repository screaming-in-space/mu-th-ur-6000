using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Temporalio.Extensions.Hosting;

namespace Muthur.Agents;

public static class TemporalClientExtensions
{
    /// <summary>
    /// Registers a Temporal client using configuration from connection strings or config keys.
    /// Reads <c>muthur-temporal-dev</c> connection string first, then falls back to <c>Temporal:*</c> config.
    /// Defaults to <c>localhost:7233</c> / <c>default</c> namespace for local development.
    /// </summary>
    public static IHostApplicationBuilder AddMuthurTemporalClient(this IHostApplicationBuilder builder)
    {
        var host = builder.Configuration.GetConnectionString("muthur-temporal-dev")
            ?? builder.Configuration["Temporal:Address"]
            ?? "localhost:7233";
        var ns = builder.Configuration["Temporal:Namespace"] ?? "default";

        builder.Services.AddTemporalClient(host, ns);
        return builder;
    }
}
