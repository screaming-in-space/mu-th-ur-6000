using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Muthur.PostgreSql;
using Npgsql;
using Pgvector.Dapper;

namespace Muthur.Data;

public static class DataExtensions
{
    /// <summary>
    /// Registers Postgres (with pgvector + DbUp migrations), Redis distributed cache,
    /// document repository with caching decorator.
    /// </summary>
    public static IHostApplicationBuilder AddMuthurData(
        this IHostApplicationBuilder builder,
        string postgresConnectionName,
        string redisConnectionName)
    {
        // Register pgvector type mapping on the Npgsql data source + Dapper type handler.
        builder.AddNpgsqlDataSource(postgresConnectionName, configureDataSourceBuilder: dsb =>
        {
            dsb.UseVector();
        });

        SqlMapper.AddTypeHandler(new VectorTypeHandler());

        // Register DbUp migrations from the PostgreSql project (without re-registering the data source).
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, DatabaseMigrationService>());

        builder.AddRedisDistributedCache(redisConnectionName);

        builder.Services.AddSingleton<DocumentRepository>();
        builder.Services.AddSingleton<IDocumentRepository, CachedDocumentRepository>();

        return builder;
    }
}
