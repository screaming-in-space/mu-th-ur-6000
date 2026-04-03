namespace Muthur.Tools.Benchmarks;

/// <summary>
/// Shared utilities for benchmark setup.
/// </summary>
internal static class BenchmarkHelpers
{
    /// <summary>
    /// Walks up from the output directory to find the sample PDF in the repo root.
    /// </summary>
    public static string FindSamplePdf()
    {
        const string relativePath = "samples/research/A Memory OS for AI System.pdf";

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Sample PDF not found. Searched upward from '{AppContext.BaseDirectory}' for '{relativePath}'.");
    }

    /// <summary>
    /// Generates page texts with realistic length variance for synthetic benchmarks.
    /// Lengths follow a normal-ish distribution around <paramref name="avgCharsPerPage"/>.
    /// </summary>
    public static string[] GeneratePageTexts(int pageCount, int avgCharsPerPage = 2000, int seed = 42)
    {
        var rng = new Random(seed);
        var pages = new string[pageCount];

        for (var i = 0; i < pageCount; i++)
        {
            // Vary ±50% around the average to model real PDF page variance.
            var length = Math.Max(100, avgCharsPerPage + (int)((rng.NextDouble() - 0.5) * avgCharsPerPage));
            pages[i] = new string('x', length);
        }

        return pages;
    }
}
