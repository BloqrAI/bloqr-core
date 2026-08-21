namespace Bloqr.Dashboard.Console.Services;

/// <summary>
/// Menu for viewing the Dashboard's structured JSON logs in human-readable form.
/// </summary>
public sealed class LogsMenuService : MenuServiceBase
{
    private readonly ILogEntryReader _logEntryReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogsMenuService"/> class.
    /// </summary>
    public LogsMenuService(
        IConsoleRenderer renderer,
        IConsolePrompter prompter,
        ILogEntryReader logEntryReader,
        ILogger<LogsMenuService> logger)
        : base(renderer, prompter, logger)
    {
        _logEntryReader = logEntryReader;
    }

    /// <inheritdoc />
    public override string Title => "Logs";

    /// <inheritdoc />
    protected override Dictionary<string, Func<Task>> GetMenuActions() => new()
    {
        ["Tail last 50 entries"] = () => ShowEntriesAsync(new LogEntryFilter { Tail = 50 }),
        ["Show errors and above"] = () => ShowEntriesAsync(new LogEntryFilter { MinimumLevel = "Error" }),
        ["Filter by application"] = FilterByApplicationAsync,
        ["Filter by time range"] = FilterByTimeRangeAsync,
        ["List log files"] = ListLogFilesAsync,
    };

    private async Task FilterByApplicationAsync()
    {
        var allEntries = await _logEntryReader.ReadAsync(new LogEntryFilter()).ConfigureAwait(false);
        var applications = allEntries
            .Select(e => e.Application)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (applications.Count == 0)
        {
            Renderer.WriteStyled("No log entries with an application tag were found.", TextStyle.Warning);
            return;
        }

        var application = Prompter.Select("Filter by application", applications);
        await ShowEntriesAsync(new LogEntryFilter { Application = application }).ConfigureAwait(false);
    }

    private async Task FilterByTimeRangeAsync()
    {
        var choice = Prompter.Select(
            "Time range",
            ["Last hour", "Last 24 hours", "Last 7 days", "Custom range"]);

        var now = DateTimeOffset.UtcNow;
        var filter = choice switch
        {
            "Last hour" => new LogEntryFilter { Since = now.AddHours(-1) },
            "Last 24 hours" => new LogEntryFilter { Since = now.AddDays(-1) },
            "Last 7 days" => new LogEntryFilter { Since = now.AddDays(-7) },
            _ => await PromptCustomRangeAsync().ConfigureAwait(false),
        };

        await ShowEntriesAsync(filter).ConfigureAwait(false);
    }

    private async Task<LogEntryFilter> PromptCustomRangeAsync()
    {
        var sinceText = await Prompter.PromptAsync(
            "Since (ISO 8601, e.g. 2026-08-10T00:00:00Z; leave blank for no lower bound)",
            defaultValue: string.Empty).ConfigureAwait(false);
        var untilText = await Prompter.PromptAsync(
            "Until (ISO 8601; leave blank for no upper bound)",
            defaultValue: string.Empty).ConfigureAwait(false);

        return new LogEntryFilter
        {
            Since = DateTimeOffset.TryParse(sinceText, out var since) ? since : null,
            Until = DateTimeOffset.TryParse(untilText, out var until) ? until : null,
        };
    }

    private async Task ShowEntriesAsync(LogEntryFilter filter)
    {
        var entries = await _logEntryReader.ReadAsync(filter).ConfigureAwait(false);

        if (entries.Count == 0)
        {
            Renderer.WriteLine("No matching log entries.");
            return;
        }

        var table = new ConsoleTable { Title = "Log Entries" };
        table.AddColumn("Timestamp");
        table.AddColumn("Level");
        table.AddColumn("Message");

        foreach (var entry in entries)
        {
            table.AddRow(entry.Timestamp.ToString("u"), entry.Level, entry.Message);
        }

        Renderer.RenderTable(table);
    }

    private Task ListLogFilesAsync()
    {
        var files = _logEntryReader.ListLogFiles();
        if (files.Count == 0)
        {
            Renderer.WriteLine("No log files found.");
            return Task.CompletedTask;
        }

        foreach (var file in files)
        {
            Renderer.WriteLine(file);
        }

        return Task.CompletedTask;
    }
}
