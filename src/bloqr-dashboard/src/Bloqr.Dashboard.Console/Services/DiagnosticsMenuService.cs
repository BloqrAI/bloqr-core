using Bloqr.Compiler.Core.Helpers;

namespace Bloqr.Dashboard.Console.Services;

/// <summary>
/// Menu for self-diagnostics: external tool presence, resolved filesystem paths, and
/// configuration health — per the epic's requirement that the Dashboard "be able to
/// troubleshoot itself and report/log errors that are fixable by the user or runtime."
/// </summary>
public sealed class DiagnosticsMenuService : MenuServiceBase
{
    private readonly CommandHelper _commandHelper;
    private readonly IDashboardPaths _paths;
    private readonly IDashboardConfigurationStore _configStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsMenuService"/> class.
    /// </summary>
    public DiagnosticsMenuService(
        IConsoleRenderer renderer,
        IConsolePrompter prompter,
        CommandHelper commandHelper,
        IDashboardPaths paths,
        IDashboardConfigurationStore configStore,
        ILogger<DiagnosticsMenuService> logger)
        : base(renderer, prompter, logger)
    {
        _commandHelper = commandHelper;
        _paths = paths;
        _configStore = configStore;
    }

    /// <inheritdoc />
    public override string Title => "Diagnostics";

    /// <inheritdoc />
    protected override Dictionary<string, Func<Task>> GetMenuActions() => new()
    {
        ["Check external tools"] = CheckExternalToolsAsync,
        ["Show resolved paths"] = ShowResolvedPathsAsync,
        ["Check configuration health"] = CheckConfigurationHealthAsync,
    };

    private Task CheckExternalToolsAsync()
    {
        var table = new ConsoleTable { Title = "External Tools" };
        table.AddColumn("Tool");
        table.AddColumn("Status");
        table.AddColumn("Path");

        foreach (var tool in new[] { "dotnet", "deno", "cargo", "hostlist-compiler" })
        {
            var path = _commandHelper.FindCommand(tool);
            table.AddRow(tool, path is null ? "not found" : "found", path ?? "-");
        }

        Renderer.RenderTable(table);
        return Task.CompletedTask;
    }

    private Task ShowResolvedPathsAsync()
    {
        var table = new ConsoleTable { Title = "Resolved Paths" };
        table.AddColumn("Purpose");
        table.AddColumn("Path");
        table.AddRow("Configuration file", _paths.ConfigFilePath);
        table.AddRow("Backup directory", _paths.BackupDirectory);
        table.AddRow("Log directory", _paths.LogDirectory);
        table.AddRow("Pattern files directory", _paths.PatternFilesDirectory);

        Renderer.RenderTable(table);
        return Task.CompletedTask;
    }

    private async Task CheckConfigurationHealthAsync()
    {
        if (!File.Exists(_configStore.ConfigPath))
        {
            Renderer.WriteStyled("No configuration file exists yet; one will be created on first load.", TextStyle.Warning);
            return;
        }

        var load = await _configStore.LoadAsync(allowInteractiveRecovery: true).ConfigureAwait(false);

        if (load.WasRecovered)
        {
            Renderer.WriteStyled($"Configuration required recovery: {load.RecoveryDescription}", TextStyle.Warning);
        }
        else
        {
            Renderer.WriteStyled("Configuration file is present and schema-valid.", TextStyle.Success);
        }

        var backups = _configStore.ListBackups();
        Renderer.WriteLine($"Backups available: {backups.Count}");
    }
}
