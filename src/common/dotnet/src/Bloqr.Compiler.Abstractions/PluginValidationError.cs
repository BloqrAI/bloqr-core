namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Represents a validation error or warning from a plugin.
/// </summary>
public class PluginValidationError
{
    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the line number where the error occurred, if applicable.
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// Gets or sets the rule that caused the error, if applicable.
    /// </summary>
    public string? Rule { get; set; }
}