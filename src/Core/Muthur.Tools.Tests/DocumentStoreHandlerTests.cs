using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Muthur.Contracts;
using Muthur.Data;
using Muthur.Tools.Documents;
using Muthur.Tools.Handlers;
using NSubstitute;

namespace Muthur.Tools.Tests;

/// <summary>
/// Tests for <see cref="DocumentStoreHandler"/> — now invoked through AIFunction.
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
    public async Task StoreDocumentAsync_ValidArgs_CallsRepositoryAndReturnsId()
    {
        var expectedId = Guid.NewGuid();
        _repo.StoreDocumentAsync(
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(expectedId);

        var result = await _sut.StoreDocumentAsync(
            "Test Doc", "/path/to/doc.pdf", 3, "Some content", null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedId, result.DocumentId);
    }

    [Fact]
    public void Register_ReturnsValidRegistration()
    {
        var registration = _sut.Register();

        Assert.Equal(AgentConstants.ToolStoreDocument, registration.Name);
        Assert.NotNull(registration.Function);
    }

    [Fact]
    public async Task StoreDocumentAsync_ViaAIFunction_ReturnsResult()
    {
        var expectedId = Guid.NewGuid();
        _repo.StoreDocumentAsync(
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(expectedId);

        var registration = _sut.Register();
        var aiArgs = new AIFunctionArguments
        {
            ["title"] = "Test Doc",
            ["sourcePath"] = "/path/to/doc.pdf",
            ["pageCount"] = 3,
            ["text"] = "Some content"
        };

        var result = await registration.Function.InvokeAsync(aiArgs, CancellationToken.None);

        Assert.NotNull(result);
        var json = JsonSerializer.Serialize(result);
        var parsed = JsonSerializer.Deserialize<StoreDocumentResult>(json, SerializerDefaults.CaseInsensitive);
        Assert.Equal(expectedId, parsed?.DocumentId);
    }
}
