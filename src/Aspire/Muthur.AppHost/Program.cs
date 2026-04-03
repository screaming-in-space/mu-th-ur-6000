using Aspire.Hosting.LMStudio;
using Aspire.Hosting.Temporal;

var builder = DistributedApplication.CreateBuilder(args);

await builder.EnsureDockerAsync();

var temporal = builder.AddTemporalDevServer("muthur-temporal-dev");

var lmstudio = builder.AddLMStudio("muthur-lmstudio")
    .WithModel("unsloth/nvidia-nemotron-3-nano-4b")
    .WithEmbeddingModel("nomic-embed-text-v1.5");

var postgres = builder.AddPostgres("muthur-postgres")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg17")
    .AddDatabase("muthur-db");

var cache = builder.AddRedis("muthur-cache")
    .WithLifetime(ContainerLifetime.Persistent);

var api = builder.AddProject<Projects.Muthur_Api>("muthur-api")
    .WithReference(temporal)
    .WithReference(lmstudio)
    .WithReference(postgres)
    .WithReference(cache)
    .WaitFor(temporal)
    .WaitFor(lmstudio)
    .WaitFor(postgres)
    .WaitFor(cache);

var worker = builder.AddProject<Projects.Muthur_Bishop_Worker>("muthur-bishop-worker")
    .WithReference(temporal)
    .WithReference(lmstudio)
    .WithReference(postgres)
    .WithReference(cache)
    .WithReference(api)
    .WaitFor(temporal)
    .WaitFor(lmstudio)
    .WaitFor(postgres)
    .WaitFor(cache)
    .WaitFor(api);

builder.AddProject<Projects.Muthur_Console>("muthur-console")
    .WithReference(api)
    .WaitFor(api)
    .WaitFor(worker);

await builder.Build().RunAsync();
