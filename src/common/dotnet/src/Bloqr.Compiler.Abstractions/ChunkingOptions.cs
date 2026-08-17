namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Configuration options for chunked parallel compilation.
/// </summary>
/// <remarks>
/// Chunking enables parallel compilation of large filter lists by splitting
/// sources into multiple chunks and compiling them concurrently.
/// </remarks>
public class ChunkingOptions
{
    /// <summary>
    /// Gets or sets whether chunking is enabled.
    /// </summary>
    /// <remarks>
    /// When false, compilation runs sequentially on all sources.
    /// When true, sources are split into chunks for parallel processing.
    /// </remarks>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of rules per chunk.
    /// </summary>
    /// <remarks>
    /// This is an estimate used for balancing chunks. Default is 100,000 rules.
    /// </remarks>
    public int ChunkSize { get; set; } = 100_000;

    /// <summary>
    /// Gets or sets the maximum number of parallel compilation workers.
    /// </summary>
    /// <remarks>
    /// Default is the number of processor cores. Higher values may improve
    /// performance but increase memory usage.
    /// </remarks>
    public int MaxParallel { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets the chunking strategy.
    /// </summary>
    /// <remarks>
    /// Currently only 'source' strategy is supported, which distributes
    /// sources evenly across chunks.
    /// </remarks>
    public ChunkingStrategy Strategy { get; set; } = ChunkingStrategy.Source;

    /// <summary>
    /// Gets default chunking options with chunking disabled.
    /// </summary>
    public static ChunkingOptions Default => new()
    {
        Enabled = false,
        ChunkSize = 100_000,
        MaxParallel = Environment.ProcessorCount,
        Strategy = ChunkingStrategy.Source
    };

    /// <summary>
    /// Gets chunking options optimized for large filter lists.
    /// </summary>
    public static ChunkingOptions ForLargeLists => new()
    {
        Enabled = true,
        ChunkSize = 100_000,
        MaxParallel = Math.Max(2, Environment.ProcessorCount),
        Strategy = ChunkingStrategy.Source
    };
}

/// <summary>
/// Strategy for splitting sources into chunks.
/// </summary>
public enum ChunkingStrategy
{
    /// <summary>
    /// Distribute sources evenly across chunks.
    /// </summary>
    Source,

    /// <summary>
    /// Balance chunks by estimated line count.
    /// </summary>
    /// <remarks>
    /// This strategy attempts to create chunks with similar rule counts.
    /// Not yet implemented.
    /// </remarks>
    LineCount
}
