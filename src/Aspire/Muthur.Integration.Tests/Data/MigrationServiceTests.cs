using Dapper;
using Muthur.Integration.Tests.Infrastructure;

namespace Muthur.Integration.Tests.Data;

/// <summary>
/// Verifies the MigrationService created the expected schema —
/// tables, extensions, and indexes all exist after AppHost startup.
/// </summary>
[Collection("Muthur")]
public sealed class MigrationServiceTests(MuthurFixture platform)
{
    [Fact]
    public async Task PgvectorExtension_IsInstalled()
    {
        await using var conn = await platform.DataSource.OpenConnectionAsync();

        var exists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'vector')");

        Assert.True(exists, "pgvector extension should be installed");
    }

    [Fact]
    public async Task DocumentsTable_Exists()
    {
        await using var conn = await platform.DataSource.OpenConnectionAsync();

        var exists = await conn.ExecuteScalarAsync<bool>("""
            SELECT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_name = 'documents')
            """);

        Assert.True(exists, "documents table should exist");
    }

    [Fact]
    public async Task DocumentChunksTable_Exists()
    {
        await using var conn = await platform.DataSource.OpenConnectionAsync();

        var exists = await conn.ExecuteScalarAsync<bool>("""
            SELECT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_name = 'document_chunks')
            """);

        Assert.True(exists, "document_chunks table should exist");
    }

    [Fact]
    public async Task DocumentChunks_EmbeddingColumn_IsVector768()
    {
        await using var conn = await platform.DataSource.OpenConnectionAsync();

        // atttypmod for vector(N) is N + 4.
        var typmod = await conn.ExecuteScalarAsync<int>("""
            SELECT atttypmod FROM pg_attribute
            WHERE attrelid = 'document_chunks'::regclass
              AND attname  = 'embedding'
            """);

        Assert.Equal(768 + 4, typmod);
    }

    [Fact]
    public async Task HnswIndex_Exists()
    {
        await using var conn = await platform.DataSource.OpenConnectionAsync();

        var exists = await conn.ExecuteScalarAsync<bool>("""
            SELECT EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE tablename = 'document_chunks'
                  AND indexname  = 'idx_chunks_embedding')
            """);

        Assert.True(exists, "HNSW index idx_chunks_embedding should exist");
    }

    [Fact]
    public async Task DocumentIdIndex_Exists()
    {
        await using var conn = await platform.DataSource.OpenConnectionAsync();

        var exists = await conn.ExecuteScalarAsync<bool>("""
            SELECT EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE tablename = 'document_chunks'
                  AND indexname  = 'idx_chunks_document')
            """);

        Assert.True(exists, "B-tree index idx_chunks_document should exist");
    }

    [Fact]
    public async Task DocumentsTable_HasExpectedColumns()
    {
        await using var conn = await platform.DataSource.OpenConnectionAsync();

        var columns = (await conn.QueryAsync<string>("""
            SELECT column_name FROM information_schema.columns
            WHERE table_name = 'documents'
            ORDER BY ordinal_position
            """)).ToList();

        Assert.Contains("id", columns);
        Assert.Contains("title", columns);
        Assert.Contains("source_path", columns);
        Assert.Contains("content", columns);
        Assert.Contains("page_count", columns);
        Assert.Contains("metadata", columns);
        Assert.Contains("created_at", columns);
    }

    [Fact]
    public async Task DocumentChunksTable_HasExpectedColumns()
    {
        await using var conn = await platform.DataSource.OpenConnectionAsync();

        var columns = (await conn.QueryAsync<string>("""
            SELECT column_name FROM information_schema.columns
            WHERE table_name = 'document_chunks'
            ORDER BY ordinal_position
            """)).ToList();

        Assert.Contains("id", columns);
        Assert.Contains("document_id", columns);
        Assert.Contains("chunk_index", columns);
        Assert.Contains("chunk_text", columns);
        Assert.Contains("embedding", columns);
        Assert.Contains("created_at", columns);
    }

    [Fact]
    public async Task ForeignKey_DocumentChunks_ReferencesDocuments()
    {
        await using var conn = await platform.DataSource.OpenConnectionAsync();

        var exists = await conn.ExecuteScalarAsync<bool>("""
            SELECT EXISTS (
                SELECT 1 FROM information_schema.table_constraints tc
                JOIN information_schema.constraint_column_usage ccu
                  ON tc.constraint_name = ccu.constraint_name
                WHERE tc.table_name      = 'document_chunks'
                  AND tc.constraint_type  = 'FOREIGN KEY'
                  AND ccu.table_name      = 'documents'
                  AND ccu.column_name     = 'id')
            """);

        Assert.True(exists, "document_chunks.document_id should FK → documents.id");
    }
}
