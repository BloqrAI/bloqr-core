using Serilog;
using Serilog.Events;

namespace Bloqr.Dashboard.Core.Logging;

/// <summary>
/// Registers the Dashboard's structured JSON logging: a dedicated log directory, rollover at
/// 24 hours or 1024 KB (whichever comes first, per the epic's explicit requirement), and a
/// user-configurable minimum level defaulting to <see cref="DashboardLogLevel.Error"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const long MaxFileSizeBytes = 1024 * 1024;

    /// <summary>
    /// Configures Serilog as the logging provider, writing structured JSON log lines (matching
    /// <c>schemas/log-entry.schema.json</c>) to <c>IDashboardPaths.LogDirectory</c>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="paths">Resolves the Dashboard's log directory.</param>
    /// <param name="minimumLevel">The minimum level to log at.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddDashboardLogging(
        this IServiceCollection services,
        IDashboardPaths paths,
        DashboardLogLevel minimumLevel)
    {
        ArgumentNullException.ThrowIfNull(paths);

        if (minimumLevel == DashboardLogLevel.Silent)
        {
            Log.Logger = Serilog.Core.Logger.None;
        }
        else
        {
            Directory.CreateDirectory(paths.LogDirectory);
            var logPath = Path.Combine(paths.LogDirectory, "bloqr-dashboard-.jsonl");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(ToSerilogLevel(minimumLevel))
                .Enrich.WithProperty("Application", "bloqr-dashboard")
                .WriteTo.File(
                    new DashboardJsonLogFormatter(),
                    logPath,
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: MaxFileSizeBytes,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: null,
                    shared: true)
                .CreateLogger();
        }

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        return services;
    }

    private static LogEventLevel ToSerilogLevel(DashboardLogLevel level) => level switch
    {
        DashboardLogLevel.Trace => LogEventLevel.Verbose,
        DashboardLogLevel.Debug => LogEventLevel.Debug,
        DashboardLogLevel.Info => LogEventLevel.Information,
        DashboardLogLevel.Warn => LogEventLevel.Warning,
        DashboardLogLevel.Error => LogEventLevel.Error,
        DashboardLogLevel.Silent => LogEventLevel.Fatal,
        _ => LogEventLevel.Error,
    };
}
