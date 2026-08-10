namespace Bloqr.Dashboard.Core.Logging;

/// <summary>
/// Reads and filters structured JSON log entries (matching <c>schemas/log-entry.schema.json</c>)
/// from the Dashboard's rolling log files, for display in the log-viewer menu.
/// </summary>
public sealed class LogEntryReader : ILogEntryReader
{
    private static readonly string[] LevelOrder =
        ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

    private readonly IDashboardPaths _paths;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogEntryReader"/> class.
    /// </summary>
    /// <param name="paths">Resolves the Dashboard's log directory.</param>
    public LogEntryReader(IDashboardPaths paths)
    {
        _paths = paths;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListLogFiles()
    {
        if (!Directory.Exists(_paths.LogDirectory))
        {
            return [];
        }

        return Directory.GetFiles(_paths.LogDirectory, "bloqr-dashboard-*.jsonl")
            .OrderBy(File.GetLastWriteTimeUtc)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LogEntry>> ReadAsync(
        LogEntryFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var entries = new List<LogEntry>();

        foreach (var path in ListLogFiles())
        {
            await foreach (var line in File.ReadLinesAsync(path, cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                LogEntry? entry;
                try
                {
                    entry = ParseLine(line);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (entry is not null && Matches(entry, filter))
                {
                    entries.Add(entry);
                }
            }
        }

        entries.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        if (filter.Tail is { } tail && entries.Count > tail)
        {
            entries = entries.Skip(entries.Count - tail).ToList();
        }

        return entries;
    }

    private static LogEntry ParseLine(string line)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;

        return new LogEntry
        {
            Timestamp = root.TryGetProperty("timestamp", out var timestamp)
                ? timestamp.GetDateTimeOffset()
                : default,
            Level = root.TryGetProperty("level", out var level) ? level.GetString() ?? "Information" : "Information",
            Message = root.TryGetProperty("message", out var message) ? message.GetString() ?? string.Empty : string.Empty,
            Exception = root.TryGetProperty("exception", out var exception) ? exception.GetString() : null,
            SourceContext = root.TryGetProperty("sourceContext", out var sourceContext)
                ? sourceContext.GetString()
                : null,
            Application = root.TryGetProperty("application", out var application) ? application.GetString() : null,
        };
    }

    private static bool Matches(LogEntry entry, LogEntryFilter filter)
    {
        if (filter.Since is { } since && entry.Timestamp < since)
        {
            return false;
        }

        if (filter.Until is { } until && entry.Timestamp > until)
        {
            return false;
        }

        if (filter.Application is { } application &&
            !string.Equals(entry.Application, application, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.MinimumLevel is { } minimumLevel && !MeetsMinimumLevel(entry.Level, minimumLevel))
        {
            return false;
        }

        return true;
    }

    private static bool MeetsMinimumLevel(string level, string minimumLevel)
    {
        var levelIndex = Array.IndexOf(LevelOrder, level);
        var minimumIndex = Array.IndexOf(LevelOrder, minimumLevel);

        if (levelIndex < 0 || minimumIndex < 0)
        {
            return true;
        }

        return levelIndex >= minimumIndex;
    }
}
