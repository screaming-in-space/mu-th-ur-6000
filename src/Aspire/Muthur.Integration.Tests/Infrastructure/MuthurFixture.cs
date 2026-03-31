using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pgvector.Dapper;

namespace Muthur.Integration.Tests.Infrastructure;

/// <summary>
/// Shared Aspire fixture — one instance per test run.
/// Starts the full AppHost (Temporal, Postgres, Redis, API, Worker),
/// waits for health, runs migrations, and exposes clients.
/// </summary>
public sealed class MuthurFixture : IAsyncLifetime
{
    private DistributedApplication _app = null!;
    private NpgsqlDataSource _dataSource = null!;

    public DistributedApplication App => _app;
    public NpgsqlDataSource DataSource => _dataSource;
    public HttpClient ApiHttpClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        SqlMapper.AddTypeHandler(new VectorTypeHandler());

        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Muthur_AppHost>();

        _app = await builder.BuildAsync();
        await _app.StartAsync();

        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();

        // Wait for Postgres and run migrations.
        await notifications.WaitForResourceHealthyAsync("muthur-db");

        var connectionString = await _app.GetConnectionStringAsync("muthur-db");
        var dsb = new NpgsqlDataSourceBuilder(connectionString);
        dsb.UseVector();
        _dataSource = dsb.Build();

        await RunMigrationsAsync();

        // Wait for API to be healthy, then create client.
        await notifications.WaitForResourceHealthyAsync("muthur-api");
        ApiHttpClient = _app.CreateHttpClient("muthur-api");
    }

    public async Task DisposeAsync()
    {
        ApiHttpClient?.Dispose();
        _dataSource?.Dispose();

        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private async Task RunMigrationsAsync()
    {
        const string sql = """
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

        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}
