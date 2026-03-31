using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Muthur.Data;

/// <summary>
/// Runs schema migrations on startup - creates the documents and chunks tables
/// with pgvector extension. Idempotent via IF NOT EXISTS.
/// </summary>
public sealed class MigrationService(
    NpgsqlDataSource dataSource,
    ILogger<MigrationService> logger) : IHostedService
{
    private const string MigrationSql = """
        CREATE EXTENSION IF NOT EXISTS vector;

        CREATE TABLE IF NOT EXISTS documents (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            title TEXT,
            source_path TEXT NOT NULL,
            content TEXT NOT NULL,
            page_count INT NOT NULL DEFAULT 0,
            metadata JSONB NOT NULL DEFAULT '{}',
            created_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS document_chunks (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            document_id UUID NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
            chunk_index INT NOT NULL,
            chunk_text TEXT NOT NULL,
            embedding vector(768),
            created_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        -- Migrate embedding column if dimensions changed (e.g. 1536 → 768).
        -- Truncates chunks since old-dimension embeddings are incompatible.
        DO $migrate$
        BEGIN
            IF EXISTS (
                SELECT 1 FROM pg_attribute
                WHERE attrelid = 'document_chunks'::regclass
                  AND attname = 'embedding'
                  AND atttypmod <> 768 + 4
            ) THEN
                DROP INDEX IF EXISTS idx_chunks_embedding;
                TRUNCATE document_chunks;
                ALTER TABLE document_chunks ALTER COLUMN embedding TYPE vector(768);
            END IF;
        END $migrate$;

        CREATE INDEX IF NOT EXISTS idx_chunks_document ON document_chunks(document_id);
        CREATE INDEX IF NOT EXISTS idx_chunks_embedding ON document_chunks
            USING hnsw (embedding vector_cosine_ops);
        """;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Running database migrations...");
            await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = MigrationSql;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            logger.LogInformation("Database migrations complete.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed — the service will continue but data operations may fail");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
