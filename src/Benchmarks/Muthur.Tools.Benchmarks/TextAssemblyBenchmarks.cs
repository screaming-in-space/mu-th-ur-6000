using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using CommunityToolkit.HighPerformance.Buffers;

namespace Muthur.Tools.Benchmarks;

/// <summary>
/// Isolates text assembly performance from PdfPig parsing.
/// Feeds pre-generated page strings into StringBuilder vs ArrayPoolBufferWriter
/// to measure the buffer optimization directly.
///
/// Run with: dotnet run -c Release --project src/Benchmarks/Muthur.Tools.Benchmarks -- --filter *TextAssembly*
/// </summary>
[MemoryDiagnoser]
[RankColumn]
[ShortRunJob]
public class TextAssemblyBenchmarks
{
    private const int AvgCharsPerPage = 2000;

    private static ReadOnlySpan<char> PagePrefix => "--- Page ";
    private static ReadOnlySpan<char> PageSuffix => " ---";

    private string[] _pages = null!;

    [Params(10, 50, 200)]
    public int PageCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _pages = BenchmarkHelpers.GeneratePageTexts(PageCount, AvgCharsPerPage);
    }

    [Benchmark(Baseline = true, Description = "StringBuilder + interpolation")]
    public string StringBuilderAssembly()
    {
        var sb = new StringBuilder(PageCount * AvgCharsPerPage);

        for (var i = 0; i < _pages.Length; i++)
        {
            sb.AppendLine($"--- Page {i + 1} ---");
            sb.AppendLine(_pages[i]);
            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }

    [Benchmark(Description = "ArrayPoolBufferWriter + TryFormat")]
    public string PooledBufferAssembly()
    {
        using var buffer = new ArrayPoolBufferWriter<char>(Math.Max(4096, PageCount * AvgCharsPerPage));

        for (var i = 0; i < _pages.Length; i++)
        {
            AppendPageHeader(buffer, i + 1);
            AppendLine(buffer, _pages[i]);
            AppendNewLine(buffer);
        }

        return buffer.WrittenSpan.TrimEnd().ToString();
    }

    private static void AppendPageHeader(ArrayPoolBufferWriter<char> buffer, int pageNumber)
    {
        Append(buffer, PagePrefix);

        Span<char> numBuf = stackalloc char[10];
        if (pageNumber.TryFormat(numBuf, out var written))
        {
            Append(buffer, numBuf[..written]);
        }

        Append(buffer, PageSuffix);
        AppendNewLine(buffer);
    }

    private static void AppendLine(ArrayPoolBufferWriter<char> buffer, string value)
    {
        Append(buffer, value.AsSpan());
        AppendNewLine(buffer);
    }

    private static void Append(ArrayPoolBufferWriter<char> buffer, ReadOnlySpan<char> value)
    {
        var span = buffer.GetSpan(value.Length);
        value.CopyTo(span);
        buffer.Advance(value.Length);
    }

    private static void AppendNewLine(ArrayPoolBufferWriter<char> buffer)
    {
        Append(buffer, Environment.NewLine.AsSpan());
    }
}
