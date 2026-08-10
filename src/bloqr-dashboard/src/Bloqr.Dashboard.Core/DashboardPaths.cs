namespace Bloqr.Dashboard.Core;

/// <summary>
/// Resolves the Dashboard's well-known filesystem locations. Windows uses <c>%APPDATA%</c>;
/// other platforms follow the XDG Base Directory convention (<c>$XDG_CONFIG_HOME</c>, falling
/// back to <c>~/.config</c>). The whole tree can be relocated via the
/// <c>BLOQR_DASHBOARD_CONFIG_DIR</c> environment variable (used by tests); the config file alone
/// can be relocated via an explicit override or <c>BLOQR_DASHBOARD_CONFIG</c>.
/// </summary>
public sealed class DashboardPaths : IDashboardPaths
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardPaths"/> class.
    /// </summary>
    /// <param name="configFileOverride">
    /// An explicit configuration file path (e.g. from a <c>--config</c> CLI flag), taking
    /// precedence over <c>BLOQR_DASHBOARD_CONFIG</c> and the platform default.
    /// </param>
    public DashboardPaths(string? configFileOverride = null)
    {
        var configDirectory = ResolveConfigDirectory();
        ConfigFilePath = configFileOverride
            ?? Environment.GetEnvironmentVariable("BLOQR_DASHBOARD_CONFIG")
            ?? Path.Combine(configDirectory, "dashboard-config.jsonc");
        BackupDirectory = Path.Combine(configDirectory, "backups");
        LogDirectory = Path.Combine(configDirectory, "logs");
        PatternFilesDirectory = Path.Combine(configDirectory, "patterns");
    }

    private DashboardPaths(string configFilePath, string backupDirectory, string logDirectory, string patternFilesDirectory)
    {
        ConfigFilePath = configFilePath;
        BackupDirectory = backupDirectory;
        LogDirectory = logDirectory;
        PatternFilesDirectory = patternFilesDirectory;
    }

    /// <summary>
    /// Creates a <see cref="DashboardPaths"/> with every path rooted under
    /// <paramref name="rootDirectory"/>, bypassing platform detection and environment variables
    /// entirely. Intended for tests that need full, parallel-safe isolation without mutating
    /// process-wide environment state.
    /// </summary>
    /// <param name="rootDirectory">The root directory all Dashboard paths are placed under.</param>
    /// <returns>A fully isolated <see cref="DashboardPaths"/> instance.</returns>
    public static DashboardPaths ForDirectory(string rootDirectory) => new(
        Path.Combine(rootDirectory, "dashboard-config.jsonc"),
        Path.Combine(rootDirectory, "backups"),
        Path.Combine(rootDirectory, "logs"),
        Path.Combine(rootDirectory, "patterns"));

    /// <inheritdoc />
    public string ConfigFilePath { get; }

    /// <inheritdoc />
    public string BackupDirectory { get; }

    /// <inheritdoc />
    public string LogDirectory { get; }

    /// <inheritdoc />
    public string PatternFilesDirectory { get; }

    private static string ResolveConfigDirectory()
    {
        var overrideDirectory = Environment.GetEnvironmentVariable("BLOQR_DASHBOARD_CONFIG_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            return overrideDirectory;
        }

        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "bloqr-dashboard");
        }

        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var baseDirectory = !string.IsNullOrWhiteSpace(xdgConfigHome)
            ? xdgConfigHome
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

        return Path.Combine(baseDirectory, "bloqr-dashboard");
    }
}
