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
/// waits for health, and exposes clients. Migrations run through the
/// real DatabaseMigrationService hosted in the API and Worker — not
/// duplicated here.
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

        // Wait for API to be healthy — this means Postgres is up and
        // DatabaseMigrationService has run through the real code path.
        await notifications.WaitForResourceHealthyAsync("muthur-api");
        ApiHttpClient = _app.CreateHttpClient("muthur-api");

        // Direct data source for test assertions that query the DB.
        var connectionString = await _app.GetConnectionStringAsync("muthur-db");
        var dsb = new NpgsqlDataSourceBuilder(connectionString);
        dsb.UseVector();
        _dataSource = dsb.Build();
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
}
