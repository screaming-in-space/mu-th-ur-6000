using Microsoft.Extensions.Logging.Abstractions;
using Muthur.Contracts;
using Muthur.Data;
using Muthur.Tools.Handlers;
using NSubstitute;

namespace Muthur.Tools.Tests;

public class ToolRegistryTests
{
    private readonly ToolRegistry _sut;

    public ToolRegistryTests()
    {
        var repository = Substitute.For<IDocumentRepository>();
        var handler = new DocumentStoreHandler(
            NullLogger<DocumentStoreHandler>.Instance, repository);
        _sut = new ToolRegistry(NullLogger<ToolRegistry>.Instance, handler);
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
}
