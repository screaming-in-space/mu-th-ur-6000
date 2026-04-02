namespace Muthur.Contracts;

/// <summary>Event types broadcast through the relay.</summary>
public enum RelayEventType
{
    IngestionStarted = 100,
    IngestionCompleted = 101,
    IngestionFailed = 110,
}

/// <summary>Event broadcast to connected clients via SignalR.</summary>
public sealed record RelayEvent(
    string AgentId,
    Guid MessageId,
    RelayEventType EventType,
    string Message,
    DateTimeOffset Timestamp,
    Dictionary<string, string>? Metadata = null);
