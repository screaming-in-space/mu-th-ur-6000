using Muthur.Contracts;

namespace Muthur.Data;

public interface IDocumentRepository
{
    Task<Guid> StoreDocumentAsync(string? title, string sourcePath, string content,
        int pageCount, Dictionary<string, string> metadata, CancellationToken ct = default);

    Task StoreChunksAsync(Guid documentId, IReadOnlyList<TextChunk> chunks,
        IReadOnlyList<float[]> embeddings, CancellationToken ct = default);

    Task<DocumentRecord?> GetDocumentAsync(Guid id, CancellationToken ct = default);

    Task<string?> GetDocumentContentAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<SimilarChunk>> SearchSimilarAsync(float[] queryEmbedding,
        int limit = 5, CancellationToken ct = default);
}
