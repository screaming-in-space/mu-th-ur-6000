using Microsoft.Extensions.DependencyInjection;

namespace Muthur.Utilities;

public static class UtilityExtensions
{
    /// <summary>
    /// Registers <see cref="AgentRunner"/> for DI injection.
    /// Requires <see cref="Muthur.Clients.MuthurApiClient"/> to be registered.
    /// </summary>
    public static IServiceCollection AddAgentRunner(this IServiceCollection services)
    {
        services.AddTransient<AgentRunner>();
        return services;
    }
}
