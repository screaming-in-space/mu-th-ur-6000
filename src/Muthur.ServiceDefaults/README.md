# Muthur.ServiceDefaults

Shared DI extensions for Aspire service defaults and the M.E.AI chat client pipeline.

## Extensions

### `AddServiceDefaults()`

Standard Aspire service defaults — OpenTelemetry tracing/metrics, health checks, HTTP resilience, service discovery.

### `AddAgentChatClient()`

Registers `IChatClient` in DI with provider detection and middleware:

1. Reads `AI:Provider`, `AI:Model`, `AI:ApiKey`, `AI:Endpoint` from configuration
2. Builds the inner client: Anthropic (via OpenAI-compatible endpoint) or OpenAI
3. Wraps with `ChatClientBuilder` pipeline: `.UseOpenTelemetry()` → `.UseLogging()` → `.Build()`
4. Registers as singleton `IChatClient`

The pipeline does **not** include `FunctionInvokingChatClient` — tool call dispatch is handled explicitly by the Temporal workflow for per-tool durability checkpoints.

## Dependencies

- `Microsoft.Extensions.AI` 9.7.0
- `Microsoft.Extensions.AI.OpenAI` 10.4.1
- `Microsoft.Extensions.ServiceDiscovery`
- `Microsoft.Extensions.Http.Resilience`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`
