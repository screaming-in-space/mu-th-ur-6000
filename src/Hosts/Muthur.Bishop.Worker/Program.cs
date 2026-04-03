using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muthur.Agents;
using Muthur.Bishop.Worker.Activities;
using Muthur.Bishop.Worker.Workflows;
using Muthur.Contracts;
using Muthur.Data;
using Muthur.ServiceDefaults;
using Muthur.Tools;
using Temporalio.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<HostOptions>(options =>
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

builder.AddServiceDefaults();
builder.AddMuthurData("muthur-db", "muthur-cache", runMigrations: false);
builder.AddMuthurTemporalClient();
builder.AddAgentChatClient();
builder.AddAgentEmbeddingGenerator();
builder.AddMuthurTools();

// Named HTTP client for relay notifications — resolves via Aspire service discovery.
// Named (not typed) because AddScopedActivities owns the registration of NotificationActivities.
builder.Services.AddHttpClient(NotificationActivities.HttpClientName, c =>
    c.BaseAddress = new Uri("http://muthur-api"));

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

await host.RunAsync();
