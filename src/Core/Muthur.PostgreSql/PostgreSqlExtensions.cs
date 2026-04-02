using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Muthur.PostgreSql;

public static class PostgreSqlExtensions
{
    /// <summary>
    /// Registers <see cref="NpgsqlDataSource"/> via Aspire and runs DbUp embedded migrations on startup.
    /// Callers that need pgvector should configure the data source builder via
    /// <paramref name="configureDataSource"/>.
    /// </summary>
    public static IHostApplicationBuilder AddMuthurPostgreSql(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<NpgsqlDataSourceBuilder>? configureDataSource = null,
        bool runMigrations = true)
    {
        builder.AddNpgsqlDataSource(connectionName, configureDataSourceBuilder: dsb =>
        {
            // Npgsql strips the password from ConnectionString by default.
            // DbUp reads ConnectionString directly, so it needs the password retained.
            dsb.ConnectionStringBuilder.PersistSecurityInfo = true;
            configureDataSource?.Invoke(dsb);
        });

        if (runMigrations)
        {
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, DatabaseMigrationService>());
        }

        return builder;
    }
}
