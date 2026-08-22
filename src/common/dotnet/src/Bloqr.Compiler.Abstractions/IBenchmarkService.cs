namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Benchmarks real compilation performance (chunked vs unchunked) against the canned
/// <c>benchmarks/data/</c> datasets, through the same <see cref="IBloqrCompilerService"/>/
/// <see cref="IChunkingService"/> pipeline the <c>compile</c> command uses - not a synthetic
/// simulation. Shared by the <c>Bloqr.Compiler.Dotnet.Console</c> <c>--benchmark</c> CLI mode
/// and the Dashboard's Diagnostics menu (#423), so both surfaces exercise identical logic.
/// </summary>
public interface IBenchmarkService
{
    /// <summary>The canned dataset sizes this service knows how to benchmark.</summary>
    IReadOnlyList<string> BenchmarkSizes { get; }

    /// <summary>
    /// Locates the repo's <c>benchmarks/data</c> directory by walking up from the current
    /// directory, or <c>null</c> if none is found (e.g. a binary-only release checkout).
    /// </summary>
    string? FindBenchmarkDataDir();

    /// <summary>
    /// Benchmarks one canned dataset size, or all of them if <paramref name="size"/> is
    /// <c>"all"</c>.
    /// </summary>
    /// <param name="size">A value from <see cref="BenchmarkSizes"/>, or <c>"all"</c>.</param>
    /// <param name="dataDir">
    /// Directory containing the canned datasets. Falls back to <see cref="FindBenchmarkDataDir"/>
    /// when <c>null</c>.
    /// </param>
    /// <param name="numSources">Number of identical duplicated sources to compile per size.</param>
    /// <param name="maxParallel">Maximum parallel chunk workers for the chunked run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One result per benchmarked size.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="size"/> is not a recognized size or <c>"all"</c>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// No data directory was given or could be found.
    /// </exception>
    Task<List<BenchmarkRunResult>> RunBenchmarkAsync(
        string size,
        string? dataDir,
        int numSources,
        int maxParallel,
        CancellationToken cancellationToken = default);
}
