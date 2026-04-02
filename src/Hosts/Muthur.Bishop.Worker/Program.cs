using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Muthur.Bishop.Worker.Activities;
using Muthur.Bishop.Worker.Workflows;
using Muthur.Contracts;
using Muthur.Data;
using Muthur.ServiceDefaults;
using Muthur.Tools;
using Muthur.Tools.Handlers;
using Temporalio.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<HostOptions>(options =>
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

builder.AddServiceDefaults();
builder.AddMuthurData("muthur-db", "muthur-cache");
builder.AddAgentChatClient();
builder.AddAgentEmbeddingGenerator();

// Tools - isolated project, independently testable.
builder.Services.AddSingleton<DocumentStoreHandler>();
builder.Services.AddSingleton<ToolRegistry>();

// Named HTTP client for relay notifications — resolves via Aspire service discovery.
// Named (not typed) because AddScopedActivities owns the registration of NotificationActivities.
builder.Services.AddHttpClient(NotificationActivities.HttpClientName, c =>
    c.BaseAddress = new Uri("http://muthur-api"));

var temporalHost = builder.Configuration.GetConnectionString("muthur-temporal-dev")
    ?? builder.Configuration["Temporal:Address"]
    ?? "localhost:7233";
var temporalNamespace = builder.Configuration["Temporal:Namespace"] ?? "default";

// Temporal client — singleton, shared by worker and any direct client usage.
builder.Services.AddTemporalClient(temporalHost, temporalNamespace);

// Temporal worker — uses the DI'd ITemporalClient above.
builder.Services
    .AddHostedTemporalWorker(AgentConstants.TaskQueue)
    .AddWorkflow<AgentWorkflow>()
    .AddWorkflow<DocumentIngestionWorkflow>()
    .AddScopedActivities<LlmActivities>()
    .AddScopedActivities<ToolActivities>()
    .AddScopedActivities<IngestionActivities>()
    .AddScopedActivities<NotificationActivities>();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Muthur.Bishop.Worker");
var lmConnectionString = builder.Configuration.GetConnectionString("muthur-lmstudio") ?? "(not set)";
logger.LogInformation("Bishop Worker starting — Temporal: {TargetHost}/{Namespace}, TaskQueue: {TaskQueue}, LMStudio: {LMStudio}",
    temporalHost, temporalNamespace, AgentConstants.TaskQueue, lmConnectionString);

await host.RunAsync();
