namespace Bloqr.Dashboard.Console;

/// <summary>
/// Entry point for the Bloqr Dashboard console application.
/// </summary>
public static class Program
{
    /// <summary>
    /// Main entry point. Wires up configuration, structured logging, the rules-compiler
    /// services, and the Dashboard's own menu services, then runs the application loop.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        // Spectre.Console can end up with an undetectable (-1) console width in headless/CI
        // environments (piped output, no pty), and silently drops ALL AnsiConsole output — even
        // plain WriteLine — until the width is set. Floor it so --version/--help/--non-interactive
        // and the interactive menus stay reliable when run under CI, Docker, or systemd.
        if (AnsiConsole.Profile.Width <= 0)
        {
            AnsiConsole.Profile.Width = 80;
        }

        var configuration = BuildConfiguration(args);

        var configOverride = configuration["config"];
        var paths = new DashboardPaths(configOverride);
        var bootstrapLogLevel = ResolveBootstrapLogLevel(configuration, paths.ConfigFilePath);

        using var cancellationSource = new CancellationTokenSource();
        System.Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellationSource.Cancel();
        };

        try
        {
            var services = ConfigureServices(configuration, paths, bootstrapLogLevel);
            using var serviceProvider = services.BuildServiceProvider();

            var app = serviceProvider.GetRequiredService<DashboardApplication>();
            return await app.RunAsync(args, cancellationSource.Token).ConfigureAwait(false);
        }
        catch (DashboardConfigurationException ex)
        {
            // Configuration failures that recovery couldn't resolve (e.g. non-interactive mode
            // hitting a corrupt file) are the one case allowed to exit with a distinct, non-zero
            // code instead of looping — there's no menu to return to before a config exists.
            AnsiConsole.MarkupLine($"[red]Configuration error:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
        finally
        {
            Serilog.Log.CloseAndFlush();
        }
    }

    private static IConfiguration BuildConfiguration(string[] args)
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables("BLOQR_DASHBOARD_")
            .AddCommandLine(args)
            .Build();
    }

    /// <summary>
    /// Resolves the log level to bootstrap Serilog with, before the DI-registered
    /// <see cref="IDashboardConfigurationStore"/> exists to load the real configuration. Prefers
    /// an explicit <c>--log-level</c>/<c>BLOQR_DASHBOARD_LOG_LEVEL</c> override, then the
    /// existing config file's configured level (best-effort — a corrupt file falls back to the
    /// safe default here and gets properly recovered once the real store loads it), then the
    /// schema default of <see cref="DashboardLogLevel.Error"/>.
    /// </summary>
    private static DashboardLogLevel ResolveBootstrapLogLevel(IConfiguration configuration, string configPath)
    {
        var overrideValue = configuration["log-level"] ?? configuration["LogLevel"];
        if (overrideValue is not null && Enum.TryParse<DashboardLogLevel>(overrideValue, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        try
        {
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var existing = JsonSerializer.Deserialize<DashboardConfiguration>(json, DashboardJsonOptions.Instance);
                if (existing is not null)
                {
                    return existing.Settings.LogLevel;
                }
            }
        }
        catch (JsonException)
        {
            // Corrupt file: fall through to the safe default. DashboardConfigurationStore runs
            // full quarantine/recovery once it loads the file for real.
        }

        return DashboardLogLevel.Error;
    }

    private static IServiceCollection ConfigureServices(
        IConfiguration configuration,
        IDashboardPaths paths,
        DashboardLogLevel bootstrapLogLevel)
    {
        var services = new ServiceCollection();

        services.AddSingleton(configuration);
        services.AddSingleton(paths);
        services.AddDashboardLogging(paths, bootstrapLogLevel);

        services.AddRulesCompiler();
        services.AddCompilationEventHandler<CompilationLoggingEventHandler>();

        services.AddSingleton<IDashboardConfigurationStore, DashboardConfigurationStore>();
        services.AddSingleton<IProfileManager, ProfileManager>();
        services.AddSingleton<ILogEntryReader, LogEntryReader>();
        services.AddSingleton<ICompilerConfigGuard, CompilerConfigGuard>();
        services.AddSingleton<ICompilerConfigSchemaValidator, CompilerConfigSchemaValidator>();

        services.AddSingleton<IConsoleRenderer, SpectreConsoleRenderer>();
        services.AddSingleton<IConsolePrompter, SpectreConsolePrompter>();

        services.AddSingleton<CompileMenuService>();
        services.AddSingleton<ConfigMenuService>();
        services.AddSingleton<CompilerConfigWizardMenuService>();
        services.AddSingleton<ProfileMenuService>();
        services.AddSingleton<LogsMenuService>();
        services.AddSingleton<DiagnosticsMenuService>();

        services.AddSingleton<IMenuServiceFactory>(provider => new MenuServiceFactory(
            provider,
            [
                typeof(CompileMenuService),
                typeof(ConfigMenuService),
                typeof(CompilerConfigWizardMenuService),
                typeof(ProfileMenuService),
                typeof(LogsMenuService),
                typeof(DiagnosticsMenuService),
            ]));

        services.AddSingleton<DashboardApplication>();

        return services;
    }
}
