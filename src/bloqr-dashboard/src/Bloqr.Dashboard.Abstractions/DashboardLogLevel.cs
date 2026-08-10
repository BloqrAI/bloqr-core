namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Log verbosity level, as documented in <c>schemas/dashboard-config.schema.json</c>.
/// The epic-specified default is <see cref="Error"/>.
/// </summary>
public enum DashboardLogLevel
{
    /// <summary>The most detailed tracing output.</summary>
    Trace,

    /// <summary>Debug-level diagnostic output.</summary>
    Debug,

    /// <summary>Informational messages about normal operation.</summary>
    Info,

    /// <summary>Warnings about unexpected but non-fatal conditions.</summary>
    Warn,

    /// <summary>Errors only. This is the Dashboard's default level.</summary>
    Error,

    /// <summary>No logging output.</summary>
    Silent,
}
