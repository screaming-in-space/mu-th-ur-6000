using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Muthur.Contracts;

namespace Muthur.Data;

/// <summary>
/// Decorator over <see cref="IDocumentRepository"/> that caches reads in Redis.
/// Writes pass through to the inner repository and invalidate the cache.
/// Cache operations are best-effort — failures are logged but never block.
/// </summary>
public sealed class CachedDocumentRepository(
    ILogger<CachedDocumentRepository> logger,
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
        var id = await inner.StoreDocumentAsync(title, sourcePath, content, pageCount, metadata, ct)
            .ConfigureAwait(false);

        try
        {
            await cache.RemoveAsync("docs:list", ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache invalidation failed for key {Key}", "docs:list");
        }

        return id;
    }

    public Task StoreChunksAsync(Guid documentId, IReadOnlyList<TextChunk> chunks,
        IReadOnlyList<float[]> embeddings, CancellationToken ct = default)
        => inner.StoreChunksAsync(documentId, chunks, embeddings, ct);

    public async Task<DocumentRecord?> GetDocumentAsync(Guid id, CancellationToken ct = default)
    {
        var key = $"docs:{id}";

        try
        {
            var cached = await cache.GetStringAsync(key, ct).ConfigureAwait(false);
            if (cached is not null)
            {
                logger.LogDebug("Cache hit for {Key}", key);
                return JsonSerializer.Deserialize<DocumentRecord>(cached);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache read failed for key {Key}", key);
        }

        var doc = await inner.GetDocumentAsync(id, ct).ConfigureAwait(false);

        if (doc is not null)
        {
            try
            {
                await cache.SetStringAsync(key, JsonSerializer.Serialize(doc), DefaultTtl, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cache write failed for key {Key}", key);
            }
        }

        return doc;
    }

    public Task<string?> GetDocumentContentAsync(Guid id, CancellationToken ct = default)
        => inner.GetDocumentContentAsync(id, ct);

    public async Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default)
    {
        const string key = "docs:list";

        try
        {
            var cached = await cache.GetStringAsync(key, ct).ConfigureAwait(false);
            if (cached is not null)
            {
                logger.LogDebug("Cache hit for {Key}", key);
                return JsonSerializer.Deserialize<List<DocumentSummary>>(cached) ?? [];
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache read failed for key {Key}", key);
        }

        var docs = await inner.ListDocumentsAsync(ct).ConfigureAwait(false);

        try
        {
            await cache.SetStringAsync(key, JsonSerializer.Serialize(docs), DefaultTtl, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache write failed for key {Key}", key);
        }

        return docs;
    }

    public Task<IReadOnlyList<SimilarChunk>> SearchSimilarAsync(float[] queryEmbedding,
        int limit = 5, CancellationToken ct = default)
        => inner.SearchSimilarAsync(queryEmbedding, limit, ct);
}
