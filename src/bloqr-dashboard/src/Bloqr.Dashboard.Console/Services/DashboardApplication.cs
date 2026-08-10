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
    private readonly ILogger<DashboardApplication> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardApplication"/> class.
    /// </summary>
    public DashboardApplication(
        IConsoleRenderer renderer,
        IConsolePrompter prompter,
        IMenuServiceFactory menuServiceFactory,
        IDashboardConfigurationStore configStore,
        ILogger<DashboardApplication> logger)
    {
        _renderer = renderer;
        _prompter = prompter;
        _menuServiceFactory = menuServiceFactory;
        _configStore = configStore;
        _logger = logger;
    }

    /// <summary>
    /// Runs the Dashboard, dispatching to a minimal CLI surface (<c>--version</c>, <c>--help</c>,
    /// <c>--non-interactive</c>) or the interactive main menu loop. Full CLI-switch parity with
    /// interactive-mode operations is issue #271's scope; this is the minimal surface needed for
    /// scripting/CI-safety today.
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
    }
}
