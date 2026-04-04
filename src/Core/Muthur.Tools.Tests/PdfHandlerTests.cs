using System.Text.Json;
using Muthur.Tools.Handlers;

namespace Muthur.Tools.Tests;

public class PdfHandlerTests
{
    [Fact]
    public async Task ExtractTextAsync_FileNotFound_Throws()
    {
        var args = JsonSerializer.Serialize(new { FilePath = "/nonexistent/path.pdf" });
        var pdfHandler = new PdfHandler();

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => pdfHandler.ExtractTextAsync(args, new ToolExecutionContext("test")));
    }

    [Fact]
    public async Task ExtractTextAsync_NullFilePath_ThrowsArgument()
    {
        var args = JsonSerializer.Serialize(new { FilePath = (string?)null });
        var pdfHandler = new PdfHandler();

        await Assert.ThrowsAsync<ArgumentException>(
            () => pdfHandler.ExtractTextAsync(args, new ToolExecutionContext("test")));
    }

    [Fact]
    public async Task ExtractTextAsync_InvalidJson_ThrowsJsonException()
    {
        var pdfHandler = new PdfHandler();
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => pdfHandler.ExtractTextAsync("not json", new ToolExecutionContext("test")));
    }

    [Fact]
    public async Task ExtractTextAsync_EmptyFilePath_ThrowsArgument()
    {
        var args = JsonSerializer.Serialize(new { FilePath = "" });
        var pdfHandler = new PdfHandler();

        await Assert.ThrowsAsync<ArgumentException>(
            () => pdfHandler.ExtractTextAsync(args, new ToolExecutionContext("test")));
    }
}
