using Microsoft.Extensions.Logging.Abstractions;
using Muthur.Contracts;
using Muthur.Data;
using Muthur.Integration.Tests.Infrastructure;

namespace Muthur.Integration.Tests.Repos;

[Collection("Muthur")]
public sealed class DocumentRepoTests(MuthurFixture platform)
{
    private DocumentRepository CreateRepo() =>
        new(NullLogger<DocumentRepository>.Instance, platform.DataSource);

    [Fact]
    public async Task StoreDocument_ReturnsGuid()
    {
        var repo = CreateRepo();
        var id = await repo.StoreDocumentAsync(
            "Test Doc", "/test/path.pdf", "Hello world", 1, []);

        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task GetDocument_AfterStore_ReturnsRecord()
    {
        var repo = CreateRepo();
        var id = await repo.StoreDocumentAsync(
            "Roundtrip Test", "/test/roundtrip.pdf", "Content here", 3,
            new Dictionary<string, string> { ["author"] = "test" });

        var doc = await repo.GetDocumentAsync(id);

        Assert.NotNull(doc);
        Assert.Equal("Roundtrip Test", doc.Title);
        Assert.Equal("/test/roundtrip.pdf", doc.SourcePath);
        Assert.Equal(3, doc.PageCount);
        Assert.Equal("test", doc.Metadata["author"]);
    }

    [Fact]
    public async Task GetDocument_UnknownId_ReturnsNull()
    {
        var repo = CreateRepo();
        var doc = await repo.GetDocumentAsync(Guid.NewGuid());

        Assert.Null(doc);
    }

    [Fact]
    public async Task GetDocumentContent_AfterStore_ReturnsText()
    {
        var repo = CreateRepo();
        var id = await repo.StoreDocumentAsync(
            "Content Test", "/test/content.pdf", "The quick brown fox", 1, []);

        var content = await repo.GetDocumentContentAsync(id);

        Assert.Equal("The quick brown fox", content);
    }

    [Fact]
    public async Task ListDocuments_AfterStore_IncludesDocument()
    {
        var repo = CreateRepo();
        var uniqueTitle = $"List Test {Guid.NewGuid():N}";
        await repo.StoreDocumentAsync(uniqueTitle, "/test/list.pdf", "content", 1, []);

        var docs = await repo.ListDocumentsAsync();

        Assert.Contains(docs, d => d.Title == uniqueTitle);
    }

    [Fact]
    public async Task StoreChunks_ThenSearch_ReturnsSimilarChunks()
    {
        var repo = CreateRepo();
        var id = await repo.StoreDocumentAsync(
            "Vector Test", "/test/vector.pdf", "embeddings content", 1, []);

        // Create a deterministic embedding (768 dims, first dim = 1.0).
        var embedding = new float[768];
        embedding[0] = 1.0f;

        var chunks = new List<TextChunk>
        {
            new(0, "Chunk about neural networks"),
            new(1, "Chunk about vector databases"),
        };

        var embeddings = new List<float[]> { embedding, embedding };

        await repo.StoreChunksAsync(id, chunks, embeddings);

        // Search with the same embedding - should find our chunks.
        var results = await repo.SearchSimilarAsync(embedding, limit: 2);

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Score > 0));
    }
}
