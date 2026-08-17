namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Defines the supported source format types for filter lists.
/// </summary>
public enum SourceType
{
    /// <summary>
    /// AdGuard/uBlock Origin adblock filter syntax.
    /// </summary>
    /// <remarks>
    /// This is the default format. Rules use the standard adblock filter syntax
    /// including element hiding rules, network rules, and modifiers.
    /// Example: ||example.com^
    /// </remarks>
    Adblock,

    /// <summary>
    /// /etc/hosts file format.
    /// </summary>
    /// <remarks>
    /// Traditional hosts file format where each line contains an IP address
    /// followed by domain names.
    /// Example: 0.0.0.0 example.com
    /// </remarks>
    Hosts
}