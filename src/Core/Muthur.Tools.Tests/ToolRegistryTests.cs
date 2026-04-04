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
    public void GetHandler_KnownTool_ReturnsHandler()
    {
        var handler = _sut.GetHandler(AgentConstants.ToolExtractPdf);

        Assert.NotNull(handler);
    }

    [Fact]
    public void GetHandler_UnknownTool_ReturnsNull()
    {
        var handler = _sut.GetHandler("nonexistent_tool");

        Assert.Null(handler);
    }

    [Theory]
    [InlineData(AgentConstants.ToolExtractPdf)]
    [InlineData(AgentConstants.ToolStoreDocument)]
    public void GetHandler_AllRegisteredTools_HaveHandlers(string toolName)
    {
        Assert.NotNull(_sut.GetHandler(toolName));
    }

    [Fact]
    public async Task ExecuteAsync_UnknownTool_Throws()
    {
        var context = new ToolExecutionContext("nonexistent_tool");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ExecuteAsync("nonexistent_tool", "{}", context));
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

        var args = System.Text.Json.JsonSerializer.Serialize(new
        {
            Title = "Test",
            SourcePath = "/file.pdf",
            Text = "content",
            PageCount = 1
        });

        var context = new ToolExecutionContext(AgentConstants.ToolStoreDocument);
        var result = await registry.ExecuteAsync(AgentConstants.ToolStoreDocument, args, context);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Json);
        Assert.NotNull(result.TypedPayload);
    }
}
