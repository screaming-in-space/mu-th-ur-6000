using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using CommunityToolkit.HighPerformance.Buffers;

namespace Muthur.Tools.Benchmarks;

/// <summary>
/// Measures the cost of buffer growth vs pre-sizing.
/// Uses realistic varying page lengths to model real PDF extraction patterns.
///
/// Run with: dotnet run -c Release --project src/Benchmarks/Muthur.Tools.Benchmarks -- --filter *BufferSizing*
/// </summary>
[MemoryDiagnoser]
[RankColumn]
[ShortRunJob]
public class BufferSizingBenchmarks
{
    private const int AvgCharsPerPage = 2000;

    private string[] _pages = null!;

    [Params(10, 50, 200)]
    public int PageCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _pages = BenchmarkHelpers.GeneratePageTexts(PageCount, AvgCharsPerPage);
    }

    [Benchmark(Baseline = true, Description = "Default initial size (4096)")]
    public int DefaultSize()
    {
        using var buffer = new ArrayPoolBufferWriter<char>(4096);
        WritePages(buffer);
        return buffer.WrittenCount;
    }

    [Benchmark(Description = "Pre-sized (pages * 2000)")]
    public int PreSized()
    {
        using var buffer = new ArrayPoolBufferWriter<char>(PageCount * AvgCharsPerPage);
        WritePages(buffer);
        return buffer.WrittenCount;
    }

    private void WritePages(ArrayPoolBufferWriter<char> buffer)
    {
        for (var i = 0; i < _pages.Length; i++)
        {
            var page = _pages[i].AsSpan();
            var span = buffer.GetSpan(page.Length);
            page.CopyTo(span);
            buffer.Advance(page.Length);
        }
    }
}
