namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Service for chunking and parallel compilation of filter rules.
/// </summary>
public interface IChunkingService
{
    /// <summary>
    /// Determines if chunking should be enabled for the given configuration.
    /// </summary>
    /// <param name="configuration">The compiler configuration.</param>
    /// <param name="options">The chunking options.</param>
    /// <returns>True if chunking should be enabled.</returns>
    bool ShouldEnableChunking(CompilerConfiguration configuration, ChunkingOptions? options);

    /// <summary>
    /// Splits a configuration into chunks for parallel compilation.
    /// </summary>
    /// <param name="configuration">The original configuration.</param>
    /// <param name="options">The chunking options.</param>
    /// <returns>A list of chunked configurations with metadata.</returns>
    List<(CompilerConfiguration Config, ChunkMetadata Metadata)> SplitIntoChunks(
        CompilerConfiguration configuration,
        ChunkingOptions options);

    /// <summary>
    /// Compiles chunks in parallel.
    /// </summary>
    /// <param name="chunks">The chunks to compile.</param>
    /// <param name="options">The compiler options to use.</param>
    /// <param name="chunkingOptions">The chunking options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The chunked compilation result.</returns>
    Task<ChunkedCompilationResult> CompileChunksAsync(
        List<(CompilerConfiguration Config, ChunkMetadata Metadata)> chunks,
        CompilerOptions options,
        ChunkingOptions chunkingOptions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges compiled rules from multiple chunks.
    /// </summary>
    /// <param name="chunkResults">The rules from each chunk.</param>
    /// <returns>Merged and deduplicated rules.</returns>
    (string[] Rules, int DuplicatesRemoved) MergeChunks(List<string[]> chunkResults);

    /// <summary>
    /// Estimates the time savings from chunked compilation.
    /// </summary>
    /// <param name="totalRules">Estimated total rule count.</param>
    /// <param name="options">The chunking options.</param>
    /// <returns>Estimated speedup ratio (1.0 = no improvement).</returns>
    double EstimateSpeedup(int totalRules, ChunkingOptions options);
}
