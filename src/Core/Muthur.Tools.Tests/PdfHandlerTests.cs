using System.Text.Json;
using Muthur.Tools.Handlers;

namespace Muthur.Tools.Tests;

public class PdfHandlerTests
{
    [Fact]
    public async Task ExtractTextAsync_FileNotFound_Throws()
    {
        var args = JsonSerializer.Serialize(new { FilePath = "/nonexistent/path.pdf" });

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => PdfHandler.ExtractTextAsync(args));
    }

    [Fact]
    public async Task ExtractTextAsync_NullFilePath_ThrowsArgument()
    {
        var args = JsonSerializer.Serialize(new { FilePath = (string?)null });

        await Assert.ThrowsAsync<ArgumentException>(
            () => PdfHandler.ExtractTextAsync(args));
    }

    [Fact]
    public async Task ExtractTextAsync_InvalidJson_ThrowsJsonException()
    {
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => PdfHandler.ExtractTextAsync("not json"));
    }

    [Fact]
    public async Task ExtractTextAsync_EmptyFilePath_ThrowsArgument()
    {
        var args = JsonSerializer.Serialize(new { FilePath = "" });

        await Assert.ThrowsAsync<ArgumentException>(
            () => PdfHandler.ExtractTextAsync(args));
    }
}
