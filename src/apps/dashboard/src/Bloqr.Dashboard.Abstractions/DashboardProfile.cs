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
    /// <remarks>
    /// Deliberately no per-engine (DNS/browser) output-path fields here (#440): a profile only
    /// points at compiler-config files, and each compiler config is already the single source of
    /// truth for its own output path(s) via <c>CompilerOptions.OutputPath</c>/
    /// <c>.BrowserOutputPath</c> (#436). Duplicating those paths onto the profile would let the
    /// two drift out of sync with no clear owner for which wins. <c>--compile --browser-output
    /// &lt;path&gt;</c> overrides a config's browser output for one CLI invocation without needing
    /// a profile-level field for it.
    /// </remarks>
    public List<string> CompilerConfigs { get; set; } = [];
}
