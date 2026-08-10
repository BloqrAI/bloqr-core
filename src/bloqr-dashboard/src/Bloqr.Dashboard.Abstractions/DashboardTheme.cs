namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Console UI color theme, as documented in <c>schemas/dashboard-config.schema.json</c>.
/// </summary>
public enum DashboardTheme
{
    /// <summary>The terminal's default color scheme.</summary>
    Default,

    /// <summary>A dark color scheme.</summary>
    Dark,

    /// <summary>A light color scheme.</summary>
    Light,

    /// <summary>A high-contrast color scheme for accessibility.</summary>
    HighContrast,
}
