using System.Text.Json;
using Dapper;
using Muthur.Contracts;
using Npgsql;
using Pgvector;

namespace Muthur.Data;

public sealed class DocumentRepository(NpgsqlDataSource dataSource) : IDocumentRepository
{
    public async Task<Guid> StoreDocumentAsync(string? title, string sourcePath, string content,
        int pageCount, Dictionary<string, string> metadata, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO documents (title, source_path, content, page_count, metadata)
            VALUES (@Title, @SourcePath, @Content, @PageCount, @Metadata::jsonb)
            RETURNING id
            """;

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return await conn.QuerySingleAsync<Guid>(new CommandDefinition(sql, new
        {
            Title = title,
            SourcePath = sourcePath,
            Content = content,
            PageCount = pageCount,
            Metadata = JsonSerializer.Serialize(metadata)
        }, cancellationToken: ct));
    }

    public async Task StoreChunksAsync(Guid documentId, IReadOnlyList<TextChunk> chunks,
        IReadOnlyList<float[]> embeddings, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO document_chunks (document_id, chunk_index, chunk_text, embedding)
            VALUES (@DocumentId, @ChunkIndex, @ChunkText, @Embedding)
            """;

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        for (var i = 0; i < chunks.Count; i++)
        {
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                DocumentId = documentId,
                ChunkIndex = chunks[i].Index,
                ChunkText = chunks[i].Text,
                Embedding = new Vector(embeddings[i])
            }, transaction: tx, cancellationToken: ct));
        }

        await tx.CommitAsync(ct);
    }

    public async Task<DocumentRecord?> GetDocumentAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT id, title, source_path, page_count, metadata, created_at
            FROM documents WHERE id = @Id
            """;

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<DocumentRow>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        return row is null ? null : new DocumentRecord(
            row.Id, row.Title, row.Source_Path, row.Page_Count,
            JsonSerializer.Deserialize<Dictionary<string, string>>(row.Metadata) ?? [],
            row.Created_At);
    }

    public async Task<string?> GetDocumentContentAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "SELECT content FROM documents WHERE id = @Id";
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT id, title, source_path, page_count, created_at
            FROM documents ORDER BY created_at DESC
            """;

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<DocumentSummaryRow>(
            new CommandDefinition(sql, cancellationToken: ct));

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

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<SimilarChunkRow>(
            new CommandDefinition(sql, new
            {
                Embedding = new Vector(queryEmbedding),
                Limit = limit
            }, cancellationToken: ct));

        return rows.Select(r => new SimilarChunk(
            r.Chunk_Text, r.Document_Id, r.Document_Title, r.Score)).ToList();
    }

    // Dapper row types — snake_case matches Postgres column names.
    private sealed record DocumentRow(Guid Id, string? Title, string Source_Path,
        int Page_Count, string Metadata, DateTimeOffset Created_At);

    private sealed record DocumentSummaryRow(Guid Id, string? Title, string Source_Path,
        int Page_Count, DateTimeOffset Created_At);

    private sealed record SimilarChunkRow(string Chunk_Text, Guid Document_Id,
        string? Document_Title, double Score);
}
