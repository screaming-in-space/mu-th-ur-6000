namespace Muthur.Contracts;

/// <summary>Event types broadcast through the relay.</summary>
public enum RelayEventType
{
    /// <summary>Document text extracted and persisted to Postgres.</summary>
    DocumentStored = 100,

    /// <summary>Chunking, embedding, and vector storage complete. Search is available.</summary>
    IngestionCompleted = 101,

    /// <summary>Ingestion pipeline failed. Document is stored but not searchable.</summary>
    IngestionFailed = 110,

    /// <summary>The agent produced a final response (no more tool calls).</summary>
    AgentResponseReady = 200,
}

/// <summary>Event broadcast to connected clients via SignalR.</summary>
public sealed record RelayEvent(
    string AgentId,
    Guid MessageId,
    RelayEventType EventType,
    string Message,
    DateTimeOffset Timestamp,
    Dictionary<string, string>? Metadata = null);
