namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Event arguments for when a source is about to be loaded.
/// </summary>
public class SourceLoadingEventArgs : CompilationEventArgs
{
    /// <summary>
    /// Gets the source being loaded.
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
    /// Gets or sets a value indicating whether this source is a local file.
    /// </summary>
    public bool IsLocalFile { get; }

    /// <summary>
    /// Gets or sets a value indicating whether to skip this source.
    /// </summary>
    public bool Skip { get; set; }

    /// <summary>
    /// Gets or sets the reason for skipping.
    /// </summary>
    public string? SkipReason { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceLoadingEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    /// <param name="source">The source being loaded.</param>
    /// <param name="sourceIndex">The index of this source.</param>
    /// <param name="totalSources">The total number of sources.</param>
    public SourceLoadingEventArgs(
        CompilerOptions options,
        FilterSource source,
        int sourceIndex,
        int totalSources)
        : base(options)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        SourceIndex = sourceIndex;
        TotalSources = totalSources;
        IsLocalFile = !source.Source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                      !source.Source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
