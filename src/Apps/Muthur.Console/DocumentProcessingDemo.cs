using Microsoft.Extensions.Logging;
using Muthur.Clients;
using Muthur.Contracts;

namespace Muthur.Console;

/// <summary>
/// Demo: send a file to the agent for extraction and storage,
/// wait for embeddings, then run a vector search to prove the pipeline works.
/// </summary>
public sealed class DocumentProcessingDemo(
    MuthurApiClient client,
    ILogger logger,
    string filePath) : RelayDemoBase(client, logger)
{
    protected override async Task SendWorkAsync(CancellationToken cancellationToken)
    {
        await Client.SendPromptAsync(AgentId,
            $"Extract the text from this PDF and store it as a document: {filePath}",
            cancellationToken: cancellationToken);
    }

    protected override async Task<bool> OnEventAsync(RelayEvent evt, CancellationToken cancellationToken)
    {
        switch (evt.EventType)
        {
            case RelayEventType.DocumentStored:
                Logger.LogInformation("Document persisted to Postgres — embeddings in progress.");
                break;

            case RelayEventType.AgentResponseReady:
                var state = await Client.GetAgentStateAsync(AgentId, cancellationToken);
                Logger.LogInformation("Agent response:\n{Response}", state.LastResponse);
                break;

            case RelayEventType.IngestionCompleted:
                Logger.LogInformation("Embeddings complete — running vector search.");
                await RunSearchAsync(cancellationToken);
                await ListDocumentsAsync(cancellationToken);
                return true; // Done.

            case RelayEventType.IngestionFailed:
                Logger.LogError("Ingestion failed: {Message}", evt.Message);
                await ListDocumentsAsync(cancellationToken);
                return true; // Done (with failure).
        }

        return false;
    }

    private async Task RunSearchAsync(CancellationToken cancellationToken)
    {
        var results = await Client.SearchDocumentsAsync("key findings", limit: 3, cancellationToken);
        Logger.LogInformation("Search results: {Count} chunks", results.Count);

        foreach (var chunk in results)
        {
            Logger.LogInformation("  [{Score:F3}] {Title}: {Text}",
                chunk.Score, chunk.DocumentTitle ?? "untitled",
                chunk.ChunkText[..Math.Min(chunk.ChunkText.Length, 120)]);
        }
    }

    private async Task ListDocumentsAsync(CancellationToken cancellationToken)
    {
        var docs = await Client.ListDocumentsAsync(cancellationToken);
        Logger.LogInformation("Documents in store: {Count}", docs.Count);

        foreach (var doc in docs)
        {
            Logger.LogInformation("  [{Id}] {Title} ({Pages} pages)",
                doc.Id, doc.Title ?? doc.SourcePath, doc.PageCount);
        }
    }
}
