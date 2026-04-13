using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muthur.Clients;
using Muthur.ServiceDefaults;

namespace Muthur.Console;

internal class StartupExtensions
{
    /// <summary>Name of the HttpClient registered for SignalR relay connections.</summary>
    public const string RelayHttpClientName = "muthur-relay";

    public static IHost GetMotherHost(params string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Environment.ContentRootPath = AppContext.BaseDirectory;

        builder.AddServiceDefaults();
        builder.Services.AddMuthurApiClient(new Uri("http://muthur-api"));

        // Named HttpClient for the SignalR relay connection.
        // Service discovery is applied via ConfigureHttpClientDefaults in AddServiceDefaults().
        builder.Services.AddHttpClient(RelayHttpClientName);

        return builder.Build();
    }

    /// <summary>Walks up from <see cref="AppContext.BaseDirectory"/> looking for the sample PDF.</summary>
    public static string FindSamplePdf()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "samples", "research", "A Memory OS for AI System.pdf");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "samples", "research", "A Memory OS for AI System.pdf");
    }
}
