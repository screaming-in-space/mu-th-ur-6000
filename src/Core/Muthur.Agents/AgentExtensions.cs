using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Muthur.Agents;

public static class AgentExtensions
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
