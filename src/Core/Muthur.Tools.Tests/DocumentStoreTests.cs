using Microsoft.Extensions.Logging.Abstractions;
using Muthur.Data;
using Muthur.Tools.Documents;
using NSubstitute;

namespace Muthur.Tools.Tests;

/// <summary>
/// Tests for <see cref="DocumentStore"/> — the pure domain logic, no JSON plumbing.
/// </summary>
public class DocumentStoreTests
{
    private readonly IDocumentRepository _repo;
    private readonly DocumentStore _sut;

    public DocumentStoreTests()
    {
        _repo = Substitute.For<IDocumentRepository>();
        _sut = new DocumentStore(NullLogger<DocumentStore>.Instance, _repo);
    }

    [Fact]
    public async Task StoreAsync_ValidArgs_ReturnsId()
    {
        var expectedId = Guid.NewGuid();
        _repo.StoreDocumentAsync(
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(expectedId);

        var args = new StoreDocumentJob("Test Doc", "/path/to/doc.pdf", "Some content", 3);

        var id = await _sut.StoreAsync(args);

        Assert.Equal(expectedId, id);
    }

    [Fact]
    public async Task StoreAsync_MissingSourcePath_ThrowsArgument()
    {
        var args = new StoreDocumentJob("Title", null, "text");

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.StoreAsync(args));
    }

    [Fact]
    public async Task StoreAsync_EmptySourcePath_ThrowsArgument()
    {
        var args = new StoreDocumentJob("Title", "  ", "text");

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.StoreAsync(args));
    }

    [Fact]
    public async Task StoreAsync_NullText_PassesEmptyString()
    {
        _repo.StoreDocumentAsync(
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        var args = new StoreDocumentJob("Title", "/file.pdf", null);

        await _sut.StoreAsync(args);

        await _repo.Received(1).StoreDocumentAsync(
            Arg.Any<string?>(), Arg.Any<string>(), "",
            Arg.Any<int>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoreAsync_NullMetadata_PassesEmptyDictionary()
    {
        _repo.StoreDocumentAsync(
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        var args = new StoreDocumentJob("Title", "/file.pdf", "text", 1, null);

        await _sut.StoreAsync(args);

        await _repo.Received(1).StoreDocumentAsync(
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Is<Dictionary<string, string>>(d => d.Count == 0), Arg.Any<CancellationToken>());
    }
}
