using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Muthur.Contracts;
using Muthur.Data;
using Muthur.Tools.Documents;
using Muthur.Tools.Handlers;
using NSubstitute;

namespace Muthur.Tools.Tests;

public class ToolRegistryTests
{
    private readonly ToolRegistry _sut;

    public ToolRegistryTests()
    {
        var repository = Substitute.For<IDocumentRepository>();
        var store = new DocumentStore(NullLogger<DocumentStore>.Instance, repository);
        var documentStoreHandler = new DocumentStoreHandler(store);
        var pdfHandler = new PdfHandler();

        IToolHandler[] handlers = [pdfHandler, documentStoreHandler];
        _sut = new ToolRegistry(NullLogger<ToolRegistry>.Instance, handlers);
    }

    [Fact]
    public void GetTools_ReturnsExpectedTools()
    {
        var tools = _sut.GetTools();

        Assert.Equal(2, tools.Count);
    }

    [Fact]
    public void GetFunction_KnownTool_ReturnsFunction()
    {
        var function = _sut.GetFunction(AgentConstants.ToolPdfExtractText);

        Assert.NotNull(function);
    }

    [Fact]
    public void GetFunction_UnknownTool_ReturnsNull()
    {
        var function = _sut.GetFunction("nonexistent_tool");

        Assert.Null(function);
    }

    [Theory]
    [InlineData(AgentConstants.ToolPdfExtractText)]
    [InlineData(AgentConstants.ToolStoreDocument)]
    public void GetFunction_AllRegisteredTools_HaveFunctions(string toolName)
    {
        Assert.NotNull(_sut.GetFunction(toolName));
    }

    [Fact]
    public async Task ExecuteAsync_UnknownTool_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ExecuteAsync("nonexistent_tool", new Dictionary<string, object?>(), CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_ValidTool_ReturnsResult()
    {
        var repo = Substitute.For<IDocumentRepository>();
        var expectedId = Guid.NewGuid();
        repo.StoreDocumentAsync(
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(expectedId);

        var store = new DocumentStore(NullLogger<DocumentStore>.Instance, repo);
        var handler = new DocumentStoreHandler(store);
        IToolHandler[] handlers = [handler];
        var registry = new ToolRegistry(NullLogger<ToolRegistry>.Instance, handlers);

        var args = new Dictionary<string, object?>
        {
            ["title"] = "Test",
            ["sourcePath"] = "/file.pdf",
            ["text"] = "content",
            ["pageCount"] = 1
        };

        var result = await registry.ExecuteAsync(AgentConstants.ToolStoreDocument, args, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Json);
        Assert.NotNull(result.TypedPayload);
    }
}
