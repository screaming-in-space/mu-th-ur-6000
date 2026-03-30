# Muthur.AppHost

Aspire orchestration host. The single entry point for local dev.

## What it does

1. Calls `EnsureDockerAsync()` - checks if Docker is running, launches Docker Desktop if not, polls until ready
2. Adds three container resources:
   - **Temporal** (`temporalio/admin-tools`) - durable execution engine
   - **Postgres** (`pgvector/pgvector:pg17`) - document storage with vector search
   - **Redis** - distributed cache
3. Adds the API and Worker projects, wiring both to `.WithReference().WaitFor()` all infrastructure
4. Starts everything via `builder.Build().RunAsync()`

## Running

```bash
dotnet run --project src/Muthur.AppHost
```

Opens the Aspire dashboard at `http://localhost:15137`.

## Aspire resources

| Resource | Image | Ports | Persistent |
|----------|-------|-------|-----------|
| `muthur-temporal-dev` | `temporalio/admin-tools:latest` | gRPC 7233, UI 8233 | Yes |
| `muthur-postgres` / `muthur-db` | `pgvector/pgvector:pg17` | 5432 | Yes |
| `muthur-cache` | Redis | 6379 | Yes |

## Dependencies

- `Aspire.Hosting.AppHost` 13.2.0 (declared as SDK, not PackageReference)
- `Aspire.Hosting.PostgreSQL` 13.2.0
- `Aspire.Hosting.Redis` 13.2.0
- `Aspire.Hosting.Temporal` (local project - `IsAspireProjectResource="false"`)
- Project references to `Muthur.Api` and `Muthur.Bishop.Worker`

## Environment

`ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` is set in `launchSettings.json` for HTTP-only local dev.
