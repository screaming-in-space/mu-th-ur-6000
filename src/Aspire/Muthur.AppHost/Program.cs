using Aspire.Hosting.Temporal;

var builder = DistributedApplication.CreateBuilder(args);

await builder.EnsureDockerAsync();

var temporal = builder.AddTemporalDevServer("muthur-temporal-dev");

var postgres = builder.AddPostgres("muthur-postgres")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg17")
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase("muthur-db");

var cache = builder.AddRedis("muthur-cache")
    .WithLifetime(ContainerLifetime.Persistent);

var api = builder.AddProject<Projects.Muthur_Api>("muthur-api")
    .WithReference(postgres)
    .WithReference(cache)
    .WaitFor(postgres)
    .WaitFor(cache);

var worker = builder.AddProject<Projects.Muthur_Bishop_Worker>("muthur-bishop-worker")
    .WithReference(temporal)
    .WithReference(postgres)
    .WithReference(cache)
    .WaitFor(temporal)
    .WaitFor(postgres)
    .WaitFor(cache);

builder.AddProject<Projects.Muthur_Console>("muthur-console")
    .WithReference(api)
    .WaitFor(api);

await builder.Build().RunAsync();
