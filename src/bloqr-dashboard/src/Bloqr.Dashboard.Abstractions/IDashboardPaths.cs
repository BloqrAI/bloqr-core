namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Resolves the well-known filesystem locations the Dashboard uses: its configuration file,
/// backup directory, log directory, and pattern-files directory. Centralizing these avoids
/// duplicating platform-specific path logic (e.g. XDG vs. %APPDATA%) across services.
/// </summary>
public interface IDashboardPaths
{
    /// <summary>
    /// Gets the resolved path to the Dashboard's own configuration file.
    /// </summary>
    string ConfigFilePath { get; }

    /// <summary>
    /// Gets the directory backups of config files are stored in.
    /// </summary>
    string BackupDirectory { get; }

    /// <summary>
    /// Gets the directory structured log files are written to.
    /// </summary>
    string LogDirectory { get; }

    /// <summary>
    /// Gets the dedicated, non-user-configurable directory for local inclusion/exclusion
    /// pattern files, per the epic's requirement that this location stay fixed and be
    /// clearly communicated to the user.
    /// </summary>
    string PatternFilesDirectory { get; }
}
