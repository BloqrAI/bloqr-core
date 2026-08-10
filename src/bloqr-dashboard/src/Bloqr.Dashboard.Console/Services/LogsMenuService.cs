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
        ["List log files"] = ListLogFilesAsync,
    };

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
