namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Interface for plugins that validate rules.
/// </summary>
public interface IRuleValidationPlugin : IPlugin
{
    /// <summary>
    /// Validates the compiled rules.
    /// </summary>
    /// <param name="rules">The rules to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with any issues found.</returns>
    Task<PluginValidationResult> ValidateAsync(
        IEnumerable<string> rules,
        CancellationToken cancellationToken = default);
}