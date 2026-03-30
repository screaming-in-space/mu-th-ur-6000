using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Muthur.Contracts;

namespace Muthur.Data;

/// <summary>
/// Decorator over <see cref="IDocumentRepository"/> that caches reads in Redis.
/// Writes pass through to the inner repository and invalidate the cache.
/// </summary>
public sealed class CachedDocumentRepository(
    DocumentRepository inner,
    IDistributedCache cache) : IDocumentRepository
{
    private static readonly DistributedCacheEntryOptions DefaultTtl = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
    };

    public async Task<Guid> StoreDocumentAsync(string? title, string sourcePath, string content,
        int pageCount, Dictionary<string, string> metadata, CancellationToken ct = default)
    {
        var id = await inner.StoreDocumentAsync(title, sourcePath, content, pageCount, metadata, ct);
        await cache.RemoveAsync("docs:list", ct);
        return id;
    }

    public Task StoreChunksAsync(Guid documentId, IReadOnlyList<TextChunk> chunks,
        IReadOnlyList<float[]> embeddings, CancellationToken ct = default)
        => inner.StoreChunksAsync(documentId, chunks, embeddings, ct);

    public async Task<DocumentRecord?> GetDocumentAsync(Guid id, CancellationToken ct = default)
    {
        var key = $"docs:{id}";
        var cached = await cache.GetStringAsync(key, ct);
        if (cached is not null)
            return JsonSerializer.Deserialize<DocumentRecord>(cached);

        var doc = await inner.GetDocumentAsync(id, ct);
        if (doc is not null)
            await cache.SetStringAsync(key, JsonSerializer.Serialize(doc), DefaultTtl, ct);

        return doc;
    }

    public Task<string?> GetDocumentContentAsync(Guid id, CancellationToken ct = default)
        => inner.GetDocumentContentAsync(id, ct);

    public async Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default)
    {
        const string key = "docs:list";
        var cached = await cache.GetStringAsync(key, ct);
        if (cached is not null)
            return JsonSerializer.Deserialize<List<DocumentSummary>>(cached) ?? [];

        var docs = await inner.ListDocumentsAsync(ct);
        await cache.SetStringAsync(key, JsonSerializer.Serialize(docs), DefaultTtl, ct);
        return docs;
    }

    public Task<IReadOnlyList<SimilarChunk>> SearchSimilarAsync(float[] queryEmbedding,
        int limit = 5, CancellationToken ct = default)
        => inner.SearchSimilarAsync(queryEmbedding, limit, ct);
}
