# MU-TH-UR 6000

Durable AI agent built on Temporal, M.E.AI, and .NET Aspire.
Companion repo for the threadunsafe.dev article. Built by one person.

## Documentation Map

| File | Owns | Read when |
|------|------|-----------|
| `docs/RULES.md` | Technical constraints, Temporal/M.E.AI/PdfPig best practices, rejected patterns | You're about to write or modify code |
| `docs/STRUCTURE.md` | Project architecture, file map, dependency graph, data flow | You need to find something or add a new file |

## Quick Start

```
dotnet run --project src/Muthur.AppHost
```

Requires Docker Desktop (auto-launched if not running). Aspire starts the Temporal container, waits for health check, then starts the Worker and API.

## Development Environment

- Windows, CRLF line endings
- .NET 10 / C# 14.0
- Docker Desktop for Temporal container
- `nuget.config` clears global sources — only `api.nuget.org`
