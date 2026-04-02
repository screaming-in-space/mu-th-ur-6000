using DbUp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Muthur.PostgreSql;

public sealed class DatabaseMigrationService(
    ILogger<DatabaseMigrationService> logger,
    NpgsqlDataSource dataSource) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var connectionString = dataSource.ConnectionString;

        logger.LogInformation("Running database migrations...");

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrationService).Assembly)
            .WithVariablesDisabled()
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            logger.LogError(result.Error, "Database migration failed.");
            throw result.Error;
        }

        var scriptsApplied = result.Scripts.Count();

        logger.LogInformation(
            "Database migrations complete. {Count} script(s) applied.",
            scriptsApplied);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
