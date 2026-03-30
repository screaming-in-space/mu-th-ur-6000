using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Pgvector.Dapper;

namespace Muthur.Data;

public static class DataExtensions
{
    /// <summary>
    /// Registers Postgres (with pgvector), Redis distributed cache,
    /// document repository, and schema migration service.
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

        builder.AddRedisDistributedCache(redisConnectionName);

        builder.Services.AddSingleton<DocumentRepository>();
        builder.Services.AddSingleton<IDocumentRepository, CachedDocumentRepository>();
        builder.Services.AddHostedService<MigrationService>();

        return builder;
    }
}
