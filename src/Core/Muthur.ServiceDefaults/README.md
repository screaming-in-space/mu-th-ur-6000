# Muthur.ServiceDefaults

Shared DI extensions for Aspire service defaults and the M.E.AI pipeline.

## Extensions

### `AddServiceDefaults()`

Standard Aspire service defaults — Serilog structured logging, OpenTelemetry tracing/metrics, health checks, HTTP resilience, service discovery.

### `AddAgentChatClient()`

Registers `IChatClient` in DI with provider detection and middleware:

1. Reads `AI:Provider`, `AI:Model`, `AI:ApiKey`, `AI:Endpoint` from configuration
2. Builds the inner client: Anthropic (via OpenAI-compatible endpoint) or OpenAI
3. Wraps with `ChatClientBuilder` pipeline: `.UseOpenTelemetry()` → `.UseLogging()` → `.Build()`
4. Registers as singleton `IChatClient`

The pipeline does **not** include `FunctionInvokingChatClient` — tool call dispatch is handled explicitly by the Temporal workflow for per-tool durability checkpoints.

### `AddAgentEmbeddingGenerator()`

Registers `IEmbeddingGenerator<string, Embedding<float>>` for vector embedding generation:

1. Reads `AI:ApiKey`, `AI:Endpoint`, `AI:EmbeddingModel` from configuration
2. Defaults to OpenAI `text-embedding-3-small` (1536 dimensions)
3. Used by `IngestionActivities` for document chunk embeddings and by the API for search query embeddings

## Dependencies

- `Muthur.Logging` — Serilog structured logging
- `Microsoft.Extensions.AI` 9.7.0
- `Microsoft.Extensions.AI.OpenAI` 10.4.1
- `Microsoft.Extensions.ServiceDiscovery`
- `Microsoft.Extensions.Http.Resilience`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`
