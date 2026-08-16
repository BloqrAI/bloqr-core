namespace Bloqr.Dashboard.Console.Services;

/// <summary>
/// The Dashboard's top-level application loop: menu- and wizard-driven, and — per the epic's
/// explicit requirement — never terminates on its own, even on unexpected exceptions. Global
/// exception handlers are registered so a truly unhandled exception is logged rather than
/// silently crashing the process; every per-action exception inside the loop is caught, logged,
/// and rendered, returning control to the main menu.
/// </summary>
public sealed class DashboardApplication
{
    private readonly IConsoleRenderer _renderer;
    private readonly IConsolePrompter _prompter;
    private readonly IMenuServiceFactory _menuServiceFactory;
    private readonly IDashboardConfigurationStore _configStore;
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<DashboardApplication> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardApplication"/> class.
    /// </summary>
    public DashboardApplication(
        IConsoleRenderer renderer,
        IConsolePrompter prompter,
        IMenuServiceFactory menuServiceFactory,
        IDashboardConfigurationStore configStore,
        IDashboardService dashboardService,
        ILogger<DashboardApplication> logger)
    {
        _renderer = renderer;
        _prompter = prompter;
        _menuServiceFactory = menuServiceFactory;
        _configStore = configStore;
        _dashboardService = dashboardService;
        _logger = logger;
    }

