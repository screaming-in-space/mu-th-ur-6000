var builder = DistributedApplication.CreateBuilder(args);

// Temporal dev server — the durable execution backbone.
// In production, point to Temporal Cloud or a self-hosted cluster.
// For local dev, run `temporal server start-dev` separately.

var api = builder.AddProject<Projects.Muthur_Api>("Muthur-api");
var worker = builder.AddProject<Projects.Muthur_Bishop_Worker>("Muthur-bishop-worker");

await builder.Build().RunAsync();
