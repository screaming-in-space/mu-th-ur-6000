namespace Muthur.Contracts;

/// <summary>Full document record for API responses (without content body).</summary>
public sealed record DocumentRecord(
    Guid Id,
    string? Title,
    string SourcePath,
    int PageCount,
    Dictionary<string, string> Metadata,
    DateTime CreatedAt);

/// <summary>Lightweight summary for list endpoints.</summary>
public sealed record DocumentSummary(
    Guid Id,
    string? Title,
    string SourcePath,
    int PageCount,
    DateTime CreatedAt);

/// <summary>Vector search result - a chunk ranked by similarity.</summary>
public sealed record SimilarChunk(
    string ChunkText,
    Guid DocumentId,
    string? DocumentTitle,
    double Score);

/// <summary>Result from the store_document tool handler.</summary>
public sealed record StoreDocumentResult(Guid? DocumentId);
