namespace Bloqr.Dashboard.Console.Services;

/// <summary>
/// Menu for viewing, validating, and recovering the Dashboard's own configuration file. The full
/// compiler-config generation wizard lives in <see cref="CompilerConfigWizardMenuService"/> (#268);
/// round-trip editing/versioning of an already-generated config is #269's separate scope.
/// </summary>
public sealed class ConfigMenuService : MenuServiceBase
{
    private readonly IDashboardConfigurationStore _configStore;
    private readonly IProfileManager _profileManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigMenuService"/> class.
    /// </summary>
    public ConfigMenuService(
        IConsoleRenderer renderer,
        IConsolePrompter prompter,
        IDashboardConfigurationStore configStore,
        IProfileManager profileManager,
        ILogger<ConfigMenuService> logger)
        : base(renderer, prompter, logger)
    {
        _configStore = configStore;
        _profileManager = profileManager;
    }

    /// <inheritdoc />
    public override string Title => "Configuration";

    /// <inheritdoc />
    protected override Dictionary<string, Func<Task>> GetMenuActions() => new()
    {
        ["Show current configuration"] = ShowConfigurationAsync,
        ["Validate configuration"] = ValidateConfigurationAsync,
        ["List backups"] = ListBackupsAsync,
        ["Restore from a backup"] = RestoreFromBackupAsync,
    };

    private async Task ShowConfigurationAsync()
    {
        var load = await _configStore.LoadAsync(allowInteractiveRecovery: true).ConfigureAwait(false);

        if (load.WasRecovered)
        {
            Renderer.WriteStyled($"Recovered on load: {load.RecoveryDescription}", TextStyle.Warning);
        }

        Renderer.WriteLine($"Config file: {_configStore.ConfigPath}");
        Renderer.WriteLine();

        var effective = _profileManager.ResolveEffectiveSettings(
            load.Configuration,
            load.Configuration.Settings.ActiveProfile);

        var table = new ConsoleTable { Title = "Effective Settings" };
        table.AddColumn("Setting");
        table.AddColumn("Value");
        table.AddRow("Active profile", load.Configuration.Settings.ActiveProfile ?? "(none)");
        table.AddRow("Theme", effective.Theme.ToString());
        table.AddRow("Log level", effective.LogLevel.ToString());
        table.AddRow("Default rules directory", effective.DefaultRulesDirectory ?? "(none)");
        table.AddRow("Backup enabled", effective.Backup.Enabled.ToString());
        table.AddRow("Max backups", effective.Backup.MaxBackups.ToString());
        Renderer.RenderTable(table);

        if (load.Configuration.Profiles.Count > 0)
        {
            var profileTable = new ConsoleTable { Title = "Profiles" };
            profileTable.AddColumn("Name");
            profileTable.AddColumn("Description");
            profileTable.AddColumn("Compiler Configs");
            foreach (var (name, profile) in load.Configuration.Profiles)
            {
                profileTable.AddRow(name, profile.Description ?? "", string.Join(", ", profile.CompilerConfigs));
            }

            Renderer.RenderTable(profileTable);
        }
    }

    private async Task ValidateConfigurationAsync()
    {
        var load = await _configStore.LoadAsync(allowInteractiveRecovery: true).ConfigureAwait(false);
        var result = _configStore.Validate(load.Configuration);

        if (result.IsValid)
        {
            Renderer.WriteStyled("Configuration is valid against schemas/dashboard-config.schema.json.", TextStyle.Success);
            return;
        }

        Renderer.WriteStyled("Configuration failed schema validation:", TextStyle.Error);
        foreach (var error in result.Errors)
        {
            Renderer.WriteStyled($"  {error}", TextStyle.Error);
        }
    }

    private Task ListBackupsAsync()
    {
        var backups = _configStore.ListBackups();
        if (backups.Count == 0)
        {
            Renderer.WriteLine("No backups found.");
            return Task.CompletedTask;
        }

        var table = new ConsoleTable { Title = "Configuration Backups" };
        table.AddColumn("Path");
        foreach (var backup in backups)
        {
            table.AddRow(backup);
        }

        Renderer.RenderTable(table);
        return Task.CompletedTask;
    }

    private async Task RestoreFromBackupAsync()
    {
        var backups = _configStore.ListBackups();
        if (backups.Count == 0)
        {
            Renderer.WriteStyled("No backups available to restore from.", TextStyle.Warning);
            return;
        }

        var selected = Prompter.Select("Select a backup to restore", backups);
        if (!Prompter.Confirm($"Restore configuration from {selected}? The current file will be backed up first.", false))
        {
            return;
        }

        await _configStore.RestoreFromBackupAsync(selected).ConfigureAwait(false);
        Renderer.WriteStyled($"Restored configuration from {selected}.", TextStyle.Success);
    }
}
