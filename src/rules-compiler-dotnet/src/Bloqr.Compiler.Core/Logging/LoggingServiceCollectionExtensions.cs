using Serilog;
using Serilog.Events;

namespace Bloqr.Compiler.Core.Logging;

/// <summary>
/// Registers structured JSON logging shared by every app in this repo: a dedicated per-app log
/// directory, rollover at 24 hours or 1024 KB (whichever comes first), and consistent file
/// naming/shape, per issue #275's cross-app consistency requirement. App-specific setup
/// (e.g. the Dashboard's own <c>DashboardLogLevel</c> enum and profile-driven log directory)
/// stays in each app's own layer and maps down to this shared extension.
/// </summary>
public static class LoggingServiceCollectionExtensions
{
    private const long MaxFileSizeBytes = 1024 * 1024;

    /// <summary>
    /// Builds a Serilog logger writing structured JSON log lines (matching
    /// <c>schemas/log-entry.schema.json</c>) to <paramref name="logDirectory"/>, or <c>null</c>
    /// if <paramref name="minimumLevel"/> is <c>null</c> (logging disabled).
    /// </summary>
    /// <param name="applicationName">
    /// The application's name, used as the log-file prefix (e.g. <c>rules-compiler-dotnet-.jsonl</c>)
    /// and stamped onto every log event as the <c>application</c> field.
    /// </param>
    /// <param name="logDirectory">The directory log files are written to.</param>
    /// <param name="minimumLevel">
    /// The minimum level to log at, or <c>null</c> to disable logging entirely.
    /// </param>
    /// <returns>The configured logger, or <c>null</c> if logging is disabled.</returns>
    public static Serilog.ILogger? CreateStructuredFileLogger(
        string applicationName,
        string logDirectory,
        LogEventLevel? minimumLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);

        if (minimumLevel is null)
        {
            return null;
        }

        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, $"{applicationName}-.jsonl");

        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel.Value)
            .Enrich.WithProperty("Application", applicationName)
            .WriteTo.File(
                new StructuredJsonLogFormatter(),
                logPath,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: MaxFileSizeBytes,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: null,
                shared: true)
            .CreateLogger();
    }

    /// <summary>
    /// Configures Serilog as the <em>sole</em> logging provider (clearing any others already
    /// registered), writing structured JSON to <paramref name="logDirectory"/>. Use this when an
    /// app's primary UI is a rendering library (e.g. Spectre.Console) rather than console log
    /// output, so <c>ILogger</c> calls don't fight with the UI for the terminal — this is
    /// how the Dashboard uses it. For an app that already relies on console log output for
    /// user-visible information (like <c>RulesCompiler.Console</c>), use
    /// <see cref="AddStructuredFileLogging(ILoggingBuilder, string, string, LogEventLevel?)"/>
    /// instead to add file logging alongside the existing console provider rather than replacing it.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="applicationName">The application's name (see <see cref="CreateStructuredFileLogger"/>).</param>
    /// <param name="logDirectory">The directory log files are written to.</param>
    /// <param name="minimumLevel">The minimum level to log at, or <c>null</c> to disable logging entirely.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddStructuredLogging(
        this IServiceCollection services,
        string applicationName,
        string logDirectory,
        LogEventLevel? minimumLevel)
    {
        var logger = CreateStructuredFileLogger(applicationName, logDirectory, minimumLevel);
        Log.Logger = logger ?? Serilog.Core.Logger.None;

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        return services;
    }

    /// <summary>
    /// Adds structured JSON file logging as an <em>additional</em> provider, alongside whatever
    /// providers are already registered on <paramref name="builder"/> (e.g. a console provider).
    /// Unlike <see cref="AddStructuredLogging"/>, this does not touch <see cref="Log.Logger"/> or
    /// clear existing providers.
    /// </summary>
    /// <param name="builder">The logging builder to add the file provider to.</param>
    /// <param name="applicationName">The application's name (see <see cref="CreateStructuredFileLogger"/>).</param>
    /// <param name="logDirectory">The directory log files are written to.</param>
    /// <param name="minimumLevel">The minimum level to log at, or <c>null</c> to skip adding the provider entirely.</param>
    /// <returns>The logging builder, for chaining.</returns>
    public static ILoggingBuilder AddStructuredFileLogging(
        this ILoggingBuilder builder,
        string applicationName,
        string logDirectory,
        LogEventLevel? minimumLevel)
    {
        var logger = CreateStructuredFileLogger(applicationName, logDirectory, minimumLevel);
        if (logger is not null)
        {
            builder.AddSerilog(logger, dispose: true);
        }

        return builder;
    }
}
