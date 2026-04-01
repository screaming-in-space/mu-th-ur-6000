using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Dapper;
using DbUp;
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

        await RunMigrationsAsync(connectionString!);

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

    private static Task RunMigrationsAsync(string connectionString)
    {
        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(Muthur.PostgreSql.DatabaseMigrationService).Assembly)
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            throw result.Error;
        }

        return Task.CompletedTask;
    }
}
