using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muthur.Contracts;
using Muthur.Data;
using Muthur.ServiceDefaults;
using Muthur.Tools;
using Muthur.Tools.Handlers;
using Muthur.Bishop.Worker.Activities;
using Muthur.Bishop.Worker.Workflows;
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

// Temporal worker - agent workflow + ingestion child workflow + all activities.
builder.Services
    .AddHostedTemporalWorker(AgentConstants.TaskQueue)
    .AddWorkflow<AgentWorkflow>()
    .AddWorkflow<DocumentIngestionWorkflow>()
    .AddScopedActivities<LlmActivities>()
    .AddScopedActivities<ToolActivities>()
    .AddScopedActivities<IngestionActivities>();

builder.Services.AddTemporalClient(options =>
{
    options.TargetHost = builder.Configuration.GetConnectionString("muthur-temporal-dev")
        ?? builder.Configuration["Temporal:Address"]
        ?? "localhost:7233";
    options.Namespace = builder.Configuration["Temporal:Namespace"] ?? "default";
});

var host = builder.Build();
await host.RunAsync();
