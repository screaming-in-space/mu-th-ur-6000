using Microsoft.Extensions.Hosting;
using Muthur.Clients;
using Muthur.ServiceDefaults;
using Muthur.Utilities;

namespace Muthur.Console;

internal class StartupExtensions
{
    public static IHost GetMotherHost(params string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Environment.ContentRootPath = AppContext.BaseDirectory;

        builder.AddServiceDefaults();
        builder.Services.AddMuthurApiClient(new Uri("http://muthur-api"));
        builder.Services.AddAgentRunner();

        return builder.Build();
    }
}
