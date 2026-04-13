using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Muthur.Clients;
using Muthur.Console;
using Serilog;

var host = StartupExtensions.GetMotherHost(args);
var client = host.Services.GetRequiredService<MuthurApiClient>();
var handlerFactory = host.Services.GetRequiredService<IHttpMessageHandlerFactory>();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Muthur.Console");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    var samplePdf = args.Length > 0
        ? args[0]
        : StartupExtensions.FindSamplePdf();

    if (!File.Exists(samplePdf))
    {
        logger.LogError("Sample PDF not found: {Path}", samplePdf);
        return;
    }

    await using var demo = new DocumentProcessingDemo(client, logger, samplePdf);

    await demo.ConnectAsync(
        systemPrompt: """
            You are MU-TH-UR 6000, codename Mother, currently a document processing agent.
            You have two tools available:
            - pdf_extract_text: Extracts text from a PDF file given an absolute file path.
            - store_document: Stores extracted document text in the database for vector search.
            When asked to process a PDF, first extract its text, then store the document.
            """,
        handlerFactory,
        cts.Token);

    await demo.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    logger.LogWarning("Cancelled.");
}
catch (MuthurApiException ex)
{
    logger.LogError(ex, "API error: {Detail}", ex.Detail);
}
finally
{
    await Log.CloseAndFlushAsync();
}
