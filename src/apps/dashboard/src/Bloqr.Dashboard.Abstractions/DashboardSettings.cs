namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Global default settings for the Dashboard, used when no profile is active or as fallback
/// values for a profile that doesn't override them. See <c>schemas/dashboard-config.schema.json</c>.
/// </summary>
public sealed class DashboardSettings
{
    /// <summary>
    /// Gets or sets the console UI color theme. Defaults to <see cref="DashboardTheme.Default"/>.
    /// </summary>
    public DashboardTheme Theme { get; set; } = DashboardTheme.Default;

    /// <summary>
    /// Gets or sets the default log verbosity. Defaults to <see cref="DashboardLogLevel.Error"/>
    /// per the epic's explicit requirement.
    /// </summary>
    public DashboardLogLevel LogLevel { get; set; } = DashboardLogLevel.Error;

    /// <summary>
    /// Gets or sets the default directory compiled output is copied to when copy-to-rules is used.
    /// </summary>
    public string? DefaultRulesDirectory { get; set; }

    /// <summary>
    /// Gets or sets the backup/restore settings.
    /// </summary>
    public BackupSettings Backup { get; set; } = new();

    /// <summary>
    /// Gets or sets the name of the currently active profile (must match a key in
    /// <see cref="DashboardConfiguration.Profiles"/>), or <c>null</c> if none is active.
    /// </summary>
    public string? ActiveProfile { get; set; }

    /// <summary>
    /// Gets or sets the placeholder settings for the future <c>adguard-api-dotnet</c>
    /// integration (#272). Disabled by default.
    /// </summary>
    public AdGuardApiSettings AdGuardApi { get; set; } = new();
}
