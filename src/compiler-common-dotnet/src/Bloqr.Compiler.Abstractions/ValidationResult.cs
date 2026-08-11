namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Represents a validation error in the configuration.
/// </summary>
/// <param name="Field">The field or path where the error occurred.</param>
/// <param name="Message">A description of the validation error.</param>
public record ValidationError(string Field, string Message);

/// <summary>
/// Represents the result of configuration validation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets whether the configuration is valid.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public List<ValidationError> Errors { get; } = [];

    /// <summary>
    /// Gets the list of validation warnings (non-fatal issues).
    /// </summary>
    public List<ValidationError> Warnings { get; } = [];

    /// <summary>
    /// Adds an error to the validation result.
    /// </summary>
    public void AddError(string field, string message)
    {
        Errors.Add(new ValidationError(field, message));
    }

    /// <summary>
    /// Adds a warning to the validation result.
    /// </summary>
    public void AddWarning(string field, string message)
    {
        Warnings.Add(new ValidationError(field, message));
    }
}
