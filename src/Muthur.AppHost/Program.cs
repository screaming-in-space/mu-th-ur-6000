var builder = DistributedApplication.CreateBuilder(args);

// Temporal dev server — the durable execution backbone.
// In production, point to Temporal Cloud or a self-hosted cluster.
// For local dev, run `temporal server start-dev` separately.

var api = builder.AddProject<Projects.MuThUr_Api>("muthr-api");
var worker = builder.AddProject<Projects.MuThUr_Worker>("muthr-worker");

builder.Build().Run();
