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
    /// Registers Postgres (with pgvector + DbUp migrations), Redis distributed cache,
    /// document repository with caching decorator.
    /// </summary>
    public static IHostApplicationBuilder AddMuthurData(
        this IHostApplicationBuilder builder,
        string postgresConnectionName,
        string redisConnectionName)
    {
        // Postgres + pgvector + DbUp migrations through the shared PostgreSql project.
        builder.AddMuthurPostgreSql(postgresConnectionName, configureDataSource: dsb =>
        {
            dsb.UseVector();
        });

        SqlMapper.AddTypeHandler(new VectorTypeHandler());

        builder.AddRedisDistributedCache(redisConnectionName);

        builder.Services.AddSingleton<DocumentRepository>();
        builder.Services.AddSingleton<IDocumentRepository, CachedDocumentRepository>();

        return builder;
    }
}
