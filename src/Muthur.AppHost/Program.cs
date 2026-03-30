var builder = DistributedApplication.CreateBuilder(args);

await builder.EnsureDockerAsync();

var temporal = builder.AddTemporalDevServer("muthur-temporal-dev");

var api = builder.AddProject<Projects.Muthur_Api>("muthur-api");

var worker = builder.AddProject<Projects.Muthur_Bishop_Worker>("muthur-bishop-worker")
    .WithReference(temporal)
    .WaitFor(temporal);

await builder.Build().RunAsync();
