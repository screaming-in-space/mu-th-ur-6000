using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Muthur.Contracts;
using Muthur.Data;
using Muthur.Tools.Documents;
using Muthur.Tools.Handlers;
using NSubstitute;

namespace Muthur.Tools.Tests;

/// <summary>
/// Tests for <see cref="DocumentStoreHandler"/> — the JSON bridge layer.
/// </summary>
public class DocumentStoreHandlerTests
{
    private readonly IDocumentRepository _repo;
    private readonly DocumentStoreHandler _sut;

    public DocumentStoreHandlerTests()
    {
        _repo = Substitute.For<IDocumentRepository>();
        var store = new DocumentStore(NullLogger<DocumentStore>.Instance, _repo);
        _sut = new DocumentStoreHandler(store);
    }

    [Fact]
    public async Task StoreAsync_ValidArgs_CallsRepositoryAndReturnsId()
    {
        var expectedId = Guid.NewGuid();
        _repo.StoreDocumentAsync(
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(expectedId);

        var args = JsonSerializer.Serialize(new
        {
            Title = "Test Doc",
            SourcePath = "/path/to/doc.pdf",
            Text = "Some content",
            PageCount = 3
        });

        var result = await _sut.StoreAsync(args, new ToolExecutionContext("test"));
        var parsed = JsonSerializer.Deserialize<StoreDocumentResult>(result.Json, SerializerDefaults.CaseInsensitive);

        Assert.NotNull(parsed);
        Assert.Equal(expectedId, parsed.DocumentId);
    }

    [Fact]
    public async Task StoreAsync_InvalidJson_ThrowsJsonException()
    {
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => _sut.StoreAsync("bad json", new ToolExecutionContext("test")));
    }
}
