namespace Bloqr.Dashboard.Core.Configuration;

/// <summary>
/// Default implementation of <see cref="ICompilerConfigSchemaValidator"/>. Delegates the actual
/// schema evaluation to <c>Bloqr.Compiler.Core</c>'s <c>CompilerConfigJsonSchemaValidator</c> (#258)
/// - the single source of truth for compiler-config schema validation, since the compiler pipeline
/// itself needs the same check and the schema shouldn't be embedded and evaluated twice. This type
/// only adapts that result to the Dashboard's own <see cref="ConfigurationValidationResult"/> shape.
/// </summary>
public sealed class CompilerConfigSchemaValidator : ICompilerConfigSchemaValidator
{
    /// <inheritdoc />
    public ConfigurationValidationResult Validate(CompilerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var result = new ValidationResult();
        CompilerConfigJsonSchemaValidator.Validate(configuration, result);

        if (result.IsValid)
        {
            return ConfigurationValidationResult.Success;
        }

        var errors = result.Errors.Select(e => $"{e.Field}: {e.Message}").ToList();
        return new ConfigurationValidationResult(false, errors);
    }
}
