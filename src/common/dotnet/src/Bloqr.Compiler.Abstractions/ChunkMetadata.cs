namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Metadata about a compilation chunk.
/// </summary>
public class ChunkMetadata
{
    /// <summary>
    /// Gets or sets the chunk index (0-based).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the total number of chunks.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets the estimated rule count for this chunk.
    /// </summary>
    public int EstimatedRules { get; set; }

    /// <summary>
    /// Gets or sets the actual rule count after compilation.
    /// </summary>
    public int? ActualRules { get; set; }

    /// <summary>
    /// Gets or sets the sources included in this chunk.
    /// </summary>
    public List<FilterSource> Sources { get; set; } = [];

    /// <summary>
    /// Gets or sets the compilation duration in milliseconds.
    /// </summary>
    public long? ElapsedMs { get; set; }

    /// <summary>
    /// Gets or sets whether this chunk compiled successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if compilation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the path to the chunk's output file.
    /// </summary>
    public string? OutputPath { get; set; }
}

/// <summary>
/// Result of chunked compilation.
/// </summary>
public class ChunkedCompilationResult
{
    /// <summary>
    /// Gets or sets whether all chunks compiled successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the total compilation time in milliseconds.
    /// </summary>
    public long TotalElapsedMs { get; set; }

    /// <summary>
    /// Gets or sets the metadata for each chunk.
    /// </summary>
    public List<ChunkMetadata> Chunks { get; set; } = [];

    /// <summary>
    /// Gets or sets the total rule count across all chunks.
    /// </summary>
    public int TotalRules { get; set; }

    /// <summary>
    /// Gets or sets the final rule count after deduplication.
    /// </summary>
    public int FinalRuleCount { get; set; }

    /// <summary>
    /// Gets or sets the number of duplicate rules removed.
    /// </summary>
    public int DuplicatesRemoved { get; set; }

    /// <summary>
    /// Gets or sets the merged output content.
    /// </summary>
    public string[]? MergedRules { get; set; }

    /// <summary>
    /// Gets or sets errors from failed chunks.
    /// </summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets the speedup ratio compared to sequential compilation.
    /// </summary>
    /// <remarks>
    /// This is an estimate based on chunk parallelism.
    /// </remarks>
    public double EstimatedSpeedup => Chunks.Count > 0
        ? (double)Chunks.Sum(c => c.ElapsedMs ?? 0) / TotalElapsedMs
        : 1.0;
}
