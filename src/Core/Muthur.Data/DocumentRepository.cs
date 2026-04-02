using Dapper;
using Microsoft.Extensions.Logging;
using Muthur.Contracts;
using Npgsql;
using Pgvector;
using System.Text.Json;

namespace Muthur.Data;

public sealed class DocumentRepository(
    ILogger<DocumentRepository> logger,
    NpgsqlDataSource dataSource) : IDocumentRepository
{
    public async Task<Guid> StoreDocumentAsync(string? title, string sourcePath, string content,
        int pageCount, Dictionary<string, string> metadata, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO documents (title, source_path, content, page_count, metadata)
            VALUES (@Title, @SourcePath, @Content, @PageCount, @Metadata::jsonb)
            RETURNING id
            """;

        await using var conn = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        var id = await conn.QuerySingleAsync<Guid>(new CommandDefinition(sql, new
        {
            Title = title,
            SourcePath = sourcePath,
            Content = content,
            PageCount = pageCount,
            Metadata = JsonSerializer.Serialize(metadata)
        }, cancellationToken: ct)).ConfigureAwait(false);

        logger.LogInformation("Stored document {DocumentId} — {Title}, {PageCount} pages",
            id, title ?? "(untitled)", pageCount);

        return id;
    }

    private const int ChunkBatchSize = 10;

    public async Task StoreChunksAsync(Guid documentId, IReadOnlyList<TextChunk> chunks,
        IReadOnlyList<float[]> embeddings, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO document_chunks (document_id, chunk_index, chunk_text, embedding)
            VALUES (@DocumentId, @ChunkIndex, @ChunkText, @Embedding)
            """;

        await using var conn = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

        // Batch inserts to reduce per-command overhead and log noise.
        for (var batch = 0; batch < chunks.Count; batch += ChunkBatchSize)
        {
            var batchEnd = Math.Min(batch + ChunkBatchSize, chunks.Count);
            var parameters = new List<object>(ChunkBatchSize);

            for (var i = batch; i < batchEnd; i++)
            {
                parameters.Add(new
                {
                    DocumentId = documentId,
                    ChunkIndex = chunks[i].Index,
                    ChunkText = chunks[i].Text,
                    Embedding = new Vector(embeddings[i])
                });
            }

            await conn.ExecuteAsync(new CommandDefinition(sql, parameters,
                transaction: tx, cancellationToken: ct)).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Stored {ChunkCount} chunks for document {DocumentId} ({Batches} batches)",
            chunks.Count, documentId, (chunks.Count + ChunkBatchSize - 1) / ChunkBatchSize);
    }

    public async Task<DocumentRecord?> GetDocumentAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT id, title, source_path, page_count, metadata, created_at
            FROM documents WHERE id = @Id
            """;

        await using var conn = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        var row = await conn.QuerySingleOrDefaultAsync<DocumentRow>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);

        return row is null ? null : new DocumentRecord(
            row.Id, row.Title, row.Source_Path, row.Page_Count,
            JsonSerializer.Deserialize<Dictionary<string, string>>(row.Metadata) ?? [],
            row.Created_At);
    }

    public async Task<string?> GetDocumentContentAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "SELECT content FROM documents WHERE id = @Id";
        await using var conn = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        return await conn.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT id, title, source_path, page_count, created_at
            FROM documents ORDER BY created_at DESC
            """;

        await using var conn = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<DocumentSummaryRow>(
            new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);

        return rows.Select(r => new DocumentSummary(
            r.Id, r.Title, r.Source_Path, r.Page_Count, r.Created_At)).ToList();
    }

    public async Task<IReadOnlyList<SimilarChunk>> SearchSimilarAsync(float[] queryEmbedding,
        int limit = 5, CancellationToken ct = default)
    {
        const string sql = """
            SELECT c.chunk_text, c.document_id, d.title AS document_title,
                   1 - (c.embedding <=> @Embedding) AS score
            FROM document_chunks c
            JOIN documents d ON d.id = c.document_id
            WHERE c.embedding IS NOT NULL
            ORDER BY c.embedding <=> @Embedding
            LIMIT @Limit
            """;

        await using var conn = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<SimilarChunkRow>(
            new CommandDefinition(sql, new
            {
                Embedding = new Vector(queryEmbedding),
                Limit = limit
            }, cancellationToken: ct)).ConfigureAwait(false);

        var results = rows.Select(r => new SimilarChunk(
            r.Chunk_Text, r.Document_Id, r.Document_Title, r.Score)).ToList();

        logger.LogInformation("Vector search returned {ResultCount} chunks (limit={Limit})",
            results.Count, limit);

        return results;
    }

    // Dapper row types - snake_case matches Postgres column names.
    private sealed record DocumentRow(Guid Id, string? Title, string Source_Path,
        int Page_Count, string Metadata, DateTime Created_At);

    private sealed record DocumentSummaryRow(Guid Id, string? Title, string Source_Path,
        int Page_Count, DateTime Created_At);

    private sealed record SimilarChunkRow(string Chunk_Text, Guid Document_Id,
        string? Document_Title, double Score);
}
