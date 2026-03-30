# Copilot Instructions — MU-TH-UR 6000
<!-- last reviewed: 2026-03-30 -->

## Identity

MU-TH-UR 6000 is a durable AI agent built on .NET 10, Temporal, M.E.AI, and .NET Aspire.
It extracts PDFs, stores documents with pgvector embeddings, and runs semantic search.
Companion repo for the threadunsafe.dev article. One developer. Paint-by-numbers architecture.

## Philosophy

- Build on Microsoft.Extensions.AI abstractions (`IChatClient`, `IEmbeddingGenerator`, `AIFunctionFactory`) — not framework-level orchestration unless the project needs it.
- Temporal is the durability layer. Every LLM call and tool call is an activity checkpoint.
- PostgreSQL + pgvector is truth. Redis is the fast path. Dapper for queries, not EF Core.
- Aspire orchestrates local dev. Docker Desktop required for container resources.
- Tools are isolated from workflows. The Worker owns orchestration; `Muthur.Tools` owns handlers.
- Explicit over clever. Raw SQL over ORMs. Manual dispatch over magic middleware.

## Rule Sources

All coding rules, conventions, best practices, and rejected patterns live in docs:

- **[docs/RULES.md](../docs/RULES.md)** — Technical constraints, Temporal/M.E.AI/pgvector/Redis best practices, priority order for AI abstractions, naming conventions.
- **[docs/STRUCTURE.md](../docs/STRUCTURE.md)** — Project architecture, file map, dependency graph, data flow diagrams, Aspire resource table.

## Documentation Ownership

- **Project structure** lives exclusively in `docs/STRUCTURE.md`. Don't duplicate it elsewhere.
- **Best practices and constraints** live exclusively in `docs/RULES.md`.
- **Project READMEs** — Every project under `src/` has a `README.md` describing its purpose, key types, and dependencies. Read the project's `README.md` first when working on it.
- **`CLAUDE.md`** at the repo root is the entry point for AI agents. It links to the docs above.
