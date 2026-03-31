using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Muthur.Contracts;
using Muthur.Data;
using Temporalio.Activities;

namespace Muthur.Bishop.Worker.Activities;

/// <summary>
/// Activities for the document ingestion child workflow.
/// Each step is individually checkpointed by Temporal.
/// </summary>
public class IngestionActivities(
    ILogger<IngestionActivities> logger,
    IDocumentRepository repository,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
{
    private const int ChunkSize = 500;   // ~500 tokens ≈ 2000 chars
    private const int ChunkOverlap = 50; // ~50 tokens ≈ 200 chars
    private const int CharsPerToken = 4;

    [Activity]
    public Task<TextChunk[]> ChunkTextAsync(string text)
    {
        var maxChars = ChunkSize * CharsPerToken;
        var overlapChars = ChunkOverlap * CharsPerToken;
        var chunks = new List<TextChunk>();
        var index = 0;
        var position = 0;

        while (position < text.Length)
        {
            var end = Math.Min(position + maxChars, text.Length);
            var chunk = text[position..end];

            // Try to break at a sentence or paragraph boundary.
            if (end < text.Length)
            {
                var lastBreak = chunk.LastIndexOf('\n');
                if (lastBreak < 0) lastBreak = chunk.LastIndexOf(". ");
                if (lastBreak > maxChars / 2)
                {
                    chunk = chunk[..(lastBreak + 1)];
                    end = position + lastBreak + 1;
                }
            }

            chunks.Add(new TextChunk(index++, chunk.Trim()));
            var nextPosition = end - overlapChars;
            position = nextPosition <= position ? end : nextPosition; // prevent infinite loop
        }

        logger.LogInformation("Chunked {TextLength} chars into {ChunkCount} chunks", text.Length, chunks.Count);
        return Task.FromResult(chunks.ToArray());
    }

    [Activity]
    public async Task<float[][]> GenerateEmbeddingsAsync(TextChunk[] chunks)
    {
        logger.LogInformation("Generating embeddings for {ChunkCount} chunks", chunks.Length);

        var texts = chunks.Select(c => c.Text).ToList();
        var result = await embeddingGenerator.GenerateAsync(texts);
        var embeddings = result.Select(e => e.Vector.ToArray()).ToArray();

        logger.LogInformation("Generated {Count} embeddings ({Dims} dims each)",
            embeddings.Length, embeddings.FirstOrDefault()?.Length ?? 0);

        return embeddings;
    }

    [Activity]
    public async Task StoreChunksAsync(Guid documentId, TextChunk[] chunks, float[][] embeddings)
    {
        logger.LogInformation("Storing {ChunkCount} chunks for document {DocumentId}", chunks.Length, documentId);
        await repository.StoreChunksAsync(documentId, chunks, embeddings);
        logger.LogInformation("Stored {ChunkCount} chunks for document {DocumentId}", chunks.Length, documentId);
    }
}
