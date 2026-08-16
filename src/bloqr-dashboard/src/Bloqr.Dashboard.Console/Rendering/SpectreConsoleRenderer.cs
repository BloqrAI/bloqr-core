namespace Bloqr.Dashboard.Console.Rendering;

/// <summary>
/// <see cref="IConsoleRenderer"/> implementation backed by Spectre.Console. This is the only
/// class in the Dashboard permitted to reference <c>Spectre.Console</c> directly for rendering,
/// alongside <see cref="SpectreConsolePrompter"/> — everything else consumes the interface, so a
/// future .NET MAUI host can supply a different implementation without touching menu logic.
/// </summary>
public sealed class SpectreConsoleRenderer : IConsoleRenderer
{
    /// <inheritdoc />
    public void WriteLine(string text) => AnsiConsole.WriteLine(text);

    /// <inheritdoc />
    public void Write(string text) => AnsiConsole.Write(text);

    /// <inheritdoc />
    public void WriteLine() => AnsiConsole.WriteLine();

    /// <inheritdoc />
    public void WriteStyled(string text, TextStyle style)
    {
        AnsiConsole.Write(new Markup(Markup.Escape(text), ToSpectreStyle(style)));
        AnsiConsole.WriteLine();
    }

    /// <inheritdoc />
    public void RenderTable(ConsoleTable table)
    {
        var spectreTable = new Table();

        if (table.Title is { } title)
        {
            spectreTable.Title(Markup.Escape(title));
        }

        foreach (var column in table.Columns)
        {
            var tableColumn = new TableColumn(Markup.Escape(column.Header));
            tableColumn.Alignment = column.Alignment switch
            {
                TextAlignment.Center => Justify.Center,
                TextAlignment.Right => Justify.Right,
                _ => Justify.Left,
            };
            spectreTable.AddColumn(tableColumn);
        }

        foreach (var row in table.Rows)
        {
            spectreTable.AddRow(row.Cells.Select(Markup.Escape).ToArray());
        }

        AnsiConsole.Write(spectreTable);
    }

    /// <inheritdoc />
    public void RenderPanel(string content, string? title = null)
    {
        var panel = new Panel(Markup.Escape(content));
        if (title is not null)
        {
            panel.Header = new PanelHeader(Markup.Escape(title));
        }

        AnsiConsole.Write(panel);
    }

    /// <inheritdoc />
    public void RenderRule(string? title = null)
    {
        var rule = title is null ? new Rule() : new Rule(Markup.Escape(title));
        rule.Justification = Justify.Left;
        AnsiConsole.Write(rule);
    }

    /// <inheritdoc />
    public void Clear() => AnsiConsole.Clear();

    /// <inheritdoc />
    public Task<T> StatusAsync<T>(string status, Func<Task<T>> operation) =>
        AnsiConsole.Status().StartAsync(status, _ => operation());

    /// <inheritdoc />
    public Task StatusAsync(string status, Func<Task> operation) =>
        AnsiConsole.Status().StartAsync(status, _ => operation());

    /// <inheritdoc />
    public Task<T> ProgressAsync<T>(string description, Func<IProgress<double>, Task<T>> operation) =>
        AnsiConsole.Progress().StartAsync(async ctx =>
        {
            var task = ctx.AddTask(Markup.Escape(description), maxValue: 100);
            var progress = new Progress<double>(fraction => task.Value = Math.Clamp(fraction, 0, 1) * 100);
            var result = await operation(progress).ConfigureAwait(false);
            task.Value = 100;
            return result;
        });

    /// <inheritdoc />
    public Task<T> LiveProgressAsync<T>(Func<ILiveProgressContext, Task<T>> operation) =>
        AnsiConsole.Progress().StartAsync(ctx => operation(new SpectreLiveProgressContext(ctx)));

    private sealed class SpectreLiveProgressContext(ProgressContext context) : ILiveProgressContext
    {
        public ILiveProgressTask AddTask(string description, double maxValue = 100) =>
            new SpectreLiveProgressTask(context.AddTask(Markup.Escape(description), maxValue: maxValue));
    }

    private sealed class SpectreLiveProgressTask(ProgressTask task) : ILiveProgressTask
    {
        public string Description
        {
            get => task.Description;
            set => task.Description = Markup.Escape(value);
        }

        public double Value
        {
            get => task.Value;
            set => task.Value = Math.Clamp(value, 0, task.MaxValue);
        }

        public double MaxValue => task.MaxValue;

        public bool IsFinished => task.IsFinished;

        public void Increment(double amount) => task.Increment(amount);

        public void Complete()
        {
            task.Value = task.MaxValue;
            task.StopTask();
        }
    }

    private static Style ToSpectreStyle(TextStyle style)
    {
        var decoration = Decoration.None;
        if (style.Bold)
        {
            decoration |= Decoration.Bold;
        }

        if (style.Italic)
        {
            decoration |= Decoration.Italic;
        }

        if (style.Underline)
        {
            decoration |= Decoration.Underline;
        }

        var foreground = style.Foreground is { } color ? ToSpectreColor(color) : Color.Default;
        return new Style(foreground, decoration: decoration);
    }

    private static Color ToSpectreColor(DashboardConsoleColor color) => color switch
    {
        DashboardConsoleColor.Grey => Color.Grey,
        DashboardConsoleColor.Red => Color.Red,
        DashboardConsoleColor.Green => Color.Green,
        DashboardConsoleColor.Yellow => Color.Yellow,
        DashboardConsoleColor.Blue => Color.Blue,
        DashboardConsoleColor.Cyan => Color.Cyan1,
        DashboardConsoleColor.Magenta => Color.Magenta1,
        DashboardConsoleColor.White => Color.White,
        _ => Color.Default,
    };
}
