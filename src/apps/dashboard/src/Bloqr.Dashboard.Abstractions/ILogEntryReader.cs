namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Reads and filters structured JSON log entries from the Dashboard's rolling log files, for
/// display in the log-viewer menu.
/// </summary>
public interface ILogEntryReader
{
    /// <summary>
    /// Reads log entries matching the given filter across all known log files, most recent last.
    /// </summary>
    /// <param name="filter">The filter criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching log entries.</returns>
    Task<IReadOnlyList<LogEntry>> ReadAsync(
        LogEntryFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the known log file paths, oldest first.
    /// </summary>
    /// <returns>The log file paths.</returns>
    IReadOnlyList<string> ListLogFiles();
}
