using Muthur.Agents;
using Muthur.Api;
using Muthur.Api.Routes;
using Muthur.Data;
using Muthur.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddMuthurData("muthur-db", "muthur-cache");
builder.AddMuthurSignalR("muthur-cache");
builder.AddMuthurTemporalClient();
builder.AddAgentEmbeddingGenerator();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapAgentRoutes();
app.MapDocumentRoutes();
app.MapRelayRoutes();

await app.RunAsync();
