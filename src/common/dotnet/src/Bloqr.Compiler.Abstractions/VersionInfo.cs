namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Contains version information for all compiler components.
/// </summary>
public class VersionInfo
{
    /// <summary>
    /// Gets or sets the module version.
    /// </summary>
    public string ModuleVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the PowerShell version (if running in PowerShell context).
    /// </summary>
    public string? PowerShellVersion { get; set; }

    /// <summary>
    /// Gets or sets the .NET runtime version.
    /// </summary>
    public string DotNetVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Node.js version.
    /// </summary>
    public string? NodeVersion { get; set; }

    /// <summary>
    /// Gets or sets the hostlist-compiler version.
    /// </summary>
    public string? HostlistCompilerVersion { get; set; }

    /// <summary>
    /// Gets or sets the path to the hostlist-compiler executable.
    /// </summary>
    public string? HostlistCompilerPath { get; set; }

    /// <summary>
    /// Gets or sets the platform information.
    /// </summary>
    public PlatformInfo Platform { get; set; } = new();
}