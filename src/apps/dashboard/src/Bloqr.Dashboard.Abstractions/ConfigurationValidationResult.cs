namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// The result of validating a configuration document against its JSON Schema.
/// </summary>
/// <param name="IsValid">Whether the document is valid.</param>
/// <param name="Errors">Schema validation error messages, empty when <paramref name="IsValid"/> is <c>true</c>.</param>
public sealed record ConfigurationValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    /// <summary>
    /// A successful, error-free validation result.
    /// </summary>
    public static ConfigurationValidationResult Success { get; } = new(true, []);
}
