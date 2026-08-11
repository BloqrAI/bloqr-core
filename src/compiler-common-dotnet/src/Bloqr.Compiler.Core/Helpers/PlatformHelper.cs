namespace Bloqr.Compiler.Core.Helpers;

/// <summary>
/// Provides platform detection and related utilities.
/// </summary>
public static class PlatformHelper
{
    /// <summary>
    /// Gets information about the current platform.
    /// </summary>
    /// <returns>Platform information.</returns>
    public static PlatformInfo GetPlatformInfo()
    {
        return new PlatformInfo
        {
            OSName = GetOSName(),
            OSVersion = Environment.OSVersion.VersionString,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
            IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        };
    }

    /// <summary>
    /// Gets the operating system name.
    /// </summary>
    /// <returns>OS name string.</returns>
    public static string GetOSName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "macOS";

        return RuntimeInformation.OSDescription;
    }

    /// <summary>
    /// Checks if the current platform is Windows.
    /// </summary>
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Checks if the current platform is Linux.
    /// </summary>
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>
    /// Checks if the current platform is macOS.
    /// </summary>
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>
    /// Gets the appropriate command executable extension for the current platform.
    /// </summary>
    /// <returns>Empty string for Unix, ".cmd" or ".exe" for Windows.</returns>
    public static string[] GetExecutableExtensions()
    {
        return IsWindows
            ? [".cmd", ".exe", ".bat", ""]
            : [""];
    }
}
