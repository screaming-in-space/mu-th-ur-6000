using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muthur.Tools.Documents;
using Muthur.Tools.Handlers;

namespace Muthur.Tools;

public static class ToolsExtensions
{
    /// <summary>
    /// Registers tool domain services, handlers, and the <see cref="ToolRegistry"/> for agent tool dispatch.
    /// Handlers are registered as <see cref="IToolHandler"/> — ToolRegistry auto-collects them.
    /// </summary>
    public static IHostApplicationBuilder AddMuthurTools(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<DocumentStore>();

        builder.Services.AddSingleton<PdfHandler>();
        builder.Services.AddSingleton<IToolHandler>(sp => sp.GetRequiredService<PdfHandler>());

        builder.Services.AddSingleton<DocumentStoreHandler>();
        builder.Services.AddSingleton<IToolHandler>(sp => sp.GetRequiredService<DocumentStoreHandler>());

        builder.Services.AddSingleton<ToolRegistry>();

        return builder;
    }
}
