namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// A single parsed structured log entry, matching one line of the JSON log files described by
/// <c>schemas/log-entry.schema.json</c>.
/// </summary>
public sealed class LogEntry
{
    /// <summary>
    /// Gets or sets the UTC timestamp the log event was emitted.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the log severity level (e.g. "Information", "Warning", "Error").
    /// </summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rendered, human-readable log message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full exception details, when present.
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// Gets or sets the logger's source context (typically a fully-qualified type name).
    /// </summary>
    public string? SourceContext { get; set; }

    /// <summary>
    /// Gets or sets the name of the application/process that emitted this entry.
    /// </summary>
    public string? Application { get; set; }
}