    /// <summary>
    /// Runs the Dashboard, dispatching to a CLI surface (<c>--version</c>, <c>--help</c>,
    /// <c>--compile</c>, <c>--validate-config</c>, <c>--list-profiles</c>,
    /// <c>--activate-profile</c>, <c>--non-interactive</c>) or the interactive main menu loop -
    /// full CLI-switch parity with the interactive menu's compile/validate/profile-management
    /// operations, per #271. Every CLI branch below calls <see cref="IDashboardService"/>, the
    /// same embeddable-library API boundary a future .NET MAUI host would depend on - the CLI is
    /// itself just one more consumer of that boundary, not a separate code path. Config
    /// generation (the wizard) remains interactive-only: mirroring its entire prompt tree as CLI
    /// flags is a materially larger, separate effort - CLI users hand-edit or generate a
    /// compiler-config JSON/JSONC file some other way and validate it with
    /// <c>--validate-config</c>.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="cancellationToken">Cancellation token, signaled on Ctrl+C.</param>
    /// <returns>The process exit code.</returns>
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Contains("--version") || args.Contains("-v"))
        {
            _renderer.WriteLine(GetVersionString());
            return 0;
        }

        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return 0;
        }

        if (TryGetCliCommand(args, out var cliCommand))
        {
            try
            {
                return await cliCommand(cancellationToken).ConfigureAwait(false);
            }
            catch (DashboardConfigurationException)
            {
                // Let Program.cs's dedicated catch handle this with its own distinct exit code.
                throw;
            }
            catch (Exception ex)
            {
                // CLI commands run outside the interactive loop's per-action try/catch, but
                // should degrade the same way: a clean message and a non-zero exit code instead
                // of an unhandled exception reaching Program.cs's generic catch-all.
                _logger.LogError(ex, "CLI command failed");
                _renderer.WriteStyled($"Error: {ex.Message}", TextStyle.Error);
                return 1;
            }
        }

        RegisterGlobalExceptionHandlers();

        var nonInteractive = args.Contains("--non-interactive") || System.Console.IsInputRedirected;
        if (nonInteractive)
        {
            // Non-interactive mode never prompts, including for corruption recovery: a corrupt
            // config surfaces as a DashboardConfigurationException (a distinct, non-zero exit
            // code from Program.cs) rather than hanging or silently guessing.
            var load = await _configStore.LoadAsync(allowInteractiveRecovery: false, cancellationToken)
                .ConfigureAwait(false);
            _renderer.WriteLine("Bloqr Dashboard: non-interactive mode, no subcommand given.");
            _renderer.WriteLine($"Configuration file: {_configStore.ConfigPath}");
            _renderer.WriteLine($"Active profile: {load.Configuration.Settings.ActiveProfile ?? "(none)"}");
            return 0;
        }

        DisplayWelcomeBanner();

        var running = true;
        while (running)
        {
            try
            {
                var menuServices = _menuServiceFactory.GetMenuServices();
                var choice = _prompter.Select("Main Menu", menuServices.Select(m => m.Title).Append("Exit").ToArray());
                _renderer.WriteLine();

                if (choice == "Exit")
                {
                    running = false;
                    continue;
                }

                var selected = menuServices.First(m => m.Title == choice);
                await selected.ShowAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                running = false;
            }
            catch (Exception ex)
            {
                // Never crash: log, render, and return to the main menu.
                _logger.LogError(ex, "Unhandled exception in the Dashboard main loop");
                _renderer.WriteStyled($"An unexpected error occurred: {ex.Message}", TextStyle.Error);
                _renderer.WriteLine("The Dashboard has recovered and returned to the main menu.");
                _renderer.WriteLine();
            }
        }

        return 0;
    }

    /// <summary>
    /// Matches <paramref name="args"/> against the CLI subcommands and, if one matches, returns
    /// a delegate that runs it. Checked in a fixed priority order since a value-bearing flag
    /// (e.g. <c>--validate-config &lt;path&gt;</c>) could otherwise be ambiguous with adjacent flags.
    /// </summary>
    private bool TryGetCliCommand(string[] args, [NotNullWhen(true)] out Func<CancellationToken, Task<int>>? command)
    {
        if (args.Contains("--list-profiles"))
        {
            command = RunListProfilesAsync;
            return true;
        }

        if (GetOptionValue(args, "--activate-profile") is { } activateProfileName)
        {
            command = ct => RunActivateProfileAsync(activateProfileName, ct);
            return true;
        }

        if (GetOptionValue(args, "--validate-config") is { } validateConfigPath)
        {
            command = ct => RunValidateConfigAsync(validateConfigPath, ct);
            return true;
        }

        if (args.Contains("--compile"))
        {
            var compileConfigPath = GetOptionValue(args, "--compile");
            command = ct => RunCompileAsync(compileConfigPath, ct);
            return true;
        }

        command = null;
        return false;
    }

    private async Task<int> RunListProfilesAsync(CancellationToken cancellationToken)
    {
        var profiles = await _dashboardService.ListProfilesAsync(cancellationToken).ConfigureAwait(false);
        if (profiles.Count == 0)
        {
            _renderer.WriteLine("No profiles are defined.");
            return 0;
        }

        var active = await _dashboardService.GetActiveProfileAsync(cancellationToken).ConfigureAwait(false);
        foreach (var profile in profiles)
        {
            _renderer.WriteLine(profile == active ? $"* {profile}" : $"  {profile}");
        }

        return 0;
    }

    private async Task<int> RunActivateProfileAsync(string profileName, CancellationToken cancellationToken)
    {
        try
        {
            await _dashboardService.ActivateProfileAsync(profileName, cancellationToken).ConfigureAwait(false);
            _renderer.WriteStyled($"Activated profile '{profileName}'.", TextStyle.Success);
            return 0;
        }
        catch (KeyNotFoundException)
        {
            _renderer.WriteStyled($"No profile named '{profileName}' exists.", TextStyle.Error);
            return 1;
        }
    }

    private async Task<int> RunValidateConfigAsync(string configPath, CancellationToken cancellationToken)
    {
        var result = await _dashboardService.ValidateCompilerConfigAsync(configPath, cancellationToken).ConfigureAwait(false);

        _renderer.WriteStyled(
            result.IsValid ? "Configuration is valid." : $"Configuration has {result.Errors.Count} error(s).",
            result.IsValid ? TextStyle.Success : TextStyle.Error);

        foreach (var error in result.Errors)
        {
            _renderer.WriteStyled($"  [error] {error.Field}: {error.Message}", TextStyle.Error);
        }

        foreach (var warning in result.Warnings)
        {
            _renderer.WriteStyled($"  [warn]  {warning.Field}: {warning.Message}", TextStyle.Warning);
        }

        return result.IsValid ? 0 : 1;
    }

    private async Task<int> RunCompileAsync(string? configPath, CancellationToken cancellationToken)
    {
        var configPaths = configPath is not null
            ? [configPath]
            : await _dashboardService.GetActiveProfileCompilerConfigsAsync(cancellationToken).ConfigureAwait(false);

        if (configPaths.Count == 0)
        {
            _renderer.WriteStyled(
                "No compiler config specified and no active profile has one. Use --compile <path> or --activate-profile <name> first.",
                TextStyle.Error);
            return 1;
        }

        var exitCode = 0;
        foreach (var path in configPaths)
        {
            var result = await _dashboardService.CompileAsync(path, cancellationToken).ConfigureAwait(false);
            if (result.Success)
            {
                _renderer.WriteStyled(
                    $"Compiled '{result.ConfigName}': {result.RuleCount} rules -> {result.OutputPath} ({result.ElapsedMs}ms)",
                    TextStyle.Success);
            }
            else
            {
                _renderer.WriteStyled($"Compilation failed for {path}: {result.ErrorMessage}", TextStyle.Error);
                exitCode = 1;
            }
        }

        return exitCode;
    }

    /// <summary>
    /// Returns the value following <paramref name="flag"/> in <paramref name="args"/>, or
    /// <c>null</c> if the flag isn't present or has no following value (e.g. <c>--compile</c>
    /// used alone to mean "the active profile's config").
    /// </summary>
    private static string? GetOptionValue(string[] args, string flag)
    {
        var index = Array.IndexOf(args, flag);
        if (index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            return args[index + 1];
        }

        return null;
    }

    private void RegisterGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            _logger.LogCritical(
                e.ExceptionObject as Exception,
                "Unhandled AppDomain exception (IsTerminating={IsTerminating})",
                e.IsTerminating);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            _logger.LogError(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };

        System.Console.CancelKeyPress += (_, _) =>
        {
            _logger.LogInformation("Cancel key pressed; shutting down gracefully");
            Serilog.Log.CloseAndFlush();
        };
    }

    private void DisplayWelcomeBanner()
    {
        _renderer.RenderRule("Bloqr Dashboard");
        _renderer.WriteLine($"Configuration: {_configStore.ConfigPath}");
        _renderer.WriteLine();
    }

    private static string GetVersionString()
    {
        var version = typeof(DashboardApplication).Assembly.GetName().Version;
        return $"Bloqr Dashboard {version}";
    }

    private void PrintHelp()
    {
        _renderer.WriteLine("Bloqr Dashboard - console application for compiler config generation and compilation.");
        _renderer.WriteLine();
        _renderer.WriteLine("Usage: bloqr-dashboard [options]");
        _renderer.WriteLine();
        _renderer.WriteLine("Options:");
        _renderer.WriteLine("  --help, -h            Show this help message");
        _renderer.WriteLine("  --version, -v         Show version information");
        _renderer.WriteLine("  --config <path>       Use a specific Dashboard configuration file");
        _renderer.WriteLine("  --profile <name>      Activate a specific profile for this run");
        _renderer.WriteLine("  --log-level <level>   Override the configured log level");
        _renderer.WriteLine("                        (trace|debug|info|warn|error|silent)");
        _renderer.WriteLine("  --non-interactive     Print status and exit instead of prompting");
        _renderer.WriteLine("  --compile [path]      Compile a specific compiler config, or the active");
        _renderer.WriteLine("                        profile's config(s) if no path is given");
        _renderer.WriteLine("  --validate-config <path>");
        _renderer.WriteLine("                        Validate a compiler config file without compiling it");
        _renderer.WriteLine("  --list-profiles       List Dashboard profiles (* marks the active one)");
        _renderer.WriteLine("  --activate-profile <name>");
        _renderer.WriteLine("                        Activate a profile and persist the change");
    }
}
