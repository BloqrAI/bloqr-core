namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Filter criteria for querying log entries via <see cref="ILogEntryReader"/>.
/// </summary>
public sealed class LogEntryFilter
{
    /// <summary>
    /// Gets or sets the minimum log level to include (e.g. only "Warning" and above), or
    /// <c>null</c> for no minimum.
    /// </summary>
    public string? MinimumLevel { get; set; }

    /// <summary>
    /// Gets or sets the earliest timestamp (inclusive) to include, or <c>null</c> for no lower bound.
    /// </summary>
    public DateTimeOffset? Since { get; set; }

    /// <summary>
    /// Gets or sets the latest timestamp (inclusive) to include, or <c>null</c> for no upper bound.
    /// </summary>
    public DateTimeOffset? Until { get; set; }

    /// <summary>
    /// Gets or sets the source application name to filter by (e.g. "bloqr-dashboard"), or
    /// <c>null</c> for all applications.
    /// </summary>
    public string? Application { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of most-recent entries to return, or <c>null</c> for no limit.
    /// </summary>
    public int? Tail { get; set; }
}
