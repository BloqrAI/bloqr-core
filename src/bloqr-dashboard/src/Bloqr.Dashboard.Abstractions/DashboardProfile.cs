namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// A named profile: a collection of settings overrides plus references to one or more
/// compiler-config files. See <c>schemas/dashboard-config.schema.json</c>.
/// </summary>
public sealed class DashboardProfile
{
    /// <summary>
    /// Gets or sets a human-readable description of what this profile is for.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the per-profile overrides of the global settings.
    /// </summary>
    public DashboardSettingsOverrides? SettingsOverrides { get; set; }

    /// <summary>
    /// Gets the paths to one or more compiler-config files that make up this profile. Required
    /// to contain at least one entry.
    /// </summary>
    public List<string> CompilerConfigs { get; set; } = [];
}
