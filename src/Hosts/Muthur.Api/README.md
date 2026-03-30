# Muthur.Api

Minimal API host. Agent lifecycle + document access endpoints.

## Endpoints

### Agent

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/v1/agent/sessions` | Start a new agent workflow. Returns `agentId` and `workflowId`. |
| `POST` | `/v1/agent/sessions/{agentId}/prompt` | Send a prompt to a running agent via Temporal signal. |
| `GET` | `/v1/agent/sessions/{agentId}` | Query current agent state (processing, turn count, last response). |

### Documents

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/v1/documents` | List stored documents (Redis-cached). |
| `GET` | `/v1/documents/{id}` | Get document metadata by ID (Redis-cached). |
| `GET` | `/v1/documents/{id}/content` | Get full document text. |
| `GET` | `/v1/documents/search?q=...&limit=5` | Vector similarity search — generates query embedding, searches pgvector. |

## Design decisions

**No Worker dependency.** The API references `Muthur.Contracts` and `Muthur.Data`, never `Muthur.Bishop.Worker`. Workflow interactions use untyped Temporal handles.

**Shared data layer.** The API reads documents directly from Postgres/Redis via `IDocumentRepository` — it doesn't go through Temporal for reads.

**Embedding generation in the API.** The search endpoint generates the query embedding via `IEmbeddingGenerator` so it can call `SearchSimilarAsync` directly.

## Dependencies

- `Muthur.Contracts` — shared records
- `Muthur.Data` — document repository + caching
- `Muthur.ServiceDefaults` — Aspire defaults, Temporal client, M.E.AI pipeline
