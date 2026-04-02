using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Muthur.Api;

public static class SignalRExtensions
{
    /// <summary>
    /// Registers SignalR with a Redis backplane, reusing the Aspire-managed
    /// IConnectionMultiplexer keyed to <paramref name="redisConnectionName"/>.
    /// </summary>
    public static IHostApplicationBuilder AddMuthurSignalR(
        this IHostApplicationBuilder builder,
        string redisConnectionName)
    {
        var connectionString = builder.Configuration.GetConnectionString(redisConnectionName);

        if (!string.IsNullOrEmpty(connectionString))
        {
            builder.Services.AddSignalR()
                .AddStackExchangeRedis(options =>
                {
                    options.Configuration.ChannelPrefix = RedisChannel.Literal("muthur-relay");
                });

            builder.Services.AddSingleton<IConfigureOptions<RedisOptions>>(sp =>
                new ConfigureOptions<RedisOptions>(options =>
                {
                    var mux = sp.GetKeyedService<IConnectionMultiplexer>(redisConnectionName);
                    if (mux is not null)
                    {
                        options.ConnectionFactory = _ => Task.FromResult(mux);
                    }
                    else
                    {
                        options.Configuration = ConfigurationOptions.Parse(connectionString);
                    }
                }));
        }
        else
        {
            builder.Services.AddSignalR();
        }

        return builder;
    }
}
