namespace Muthur.Contracts;

/// <summary>Full document record for API responses (without content body).</summary>
public sealed record DocumentRecord(
    Guid Id,
    string? Title,
    string SourcePath,
    int PageCount,
    Dictionary<string, string> Metadata,
    DateTimeOffset CreatedAt);

/// <summary>Lightweight summary for list endpoints.</summary>
public sealed record DocumentSummary(
    Guid Id,
    string? Title,
    string SourcePath,
    int PageCount,
    DateTimeOffset CreatedAt);

/// <summary>A chunk of document text without its embedding.</summary>
public sealed record DocumentChunkRecord(
    Guid Id,
    Guid DocumentId,
    int ChunkIndex,
    string ChunkText);

/// <summary>Vector search result — a chunk ranked by similarity.</summary>
public sealed record SimilarChunk(
    string ChunkText,
    Guid DocumentId,
    string? DocumentTitle,
    double Score);
