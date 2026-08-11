namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Result from a plugin validation operation.
/// </summary>
public class PluginValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether validation passed.
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IList<PluginValidationError> Errors { get; } = [];

    /// <summary>
    /// Gets the validation warnings.
    /// </summary>
    public IList<PluginValidationError> Warnings { get; } = [];

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static PluginValidationResult Success() => new();

    /// <summary>
    /// Creates a failed validation result with the specified errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public static PluginValidationResult Failure(params PluginValidationError[] errors)
    {
        var result = new PluginValidationResult { IsValid = false };
        foreach (var error in errors)
        {
            result.Errors.Add(error);
        }
        return result;
    }
}