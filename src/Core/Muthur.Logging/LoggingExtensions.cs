using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.SystemConsole.Themes;

namespace Muthur.Logging;

public static class LoggingExtensions
{
    private const string OutputTemplate =
        "[{Timestamp:HH:mm:ss.ffffff} {Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}";

    public static IHostApplicationBuilder AddStructuredLogging(
        this IHostApplicationBuilder builder,
        LogEventLevel minimumLevel = LogEventLevel.Information)
    {
        var useJson = string.Equals(
            builder.Configuration["Logging:Format"], "json",
            StringComparison.OrdinalIgnoreCase);

        builder.Services.AddSerilog((services, config) =>
        {
            config
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .MinimumLevel.Is(minimumLevel)
                .Enrich.FromLogContext();

            if (useJson)
            {
                config.WriteTo.Console(new CompactJsonFormatter());
            }
            else
            {
                config.WriteTo.Console(outputTemplate: OutputTemplate, theme: AnsiConsoleTheme.Code);
            }

            var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                config.WriteTo.OpenTelemetry();
            }
        });

        return builder;
    }
}
