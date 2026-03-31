# Muthur.Telemetry

Custom OpenTelemetry instrumentation for the Muthur agent.

## Traces

`MuthurTrace.Source` is a static `ActivitySource` named `"Muthur"`. Use `MuthurTrace.StartSpan()` to create spans — returns `null` with zero overhead when no listener is attached.

```csharp
using var span = MuthurTrace.StartSpan("process-document");
span?.WithTag("document.id", docId);
```

## Metrics

`MuthurMetrics.Meter` is a static `Meter` named `"Muthur"` with four instruments:

| Instrument | Type | Unit | Purpose |
|------------|------|------|---------|
| `muthur.agent.sessions` | Counter | session | Agent sessions started |
| `muthur.tool.executions` | Counter | execution | Tool calls dispatched |
| `muthur.documents.ingested` | Counter | document | Documents stored + vectorized |
| `muthur.llm.duration` | Histogram | seconds | LLM call duration |

## Activity Extensions

Null-safe fluent extensions on `Activity?`:
- `WithTag(key, value)` — span-local metadata
- `WithBaggage(key, value)` — propagates via W3C baggage header
- `RecordError(exception)` — sets error status + records exception
- `SetSuccess()` — sets OK status

## Registration

`AddMuthurTelemetry()` is called by `ServiceDefaults.ConfigureOpenTelemetry()`. It registers the custom ActivitySource and Meter, the M.E.AI telemetry source, and configures service resource metadata (name, version, commit SHA, environment).

## Dependencies

- `OpenTelemetry.Extensions.Hosting`
