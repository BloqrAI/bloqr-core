namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Contains platform-specific information.
/// </summary>
public class PlatformInfo
{
    /// <summary>
    /// Gets or sets the operating system name.
    /// </summary>
    public string OSName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the operating system version.
    /// </summary>
    public string OSVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the processor architecture.
    /// </summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the platform is Windows.
    /// </summary>
    public bool IsWindows { get; set; }

    /// <summary>
    /// Gets or sets whether the platform is Linux.
    /// </summary>
    public bool IsLinux { get; set; }

    /// <summary>
    /// Gets or sets whether the platform is macOS.
    /// </summary>
    public bool IsMacOS { get; set; }
}