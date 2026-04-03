using Microsoft.Extensions.DependencyInjection;

namespace Muthur.Clients;

public static class MuthurClientExtensions
{
    /// <summary>
    /// Registers <see cref="MuthurApiClient"/> with custom HttpClient configuration.
    /// Includes the <see cref="MuthurErrorHandler"/> for auth-error interception.
    /// </summary>
    public static IServiceCollection AddMuthurApiClient(
        this IServiceCollection services,
        Action<HttpClient> configureClient)
    {
        services.AddTransient<MuthurErrorHandler>();
        services.AddHttpClient<MuthurApiClient>(configureClient)
            .AddHttpMessageHandler<MuthurErrorHandler>();

        return services;
    }

    /// <summary>
    /// Registers <see cref="MuthurApiClient"/> with opinionated defaults:
    /// connection pooling, error handler pipeline, and the given base address.
    /// </summary>
    public static IServiceCollection AddMuthurApiClient(
        this IServiceCollection services,
        Uri baseAddress)
    {
        services.AddTransient<MuthurErrorHandler>();
        services.AddHttpClient<MuthurApiClient>(client => client.BaseAddress = baseAddress)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
                ConnectTimeout = TimeSpan.FromSeconds(10),
            })
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
            .AddHttpMessageHandler<MuthurErrorHandler>();

        return services;
    }
}
