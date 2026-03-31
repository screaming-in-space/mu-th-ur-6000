# MU-TH-UR 6000

Durable AI agent built on Temporal, M.E.AI, and .NET Aspire.
Companion repo for the threadunsafe.dev article. Built by one person.

## Documentation Map

| File | Owns | Read when |
|------|------|-----------|
| `docs/RULES.md` | Technical constraints, best practices for Temporal/M.E.AI/PdfPig/pgvector/Redis, rejected patterns | You're about to write or modify code |
| `docs/STRUCTURE.md` | Project architecture, file map, dependency graph, data flow | You need to find something or add a new file |

## Quick Start

```
dotnet run --project src/Aspire/Muthur.AppHost
```

Requires Docker Desktop (auto-launched if not running). Aspire starts Temporal, Postgres (pgvector), and Redis containers, waits for health checks, then starts the Worker, API, and Console.

## Development Environment

- Windows, CRLF line endings
- .NET 10 / C# 14.0
- Docker Desktop for container resources (Temporal, Postgres, Redis)
- `nuget.config` clears global sources - only `api.nuget.org`
