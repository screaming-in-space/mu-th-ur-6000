using Microsoft.Extensions.Configuration;
using Muthur.Api.Routes;
using Muthur.Data;
using Muthur.ServiceDefaults;
using Temporalio.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddMuthurData("muthur-db", "muthur-cache");
builder.AddAgentEmbeddingGenerator();
builder.Services.AddOpenApi();

builder.Services.AddTemporalClient(options =>
{
    options.TargetHost = builder.Configuration.GetConnectionString("muthur-temporal-dev")
        ?? builder.Configuration["Temporal:Address"]
        ?? "localhost:7233";
    options.Namespace = builder.Configuration["Temporal:Namespace"] ?? "default";
});

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapAgentRoutes();
app.MapDocumentRoutes();

app.Run();
