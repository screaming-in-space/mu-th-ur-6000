# Aspire.Hosting.Temporal

Aspire extension that runs Temporal as a managed container resource.

## What it provides

**`AddTemporalDevServer(name)`** — Adds a `temporalio/admin-tools` container that runs `temporal server start-dev` with embedded SQLite storage. Exposes gRPC (7233) and UI (8233) endpoints. Health-checked via the UI endpoint. Uses `ContainerLifetime.Persistent` so the container survives AppHost restarts.

**`EnsureDockerAsync()`** — Extension on `IDistributedApplicationBuilder` that checks Docker availability and auto-launches Docker Desktop (Windows, macOS, Linux) if not running. Polls for up to 60 seconds.

## Files

| File | Purpose |
|------|---------|
| `TemporalResource.cs` | `ContainerResource` + `IResourceWithConnectionString` — exposes `host:port` connection string |
| `TemporalResourceBuilderExtensions.cs` | `AddTemporalDevServer()` — image, args, endpoints, health check |
| `DockerDesktopExtensions.cs` | `EnsureDockerAsync()` — cross-platform Docker Desktop launcher |

## Usage

```csharp
var builder = DistributedApplication.CreateBuilder(args);
await builder.EnsureDockerAsync();
var temporal = builder.AddTemporalDevServer("temporal");

builder.AddProject<Projects.MyWorker>("worker")
    .WithReference(temporal)
    .WaitFor(temporal);
```

The Worker reads the connection string from `ConnectionStrings:{resource-name}` automatically.
