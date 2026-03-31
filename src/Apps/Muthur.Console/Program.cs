using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Muthur.Clients;
using Muthur.Console;
using Muthur.Utilities;
using Serilog;

var host = StartupExtensions.GetMotherHost(args);
var runner = host.Services.GetRequiredService<AgentRunner>();
var client = host.Services.GetRequiredService<MuthurApiClient>();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Muthur.Console");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    var samplePdf = args.Length > 0
        ? args[0]
        : Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "research", "A Memory OS for AI System.pdf"));

    if (!File.Exists(samplePdf))
    {
        logger.LogError("Sample PDF not found: {Path}", samplePdf);
        return;
    }

    var result = await runner.RunAsync(new AgentJobRequest
    {
        Prompt = $"Extract the text from this PDF and store it as a document: {samplePdf}",
        SystemPrompt = """
                You are MU-TH-UR 6000, codename Mother, currently a document processing agent.
                You have two tools available:
                - extract_pdf_text: Extracts text from a PDF file given an absolute file path.
                - store_document: Stores extracted document text in the database for vector search.
                When asked to process a PDF, first extract its text, then store the document.
                """,
    },
        cts.Token);

    if (result.FinalState is { } state)
    {
        logger.LogInformation("Agent response:\n{Response}", state.LastResponse);
    }
    else
    {
        logger.LogWarning("Agent did not complete within the timeout.");
    }

    // List documents to confirm ingestion.
    var docs = await client.ListDocumentsAsync(cts.Token);
    logger.LogInformation("Documents in store: {Count}", docs.Count);

    foreach (var doc in docs)
    {
        logger.LogInformation("  [{Id}] {Title} ({Pages} pages)",
            doc.Id, doc.Title ?? doc.SourcePath, doc.PageCount);
    }
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
