namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Event arguments for when a source has been loaded.
/// </summary>
public class SourceLoadedEventArgs : CompilationEventArgs
{
    /// <summary>
    /// Gets the source that was loaded.
    /// </summary>
    public FilterSource Source { get; }

    /// <summary>
    /// Gets the index of this source in the source list.
    /// </summary>
    public int SourceIndex { get; }

    /// <summary>
    /// Gets the total number of sources.
    /// </summary>
    public int TotalSources { get; }

    /// <summary>
    /// Gets whether the source was loaded successfully.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the error message if loading failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the size of the loaded content in bytes.
    /// </summary>
    public long ContentSizeBytes { get; }

    /// <summary>
    /// Gets the estimated number of rules in the source.
    /// </summary>
    public int EstimatedRuleCount { get; }

    /// <summary>
    /// Gets the duration of the load operation.
    /// </summary>
    public TimeSpan LoadDuration { get; }

    /// <summary>
    /// Gets the content hash for integrity verification (SHA-256).
    /// </summary>
    public string? ContentHash { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceLoadedEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    /// <param name="source">The source that was loaded.</param>
    /// <param name="sourceIndex">The index of this source.</param>
    /// <param name="totalSources">The total number of sources.</param>
    /// <param name="success">Whether loading was successful.</param>
    /// <param name="contentSizeBytes">The size of the content in bytes.</param>
    /// <param name="estimatedRuleCount">The estimated number of rules.</param>
    /// <param name="loadDuration">The duration of the load operation.</param>
    /// <param name="contentHash">The SHA-256 hash of the content.</param>
    /// <param name="errorMessage">The error message if loading failed.</param>
    public SourceLoadedEventArgs(
        CompilerOptions options,
        FilterSource source,
        int sourceIndex,
        int totalSources,
        bool success,
        long contentSizeBytes = 0,
        int estimatedRuleCount = 0,
        TimeSpan loadDuration = default,
        string? contentHash = null,
        string? errorMessage = null)
        : base(options)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        SourceIndex = sourceIndex;
        TotalSources = totalSources;
        Success = success;
        ContentSizeBytes = contentSizeBytes;
        EstimatedRuleCount = estimatedRuleCount;
        LoadDuration = loadDuration;
        ContentHash = contentHash;
        ErrorMessage = errorMessage;
    }
}
