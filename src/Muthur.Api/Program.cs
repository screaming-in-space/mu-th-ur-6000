using MuThUr.Api.Routes;
using MuThUr.ServiceDefaults;
using Temporalio.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Register Temporal client for signaling agent workflows.
builder.Services.AddTemporalClient(options =>
{
    options.TargetHost = builder.Configuration["Temporal:Address"] ?? "localhost:7233";
    options.Namespace = builder.Configuration["Temporal:Namespace"] ?? "default";
});

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapAgentRoutes();

app.Run();
