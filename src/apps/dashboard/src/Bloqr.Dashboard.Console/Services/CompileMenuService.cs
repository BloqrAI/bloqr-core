namespace Bloqr.Dashboard.Console.Services;

/// <summary>
/// Menu for running and validating compilations. Delegates all actual compilation work to
/// <see cref="IBloqrCompilerService"/> (registered by <c>BloqrCompiler</c>'s
/// <c>AddBloqrCompiler()</c>) — this menu is presentation only.
/// </summary>
public sealed class CompileMenuService : MenuServiceBase
{
    private readonly IBloqrCompilerService _compilerService;
    private readonly IDashboardConfigurationStore _configStore;
    private readonly ICompilerConfigGuard _configGuard;
    private readonly LiveProgressSession _liveProgressSession;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompileMenuService"/> class.
    /// </summary>
    public CompileMenuService(
        IConsoleRenderer renderer,
        IConsolePrompter prompter,
        IBloqrCompilerService compilerService,
        IDashboardConfigurationStore configStore,
        ICompilerConfigGuard configGuard,
        LiveProgressSession liveProgressSession,
        ILogger<CompileMenuService> logger)
        : base(renderer, prompter, logger)
    {
        _compilerService = compilerService;
        _configStore = configStore;
        _configGuard = configGuard;
        _liveProgressSession = liveProgressSession;
    }

    /// <inheritdoc />
    public override string Title => "Compile Rules";

    /// <inheritdoc />
    protected override Dictionary<string, Func<Task>> GetMenuActions() => new()
    {
        ["Compile using active profile"] = CompileActiveProfileAsync,
        ["Compile using a specific config file"] = CompileSpecificConfigAsync,
        ["Validate a config file"] = ValidateConfigAsync,
        ["Show available transformations"] = ShowTransformationsAsync,
        ["List backups for a compiler config"] = ListCompilerConfigBackupsAsync,
        ["Restore a compiler config from backup"] = RestoreCompilerConfigAsync,
        ["Prune backups for a compiler config"] = PruneCompilerConfigBackupsAsync,
    };

    private async Task CompileActiveProfileAsync()
    {
        var load = await _configStore.LoadAsync(allowInteractiveRecovery: true).ConfigureAwait(false);
        var configuration = load.Configuration;

        var activeProfileName = configuration.Settings.ActiveProfile;
        if (activeProfileName is null || !configuration.Profiles.TryGetValue(activeProfileName, out var profile))
        {
            Renderer.WriteStyled("No active profile is set. Use Profile Management to activate one first.", TextStyle.Warning);
            return;
        }

        var configPath = profile.CompilerConfigs.Count switch
        {
            0 => null,
            1 => profile.CompilerConfigs[0],
            _ => Prompter.Select($"Profile '{activeProfileName}' has multiple configs — pick one", profile.CompilerConfigs),
        };

        if (configPath is null)
        {
            Renderer.WriteStyled($"Profile '{activeProfileName}' has no compiler configs.", TextStyle.Warning);
            return;
        }

        await RunCompilationAsync(configPath).ConfigureAwait(false);
    }

    private async Task CompileSpecificConfigAsync()
    {
        var configPath = Prompter.Prompt("Path to compiler config file");
        await RunCompilationAsync(configPath).ConfigureAwait(false);
    }

    private async Task RunCompilationAsync(string configPath)
    {
        // Back up the referenced compiler config on a healthy load (or offer recovery on a
        // corrupt/missing one) before handing off to the actual compiler, so a later edit that
        // breaks this file has something to recover from. RunAsync below re-reads and
        // re-validates the file itself either way - this is a resilience layer, not a substitute.
        var guardResult = await Renderer.StatusAsync(
            $"Checking {configPath}...",
            () => _configGuard.LoadAsync(configPath)).ConfigureAwait(false);

        if (!guardResult.Success)
        {
            Renderer.WriteStyled($"Compiler config check failed: {guardResult.Message}", TextStyle.Warning);

            if (_configGuard.ListBackups(configPath).Count == 0)
            {
                Renderer.WriteStyled("No backups are available to recover from.", TextStyle.Error);
                return;
            }

            if (!Prompter.Confirm("Attempt to recover from the most recent valid backup?", false))
            {
                return;
            }

            var recovery = await _configGuard.RecoverAsync(configPath).ConfigureAwait(false);
            if (!recovery.Success)
            {
                Renderer.WriteStyled($"Recovery failed: {recovery.Message}", TextStyle.Error);
                return;
            }

            Renderer.WriteStyled(recovery.Message ?? "Recovered.", TextStyle.Success);
        }

        var result = await Renderer.LiveProgressAsync(async ctx =>
        {
            // CompilationProgressEventHandler (#270) picks this up to drive per-stage/per-chunk
            // progress bars and color-coded validation output for the duration of this compile.
            _liveProgressSession.Current = ctx;
            try
            {
                return await _compilerService.RunAsync(configPath).ConfigureAwait(false);
            }
            finally
            {
                _liveProgressSession.Current = null;
            }
        }).ConfigureAwait(false);

        DisplayResult(result);
    }

