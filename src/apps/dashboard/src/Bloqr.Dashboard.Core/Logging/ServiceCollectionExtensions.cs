using Serilog.Events;

namespace Bloqr.Dashboard.Core.Logging;

/// <summary>
/// Registers the Dashboard's structured JSON logging by mapping its own
/// <see cref="DashboardLogLevel"/> and profile-driven log directory down onto the shared
/// <c>AddStructuredLogging</c> extension on <see cref="Bloqr.Compiler.Core.Logging.LoggingServiceCollectionExtensions"/>
/// from <c>Bloqr.Compiler.Core</c> — the actual formatter, rollover policy, and file-naming
/// convention live there so every app in this repo shares them (issue #275).
/// </summary>
public static class ServiceCollectionExtensions
{
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

        return services.AddStructuredLogging("bloqr-dashboard", paths.LogDirectory, ToSerilogLevel(minimumLevel));
    }

    private static LogEventLevel? ToSerilogLevel(DashboardLogLevel level) => level switch
    {
        DashboardLogLevel.Trace => LogEventLevel.Verbose,
        DashboardLogLevel.Debug => LogEventLevel.Debug,
        DashboardLogLevel.Info => LogEventLevel.Information,
        DashboardLogLevel.Warn => LogEventLevel.Warning,
        DashboardLogLevel.Error => LogEventLevel.Error,
        DashboardLogLevel.Silent => null,
        _ => LogEventLevel.Error,
    };
}
