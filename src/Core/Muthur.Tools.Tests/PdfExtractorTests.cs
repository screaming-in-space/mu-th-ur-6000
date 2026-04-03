using Muthur.Tools.Pdf;

namespace Muthur.Tools.Tests;

/// <summary>
/// Tests for <see cref="PdfExtractor"/> — the pure extraction logic, no JSON plumbing.
/// </summary>
public class PdfExtractorTests
{
    [Fact]
    public void Extract_FileNotFound_Throws()
    {
        Assert.Throws<FileNotFoundException>(
            () => PdfExtractor.Extract("/nonexistent/path.pdf"));
    }

    [Fact]
    public void Extract_NullFilePath_ThrowsArgument()
    {
        Assert.Throws<ArgumentException>(
            () => PdfExtractor.Extract(null!));
    }

    [Fact]
    public void Extract_EmptyFilePath_ThrowsArgument()
    {
        Assert.Throws<ArgumentException>(
            () => PdfExtractor.Extract(""));
    }

    [Fact]
    public void Extract_WhitespaceFilePath_ThrowsArgument()
    {
        Assert.Throws<ArgumentException>(
            () => PdfExtractor.Extract("   "));
    }
}
