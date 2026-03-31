# Muthur.Data

Persistence layer. Owns Postgres with pgvector and Redis caching.

## What it provides

**`AddMuthurData(postgresName, redisName)`** - registers `NpgsqlDataSource` (with `UseVector()` for pgvector type mapping), `IDistributedCache` via Redis, `DocumentRepository`, `CachedDocumentRepository` decorator, and `MigrationService`.

## Schema

Two tables, created idempotently on startup by `MigrationService`:

- **`documents`** - id, title, source_path, content, page_count, metadata (JSONB), created_at
- **`document_chunks`** - id, document_id (FK), chunk_index, chunk_text, embedding `vector(768)`, created_at

HNSW index on the embedding column for fast approximate nearest neighbor search.

## Repository

`IDocumentRepository` with five operations:

| Method | Purpose |
|--------|---------|
| `StoreDocumentAsync` | INSERT document, return ID |
| `StoreChunksAsync` | Bulk INSERT chunks with embeddings (transactional) |
| `GetDocumentAsync` | Get metadata by ID |
| `GetDocumentContentAsync` | Get full text by ID |
| `ListDocumentsAsync` | List all document summaries |
| `SearchSimilarAsync` | pgvector cosine similarity search, returns ranked chunks |

## Caching

`CachedDocumentRepository` wraps `DocumentRepository` with `IDistributedCache`:
- Document metadata: cached by ID, 15-minute TTL
- Document list: cached as `docs:list`, invalidated on write
- Full content and vector search: not cached (too large / too dynamic)

## Dependencies

- `Aspire.Npgsql` 13.2.0 - NpgsqlDataSource via Aspire service discovery
- `Aspire.StackExchange.Redis.DistributedCaching` 13.2.0 - IDistributedCache
- `Dapper` 2.1.66 - SQL queries
- `Pgvector` 0.3.2 - vector type mapping (`UseVector()` on data source, `VectorTypeHandler` for Dapper)
