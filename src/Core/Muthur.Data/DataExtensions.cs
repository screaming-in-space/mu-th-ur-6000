using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muthur.PostgreSql;
using Npgsql;
using Pgvector.Dapper;

namespace Muthur.Data;

public static class DataExtensions
{
    /// <summary>
    /// Registers Postgres (with pgvector), Redis distributed cache,
    /// document repository with caching decorator.
    /// Set <paramref name="runMigrations"/> to false when another service owns migrations.
    /// </summary>
    public static IHostApplicationBuilder AddMuthurData(
        this IHostApplicationBuilder builder,
        string postgresConnectionName,
        string redisConnectionName,
        bool runMigrations = true)
    {
        builder.AddMuthurPostgreSql(postgresConnectionName, configureDataSource: dsb =>
        {
            dsb.UseVector();
        }, runMigrations);

        SqlMapper.AddTypeHandler(new VectorTypeHandler());

        builder.AddRedisDistributedCache(redisConnectionName);

        builder.Services.AddSingleton<DocumentRepository>();
        builder.Services.AddSingleton<IDocumentRepository, CachedDocumentRepository>();

        return builder;
    }
}
