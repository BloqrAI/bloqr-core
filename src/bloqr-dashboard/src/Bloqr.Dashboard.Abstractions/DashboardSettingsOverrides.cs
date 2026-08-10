namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Per-profile overrides of the global <see cref="DashboardSettings"/>. Every field is nullable;
/// unspecified fields fall back to the global defaults when resolved by
/// <c>IProfileManager.ResolveEffectiveSettings</c>.
/// </summary>
public sealed class DashboardSettingsOverrides
{
    /// <summary>
    /// Gets or sets the profile's theme override, or <c>null</c> to inherit the global default.
    /// </summary>
    public DashboardTheme? Theme { get; set; }

    /// <summary>
    /// Gets or sets the profile's log level override, or <c>null</c> to inherit the global default.
    /// </summary>
    public DashboardLogLevel? LogLevel { get; set; }

    /// <summary>
    /// Gets or sets the profile's default rules directory override, or <c>null</c> to inherit
    /// the global default.
    /// </summary>
    public string? DefaultRulesDirectory { get; set; }

    /// <summary>
    /// Gets or sets the profile's backup settings override, or <c>null</c> to inherit the global default.
    /// </summary>
    public BackupSettings? Backup { get; set; }
}
