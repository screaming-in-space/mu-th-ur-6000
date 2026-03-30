# Muthur.AppHost

Aspire orchestration host. The single entry point for local dev.

## What it does

1. Calls `EnsureDockerAsync()` — checks if Docker is running, launches Docker Desktop if not, polls until ready
2. Adds the Temporal dev server as a container resource (`temporalio/admin-tools`)
3. Adds the API and Worker projects, wiring the Worker to `.WaitFor(temporal)`
4. Starts everything via `builder.Build().RunAsync()`

## Running

```bash
dotnet run --project src/Muthur.AppHost
```

Opens the Aspire dashboard at `http://localhost:15137`.

## Dependencies

- `Aspire.Hosting.AppHost` 13.2.0 (declared as SDK, not PackageReference)
- `Aspire.Hosting.Temporal` (local project — `IsAspireProjectResource="false"`)
- Project references to `Muthur.Api` and `Muthur.Bishop.Worker`

## Environment

`ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` is set in `launchSettings.json` for HTTP-only local dev.
