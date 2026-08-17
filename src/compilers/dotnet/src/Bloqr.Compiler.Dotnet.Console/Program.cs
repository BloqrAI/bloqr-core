namespace Bloqr.Compiler.Dotnet.Console;

/// <summary>
/// Entry point for the BloqrCompiler Console application.
/// </summary>
public class Program
{
    /// <summary>
    /// Main entry point for the application.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Exit code: 0 for success, 1 for failure.</returns>
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var configuration = BuildConfiguration(args);
            var services = ConfigureServices(configuration);

            // `await using` drains QueuedCompilationEventDispatcher's background queue (if
            // registered) via DisposeAsync before the process exits (#274).
            await using var serviceProvider = services.BuildServiceProvider();

            var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger<Program>();
            logger.LogInformation("BloqrCompiler Console starting");

            var app = serviceProvider.GetRequiredService<ConsoleApplication>();
            var exitCode = await app.RunAsync(args);

            logger.LogInformation("BloqrCompiler Console shutting down");
            return exitCode;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    /// <summary>
    /// Builds the application configuration from various sources.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>The built configuration.</returns>
    private static IConfiguration BuildConfiguration(string[] args)
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables("BLOQR_COMPILER_")
            .AddCommandLine(args)
            .Build();
    }

    /// <summary>
    /// Configures the dependency injection container with all required services.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The configured service collection.</returns>
    private static IServiceCollection ConfigureServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        // Configuration
        services.AddSingleton(configuration);

        // Logging: console output as before, plus structured JSON file logging (dedicated
        // per-app log directory, 24h-or-1MB rollover) matching the convention shared with
        // bloqr-dashboard - see Bloqr.Compiler.Core.Logging.LoggingServiceCollectionExtensions.
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole(options =>
            {
                options.FormatterName = "simple";
            });
            builder.AddStructuredFileLogging("bloqr-compiler-dotnet", logDirectory, LogEventLevel.Information);
        });

        // Add BloqrCompiler services
        services.AddBloqrCompiler();

        // Fire-and-forget events (source loaded, file locks, chunk/hash completion, etc.) are
        // processed on a background queue so a slow handler (e.g. writing structured logs)
        // never blocks the compilation pipeline (#274).
        services.AddQueuedCompilationEventDispatching();

        // Console application
        services.AddSingleton<ConsoleApplication>();

        return services;
    }
}
