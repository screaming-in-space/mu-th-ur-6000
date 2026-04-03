using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Muthur.Contracts;
using Muthur.Data;
using Muthur.Tools.Handlers;
using NSubstitute;

namespace Muthur.Tools.Tests;

public class DocumentStoreHandlerTests
{
    private readonly IDocumentRepository _repo;
    private readonly DocumentStoreHandler _sut;

    public DocumentStoreHandlerTests()
    {
        _repo = Substitute.For<IDocumentRepository>();
        _sut = new DocumentStoreHandler(NullLogger<DocumentStoreHandler>.Instance, _repo);
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

        var result = await _sut.StoreAsync(args);
        var parsed = JsonSerializer.Deserialize<StoreDocumentResult>(result, SerializerDefaults.CaseInsensitive);

        Assert.NotNull(parsed);
        Assert.Equal(expectedId, parsed.DocumentId);
    }

    [Fact]
    public async Task StoreAsync_MissingSourcePath_ThrowsArgument()
    {
        var args = JsonSerializer.Serialize(new
        {
            Title = "Test Doc",
            Text = "content"
        });

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.StoreAsync(args));
    }

    [Fact]
    public async Task StoreAsync_InvalidJson_ThrowsJsonException()
    {
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => _sut.StoreAsync("bad json"));
    }

    [Fact]
    public async Task StoreAsync_NullMetadata_PassesEmptyDictionary()
    {
        _repo.StoreDocumentAsync(
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        var args = JsonSerializer.Serialize(new
        {
            Title = "Test",
            SourcePath = "/file.pdf",
            Text = "text",
            PageCount = 1
        });

        await _sut.StoreAsync(args);

        await _repo.Received(1).StoreDocumentAsync(
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Is<Dictionary<string, string>>(d => d.Count == 0), Arg.Any<CancellationToken>());
    }
}
