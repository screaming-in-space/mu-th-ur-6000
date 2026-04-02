using Muthur.Api;
using Muthur.Api.Routes;
using Muthur.Data;
using Muthur.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddMuthurData("muthur-db", "muthur-cache");
builder.AddMuthurSignalR("muthur-cache");
builder.AddAgentEmbeddingGenerator();
builder.Services.AddOpenApi();

var temporalHost = builder.Configuration.GetConnectionString("muthur-temporal-dev")
    ?? builder.Configuration["Temporal:Address"]
    ?? "localhost:7233";
var temporalNamespace = builder.Configuration["Temporal:Namespace"] ?? "default";

builder.Services.AddTemporalClient(temporalHost, temporalNamespace);

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapAgentRoutes();
app.MapDocumentRoutes();
app.MapRelayRoutes();

await app.RunAsync();
