namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// The root model for the Dashboard's own <c>.jsonc</c> configuration file: schema version,
/// global settings, and named profiles. See <c>schemas/dashboard-config.schema.json</c>.
/// </summary>
public sealed class DashboardConfiguration
{
    /// <summary>
    /// Gets or sets the config schema version, for migration/compatibility checks. Strict
    /// "MAJOR.MINOR.PATCH" format.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the global default settings.
    /// </summary>
    public DashboardSettings Settings { get; set; } = new();

    /// <summary>
    /// Gets the named profiles, keyed by profile name.
    /// </summary>
    public Dictionary<string, DashboardProfile> Profiles { get; set; } = [];
}
