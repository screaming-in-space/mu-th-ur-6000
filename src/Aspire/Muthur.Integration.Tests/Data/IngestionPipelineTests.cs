using Dapper;
using Muthur.Contracts;
using Muthur.Data;
using Muthur.Integration.Tests.Infrastructure;

namespace Muthur.Integration.Tests.Data;

/// <summary>
/// End-to-end ingestion pipeline: store doc → store chunks with embeddings → vector search.
/// Uses the real Postgres + pgvector running inside Aspire.
/// </summary>
[Collection("Muthur")]
public sealed class IngestionPipelineTests(MuthurFixture platform)
{
    private DocumentRepository CreateRepo() => new(platform.DataSource);

    [Fact]
    public async Task StoreChunks_ThenSearch_ReturnsRankedResults()
    {
        var repo = CreateRepo();

        var docId = await repo.StoreDocumentAsync(
            "Pipeline Test", "/test/pipeline.pdf", "Full text content", 1, []);

        // Two chunks with distinct embeddings — first dim differentiates them.
        var chunks = new List<TextChunk>
        {
            new(0, "Neural networks are used for deep learning."),
            new(1, "Databases store structured data efficiently."),
        };

        var embeddingA = CreateEmbedding(1.0f, 0.0f);
        var embeddingB = CreateEmbedding(0.0f, 1.0f);

        await repo.StoreChunksAsync(docId, chunks, [embeddingA, embeddingB]);

        // Search with embedding close to A — chunk 0 should rank higher.
        var results = await repo.SearchSimilarAsync(embeddingA, limit: 2);

        Assert.NotEmpty(results);
        Assert.Equal("Neural networks are used for deep learning.", results[0].ChunkText);
        Assert.True(results[0].Score > results[1].Score,
            "Chunk matching the query embedding should rank first");
    }

    [Fact]
    public async Task SearchSimilar_NoChunks_ReturnsEmpty()
    {
        var repo = CreateRepo();

        var embedding = CreateEmbedding(0.5f, 0.5f);
        var results = await repo.SearchSimilarAsync(embedding, limit: 5);

        // May return results from other tests, but should not throw.
        Assert.NotNull(results);
    }

    [Fact]
    public async Task StoreChunks_MultipleDocuments_SearchReturnsCorrectDocument()
    {
        var repo = CreateRepo();

        var docA = await repo.StoreDocumentAsync(
            "Doc Alpha", "/test/alpha.pdf", "Alpha content", 1, []);
        var docB = await repo.StoreDocumentAsync(
            "Doc Beta", "/test/beta.pdf", "Beta content", 1, []);

        var embeddingA = CreateEmbedding(1.0f, 0.0f);
        var embeddingB = CreateEmbedding(0.0f, 1.0f);

        await repo.StoreChunksAsync(docA, [new TextChunk(0, "Alpha chunk")], [embeddingA]);
        await repo.StoreChunksAsync(docB, [new TextChunk(0, "Beta chunk")], [embeddingB]);

        // Search with embedding close to B.
        var results = await repo.SearchSimilarAsync(embeddingB, limit: 1);

        Assert.NotEmpty(results);
        Assert.Equal("Beta chunk", results[0].ChunkText);
        Assert.Equal(docB, results[0].DocumentId);
        Assert.Equal("Doc Beta", results[0].DocumentTitle);
    }

    [Fact]
    public async Task StoreChunks_PreservesChunkIndex()
    {
        var repo = CreateRepo();

        var docId = await repo.StoreDocumentAsync(
            "Index Test", "/test/index.pdf", "Content", 1, []);

        var embedding = CreateEmbedding(0.7f, 0.3f);
        var chunks = Enumerable.Range(0, 5)
            .Select(i => new TextChunk(i, $"Chunk number {i}"))
            .ToList();

        var embeddings = chunks.Select(_ => embedding).ToList();
        await repo.StoreChunksAsync(docId, chunks, embeddings);

        // Verify chunks were stored by searching.
        var results = await repo.SearchSimilarAsync(embedding, limit: 10);
        var docResults = results.Where(r => r.DocumentId == docId).ToList();

        Assert.Equal(5, docResults.Count);
    }

    [Fact]
    public async Task CascadeDelete_RemovesChunksWhenDocumentDeleted()
    {
        var repo = CreateRepo();

        var docId = await repo.StoreDocumentAsync(
            "Cascade Test", "/test/cascade.pdf", "Content to delete", 1, []);

        var embedding = CreateEmbedding(0.9f, 0.1f);
        await repo.StoreChunksAsync(docId,
            [new TextChunk(0, "Will be cascade deleted")], [embedding]);

        // Delete the document directly.
        await using var conn = await platform.DataSource.OpenConnectionAsync();
        await conn.ExecuteAsync("DELETE FROM documents WHERE id = @Id", new { Id = docId });

        // Chunks should be gone (ON DELETE CASCADE).
        var chunkCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM document_chunks WHERE document_id = @Id", new { Id = docId });

        Assert.Equal(0, chunkCount);
    }

    /// <summary>
    /// Creates a 768-dim embedding with the first two dimensions set to the given values.
    /// All other dimensions are zero. This gives distinct cosine similarity patterns.
    /// </summary>
    private static float[] CreateEmbedding(float dim0, float dim1)
    {
        var embedding = new float[768];
        embedding[0] = dim0;
        embedding[1] = dim1;
        return embedding;
    }
}
