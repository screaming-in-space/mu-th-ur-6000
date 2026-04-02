using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Muthur.Clients;
using Muthur.Console;
using Muthur.Contracts;
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
        : FindSamplePdf();

    static string FindSamplePdf()
    {
        // Walk up from the output directory until we find the samples/ folder at the repo root.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "samples", "research", "A Memory OS for AI System.pdf");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "samples", "research", "A Memory OS for AI System.pdf");
    }

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

    // Wait for ingestion to complete via SignalR relay, then search.
    await WaitForIngestionAndSearchAsync(result.AgentId, host, client, logger, cts.Token);

    // List documents to confirm storage.
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

/// <summary>
/// Connects to the relay hub, waits for the ingestion-completed event,
/// then runs a vector search to prove the full pipeline works.
/// </summary>
static async Task WaitForIngestionAndSearchAsync(
    string agentId, IHost host, MuthurApiClient client, Microsoft.Extensions.Logging.ILogger logger, CancellationToken cancellationToken)
{
    var ingestionDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    // Use a handler from IHttpMessageHandlerFactory so Aspire service discovery
    // resolves "http://muthur-api" to the actual endpoint.
    var handlerFactory = host.Services.GetRequiredService<IHttpMessageHandlerFactory>();

    var connection = new HubConnectionBuilder()
        .WithUrl($"http://muthur-api/v1/relay?agentId={agentId}", options =>
        {
            options.HttpMessageHandlerFactory = _ =>
                handlerFactory.CreateHandler(StartupExtensions.RelayHttpClientName);
        })
        .WithAutomaticReconnect()
        .Build();

    connection.On<RelayEvent>("RelayEvent", relay =>
    {
        logger.LogInformation("Relay event: {EventType} — {Message}", relay.EventType, relay.Message);

        if (relay.EventType == RelayEventType.IngestionCompleted)
            ingestionDone.TrySetResult();
    });

    try
    {
        await connection.StartAsync(cancellationToken);
        logger.LogInformation("Connected to relay hub — waiting for ingestion to complete...");

        // Wait for ingestion with a timeout. If it takes longer than 3 minutes, move on.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(3));

        try
        {
            await ingestionDone.Task.WaitAsync(timeoutCts.Token);
            logger.LogInformation("Ingestion complete — running vector search.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Ingestion notification timed out — searching anyway.");
        }

        // Search to demonstrate the full pipeline: extract → store → vectorize → search.
        var results = await client.SearchDocumentsAsync("key findings", limit: 3, cancellationToken);
        logger.LogInformation("Search results: {Count} chunks", results.Count);

        foreach (var chunk in results)
        {
            logger.LogInformation("  [{Score:F3}] {Title}: {Text}",
                chunk.Score, chunk.DocumentTitle ?? "untitled",
                chunk.ChunkText[..Math.Min(chunk.ChunkText.Length, 120)]);
        }
    }
    finally
    {
        await connection.DisposeAsync();
    }
}
