using Muthur.Contracts;
using Muthur.Tools.Handlers;

namespace Muthur.Tools.Tests;

public class PdfHandlerTests
{
    private readonly PdfHandler _handler = new();

    [Fact]
    public async Task ExtractPdfTextAsync_FileNotFound_Throws()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _handler.ExtractPdfTextAsync("/nonexistent/path.pdf", CancellationToken.None));
    }

    [Fact]
    public async Task ExtractPdfTextAsync_EmptyFilePath_ThrowsArgument()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.ExtractPdfTextAsync("", CancellationToken.None));
    }

    [Fact]
    public void Register_ReturnsValidRegistration()
    {
        var registration = _handler.Register();

        Assert.Equal(AgentConstants.ToolPdfExtractText, registration.Name);
        Assert.NotNull(registration.Function);
    }
}
