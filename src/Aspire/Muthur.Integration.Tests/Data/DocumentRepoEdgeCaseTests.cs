using Dapper;
using Muthur.Contracts;
using Muthur.Data;
using Muthur.Integration.Tests.Infrastructure;

namespace Muthur.Integration.Tests.Data;

/// <summary>
/// Edge cases and additional coverage for DocumentRepository
/// that complement the happy-path tests in DocumentRepoTests.
/// </summary>
[Collection("Muthur")]
public sealed class DocumentRepoEdgeCaseTests(MuthurFixture platform)
{
    private DocumentRepository CreateRepo() => new(platform.DataSource);

    [Fact]
    public async Task StoreDocument_NullTitle_Succeeds()
    {
        var repo = CreateRepo();

        var id = await repo.StoreDocumentAsync(
            null, "/test/no-title.pdf", "content", 1, []);

        Assert.NotEqual(Guid.Empty, id);

        var doc = await repo.GetDocumentAsync(id);
        Assert.NotNull(doc);
        Assert.Null(doc.Title);
    }

    [Fact]
    public async Task StoreDocument_EmptyMetadata_ReturnsEmptyDict()
    {
        var repo = CreateRepo();

        var id = await repo.StoreDocumentAsync(
            "Empty Meta", "/test/empty-meta.pdf", "content", 0, []);

        var doc = await repo.GetDocumentAsync(id);
        Assert.NotNull(doc);
        Assert.Empty(doc.Metadata);
    }

    [Fact]
    public async Task StoreDocument_LargeMetadata_RoundTrips()
    {
        var repo = CreateRepo();

        var metadata = new Dictionary<string, string>
        {
            ["author"] = "Test Author",
            ["subject"] = "Integration Testing",
            ["creator"] = "MU-TH-UR 6000",
            ["keywords"] = "test, integration, pgvector, temporal"
        };

        var id = await repo.StoreDocumentAsync(
            "Meta Test", "/test/meta.pdf", "content", 2, metadata);

        var doc = await repo.GetDocumentAsync(id);
        Assert.NotNull(doc);
        Assert.Equal(4, doc.Metadata.Count);
        Assert.Equal("Test Author", doc.Metadata["author"]);
        Assert.Equal("Integration Testing", doc.Metadata["subject"]);
    }

    [Fact]
    public async Task GetDocumentContent_UnknownId_ReturnsNull()
    {
        var repo = CreateRepo();

        var content = await repo.GetDocumentContentAsync(Guid.NewGuid());

        Assert.Null(content);
    }

    [Fact]
    public async Task ListDocuments_ReturnsDescendingCreatedAt()
    {
        var repo = CreateRepo();

        var tag = Guid.NewGuid().ToString("N")[..8];
        var id1 = await repo.StoreDocumentAsync($"Order-1-{tag}", "/test/order1.pdf", "first", 1, []);

        // Small delay to ensure distinct timestamps.
        await Task.Delay(50);
        var id2 = await repo.StoreDocumentAsync($"Order-2-{tag}", "/test/order2.pdf", "second", 1, []);

        var docs = await repo.ListDocumentsAsync();
        var tagged = docs.Where(d => d.Title?.Contains(tag) == true).ToList();

        Assert.Equal(2, tagged.Count);

        // Most recent first.
        Assert.Equal(id2, tagged[0].Id);
        Assert.Equal(id1, tagged[1].Id);
    }

    [Fact]
    public async Task StoreChunks_EmptyList_DoesNotThrow()
    {
        var repo = CreateRepo();

        var docId = await repo.StoreDocumentAsync(
            "Empty Chunks", "/test/empty-chunks.pdf", "content", 1, []);

        // Storing zero chunks should succeed (no-op).
        await repo.StoreChunksAsync(docId, [], []);

        // Verify no chunks were created.
        await using var conn = await platform.DataSource.OpenConnectionAsync();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM document_chunks WHERE document_id = @Id",
            new { Id = docId });

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task StoreDocument_LargeContent_Succeeds()
    {
        var repo = CreateRepo();

        // ~100 KB of text — typical for a multi-page PDF extraction.
        var largeContent = string.Join("\n", Enumerable.Range(0, 1000)
            .Select(i => $"Line {i}: This is paragraph content for testing large document storage."));

        var id = await repo.StoreDocumentAsync(
            "Large Doc", "/test/large.pdf", largeContent, 50, []);

        var content = await repo.GetDocumentContentAsync(id);
        Assert.Equal(largeContent, content);
    }

    [Fact]
    public async Task StoreDocument_ZeroPageCount_Succeeds()
    {
        var repo = CreateRepo();

        var id = await repo.StoreDocumentAsync(
            "Zero Pages", "/test/zero-pages.pdf", "content", 0, []);

        var doc = await repo.GetDocumentAsync(id);
        Assert.NotNull(doc);
        Assert.Equal(0, doc.PageCount);
    }
}
