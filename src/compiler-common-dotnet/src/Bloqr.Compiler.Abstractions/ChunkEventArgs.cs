namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Event arguments for when a chunk is about to be processed.
/// </summary>
public class ChunkStartedEventArgs : CompilationEventArgs
{
    /// <summary>
    /// Gets the chunk metadata.
    /// </summary>
    public ChunkMetadata Chunk { get; }

    /// <summary>
    /// Gets the sources in this chunk.
    /// </summary>
    public IReadOnlyList<FilterSource> Sources { get; }

    /// <summary>
    /// Gets or sets a value indicating whether to skip this chunk.
    /// </summary>
    public bool Skip { get; set; }

    /// <summary>
    /// Gets or sets the reason for skipping.
    /// </summary>
    public string? SkipReason { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkStartedEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    /// <param name="chunk">The chunk metadata.</param>
    /// <param name="sources">The sources in this chunk.</param>
    public ChunkStartedEventArgs(
        CompilerOptions options,
        ChunkMetadata chunk,
        IReadOnlyList<FilterSource> sources)
        : base(options)
    {
        Chunk = chunk ?? throw new ArgumentNullException(nameof(chunk));
        Sources = sources ?? throw new ArgumentNullException(nameof(sources));
    }
}

/// <summary>
/// Event arguments for when a chunk has completed processing.
/// </summary>
public class ChunkCompletedEventArgs : CompilationEventArgs
{
    /// <summary>
    /// Gets the chunk metadata with updated results.
    /// </summary>
    public ChunkMetadata Chunk { get; }

    /// <summary>
    /// Gets whether the chunk was processed successfully.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the number of rules in the chunk result.
    /// </summary>
    public int RuleCount { get; }

    /// <summary>
    /// Gets the processing duration.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkCompletedEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    /// <param name="chunk">The chunk metadata.</param>
    /// <param name="success">Whether processing was successful.</param>
    /// <param name="ruleCount">The number of rules.</param>
    /// <param name="duration">The processing duration.</param>
    /// <param name="errorMessage">The error message if failed.</param>
    public ChunkCompletedEventArgs(
        CompilerOptions options,
        ChunkMetadata chunk,
        bool success,
        int ruleCount = 0,
        TimeSpan duration = default,
        string? errorMessage = null)
        : base(options)
    {
        Chunk = chunk ?? throw new ArgumentNullException(nameof(chunk));
        Success = success;
        RuleCount = ruleCount;
        Duration = duration;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// Event arguments for when chunks are being merged.
/// </summary>
public class ChunksMergingEventArgs : CompilationEventArgs
{
    /// <summary>
    /// Gets the number of chunks to merge.
    /// </summary>
    public int ChunkCount { get; }

    /// <summary>
    /// Gets the total rules before deduplication.
    /// </summary>
    public int TotalRulesBeforeMerge { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunksMergingEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    /// <param name="chunkCount">The number of chunks.</param>
    /// <param name="totalRulesBeforeMerge">Total rules before merging.</param>
    public ChunksMergingEventArgs(
        CompilerOptions options,
        int chunkCount,
        int totalRulesBeforeMerge)
        : base(options)
    {
        ChunkCount = chunkCount;
        TotalRulesBeforeMerge = totalRulesBeforeMerge;
    }
}

/// <summary>
/// Event arguments for when chunks have been merged.
/// </summary>
public class ChunksMergedEventArgs : CompilationEventArgs
{
    /// <summary>
    /// Gets the number of chunks that were merged.
    /// </summary>
    public int ChunkCount { get; }

    /// <summary>
    /// Gets the total rules before deduplication.
    /// </summary>
    public int TotalRulesBeforeMerge { get; }

    /// <summary>
    /// Gets the final rule count after deduplication.
    /// </summary>
    public int FinalRuleCount { get; }

    /// <summary>
    /// Gets the number of duplicates removed.
    /// </summary>
    public int DuplicatesRemoved { get; }

    /// <summary>
    /// Gets the merge duration.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunksMergedEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    /// <param name="chunkCount">The number of chunks.</param>
    /// <param name="totalRulesBeforeMerge">Total rules before merging.</param>
    /// <param name="finalRuleCount">Final rule count after deduplication.</param>
    /// <param name="duplicatesRemoved">Number of duplicates removed.</param>
    /// <param name="duration">The merge duration.</param>
    public ChunksMergedEventArgs(
        CompilerOptions options,
        int chunkCount,
        int totalRulesBeforeMerge,
        int finalRuleCount,
        int duplicatesRemoved,
        TimeSpan duration)
        : base(options)
    {
        ChunkCount = chunkCount;
        TotalRulesBeforeMerge = totalRulesBeforeMerge;
        FinalRuleCount = finalRuleCount;
        DuplicatesRemoved = duplicatesRemoved;
        Duration = duration;
    }
}