    private async Task ValidateConfigAsync()
    {
        var configPath = Prompter.Prompt("Path to compiler config file to validate");
        var result = await _compilerService.ValidateConfigurationAsync(configPath).ConfigureAwait(false);

        if (result.IsValid)
        {
            Renderer.WriteStyled("Configuration is valid.", TextStyle.Success);
        }
        else
        {
            Renderer.WriteStyled($"Configuration has {result.Errors.Count} error(s).", TextStyle.Error);
        }

        foreach (var error in result.Errors)
        {
            Renderer.WriteStyled($"  [error] {error.Field}: {error.Message}", TextStyle.Error);
        }

        foreach (var warning in result.Warnings)
        {
            Renderer.WriteStyled($"  [warn]  {warning.Field}: {warning.Message}", TextStyle.Warning);
        }
    }

    private Task ListCompilerConfigBackupsAsync()
    {
        var configPath = Prompter.Prompt("Path to compiler config file");
        var backups = _configGuard.ListBackups(configPath);

        if (backups.Count == 0)
        {
            Renderer.WriteLine($"No backups found for {configPath}.");
            return Task.CompletedTask;
        }

        var table = new ConsoleTable { Title = $"Backups for {configPath}" };
        table.AddColumn("Path");
        foreach (var backup in backups)
        {
            table.AddRow(backup);
        }

        Renderer.RenderTable(table);
        return Task.CompletedTask;
    }

    private async Task RestoreCompilerConfigAsync()
    {
        var configPath = Prompter.Prompt("Path to compiler config file to restore");

        if (_configGuard.ListBackups(configPath).Count == 0)
        {
            Renderer.WriteStyled($"No backups are available for {configPath}.", TextStyle.Warning);
            return;
        }

        if (!Prompter.Confirm($"Restore {configPath} from its most recent valid backup?", false))
        {
            return;
        }

        var recovery = await _configGuard.RecoverAsync(configPath).ConfigureAwait(false);
        if (recovery.Success)
        {
            Renderer.WriteStyled(recovery.Message ?? "Restored.", TextStyle.Success);
        }
        else
        {
            Renderer.WriteStyled($"Restore failed: {recovery.Message}", TextStyle.Error);
        }
    }

    private async Task PruneCompilerConfigBackupsAsync()
    {
        var configPath = Prompter.Prompt("Path to compiler config file");
        var backups = _configGuard.ListBackups(configPath);

        if (backups.Count == 0)
        {
            Renderer.WriteLine($"No backups found for {configPath}.");
            return;
        }

        // Default to the same retention count the Dashboard already applies to its own config's
        // backups, so there's one shared "how many backups do we keep" setting rather than two
        // - per the epic's "standing archive setting with configurable retention count".
        var load = await _configStore.LoadAsync(allowInteractiveRecovery: true).ConfigureAwait(false);
        var defaultRetention = load.Configuration.Settings.Backup.MaxBackups;

        var retentionText = Prompter.Prompt($"Keep how many of the {backups.Count} backups?", defaultRetention.ToString());
        if (!int.TryParse(retentionText, out var retention) || retention < 0)
        {
            Renderer.WriteStyled("Retention count must be a non-negative integer.", TextStyle.Warning);
            return;
        }

        var toRemove = Math.Max(0, backups.Count - retention);
        if (toRemove == 0)
        {
            Renderer.WriteLine("Nothing to prune.");
            return;
        }

        if (!Prompter.Confirm($"Delete {toRemove} old backup(s), keeping the {retention} newest?", false))
        {
            return;
        }

        _configGuard.PruneBackups(configPath, retention);
        Renderer.WriteStyled($"Pruned {toRemove} backup(s) for {configPath}.", TextStyle.Success);
    }

    private Task ShowTransformationsAsync()
    {
        var table = new ConsoleTable { Title = "Available Transformations" };
        table.AddColumn("Name");
        table.AddColumn("Recommended?", TextAlignment.Center);

        foreach (var transformation in TransformationHelper.AllTransformations)
        {
            var recommended = TransformationHelper.RecommendedTransformations.Contains(transformation) ? "yes" : "";
            table.AddRow(transformation, recommended);
        }

        Renderer.RenderTable(table);
        return Task.CompletedTask;
    }

    private void DisplayResult(CompilerResult result)
    {
        if (result.Success)
        {
            Renderer.WriteStyled(
                $"Compiled '{result.ConfigName}': {result.RuleCount} rules -> {result.OutputPath} " +
                $"({result.ElapsedMs}ms)",
                TextStyle.Success);
        }
        else
        {
            Renderer.WriteStyled($"Compilation failed: {result.ErrorMessage}", TextStyle.Error);
        }

        if (result.CopiedToRules)
        {
            Renderer.WriteLine($"Copied to: {result.RulesDestination}");
        }
    }
}
