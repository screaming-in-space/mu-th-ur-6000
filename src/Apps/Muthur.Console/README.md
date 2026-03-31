# Muthur.Console

Thin demo CLI host. Configures DI and delegates to `AgentRunner` from `Muthur.Utilities`.

## What it does

1. Registers `MuthurApiClient` + `AgentRunner` via DI
2. Builds an `AgentJobRequest` targeting the sample PDF
3. Calls `AgentRunner.RunAsync` — session creation, prompting, and polling happen inside
4. Prints the agent's response and lists stored documents

## Running

Managed by Aspire — starts automatically when you run the AppHost:

```
dotnet run --project src/Aspire/Muthur.AppHost
```

To process a different PDF, pass the absolute path as an argument:

```
dotnet run --project src/Apps/Muthur.Console -- "/path/to/document.pdf"
```

## Dependencies

- `Muthur.Utilities` — `AgentRunner` orchestration (transitively brings in `Muthur.Clients`)
- `Muthur.ServiceDefaults` — service discovery, logging, telemetry
- `Microsoft.Extensions.Hosting` — generic host builder
