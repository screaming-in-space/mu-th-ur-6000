using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muthur.Contracts;
using Muthur.ServiceDefaults;
using Muthur.Bishop.Worker.Activities;
using Muthur.Bishop.Worker.Workflows;
using Temporalio.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddAgentChatClient();

// Register tool registry as singleton — shared across activities.
builder.Services.AddSingleton<ToolRegistry>();

// Register Temporal worker with the agent workflow and activities.
builder.Services
    .AddHostedTemporalWorker(AgentConstants.TaskQueue)
    .AddWorkflow<AgentWorkflow>()
    .AddScopedActivities<LlmActivities>()
    .AddScopedActivities<ToolActivities>();

// Configure Temporal client connection.
builder.Services.AddTemporalClient(options =>
{
    options.TargetHost = builder.Configuration["Temporal:Address"] ?? "localhost:7233";
    options.Namespace = builder.Configuration["Temporal:Namespace"] ?? "default";
});

var host = builder.Build();
await host.RunAsync();
