using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muthur.Tools.Handlers;

namespace Muthur.Tools;

public static class ToolsExtensions
{
    /// <summary>
    /// Registers tool handlers and the <see cref="ToolRegistry"/> for agent tool dispatch.
    /// </summary>
    public static IHostApplicationBuilder AddMuthurTools(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<DocumentStoreHandler>();
        builder.Services.AddSingleton<ToolRegistry>();
        return builder;
    }
}
