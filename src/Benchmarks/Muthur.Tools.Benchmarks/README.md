# Muthur.Tools.Benchmarks

BenchmarkDotNet benchmarks for the PDF extraction pipeline in `Muthur.Tools`.

## Benchmarks

### TextAssemblyBenchmarks — Pooled Buffer vs StringBuilder

Isolates text assembly from PdfPig parsing. Feeds pre-generated page strings (realistic varying lengths) into StringBuilder vs ArrayPoolBufferWriter to measure the buffer optimization directly.

**Result:** ~2x throughput, ~67% allocation reduction at all page counts.

| Pages | StringBuilder | Pooled Buffer | Speed | Alloc |
|---|---|---|---|---|
| 10 | 3.73 us / 111 KB | 1.50 us / 36 KB | 2.5x faster | 68% less |
| 50 | 51.5 us / 558 KB | 33.9 us / 181 KB | 1.5x faster | 68% less |
| 200 | 269 us / 2,291 KB | 130 us / 755 KB | 2.1x faster | 67% less |

### BufferSizingBenchmarks — Pre-Sizing Heuristic

Measures ArrayPoolBufferWriter starting at 4096 (default) vs pre-sized at `pages * 2000`. Uses realistic varying page lengths to model real PDFs.

**Result:** Pre-sizing is ~2.4x faster by avoiding rent-copy-return growth cycles.

| Pages | Default (4096) | Pre-sized | Speed |
|---|---|---|---|
| 10 | 791 ns | 321 ns | 2.5x faster |
| 50 | 3,699 ns | 1,594 ns | 2.3x faster |
| 200 | 22,987 ns | 9,590 ns | 2.4x faster |

## Running

```bash
# All benchmarks
dotnet run -c Release --project src/Benchmarks/Muthur.Tools.Benchmarks

# Individual
dotnet run -c Release --project src/Benchmarks/Muthur.Tools.Benchmarks -- --filter *TextAssembly*
dotnet run -c Release --project src/Benchmarks/Muthur.Tools.Benchmarks -- --filter *BufferSizing*
```

> Uses `[ShortRunJob]` for fast iteration. Switch to `[SimpleJob]` for publication-quality numbers.
