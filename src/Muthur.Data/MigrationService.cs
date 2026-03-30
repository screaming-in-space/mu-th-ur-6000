using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Muthur.Data;

/// <summary>
/// Runs schema migrations on startup — creates the documents and chunks tables
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
            embedding vector(1536),
            created_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS idx_chunks_document ON document_chunks(document_id);
        CREATE INDEX IF NOT EXISTS idx_chunks_embedding ON document_chunks
            USING hnsw (embedding vector_cosine_ops);
        """;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Running database migrations...");
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = MigrationSql;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        logger.LogInformation("Database migrations complete.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
