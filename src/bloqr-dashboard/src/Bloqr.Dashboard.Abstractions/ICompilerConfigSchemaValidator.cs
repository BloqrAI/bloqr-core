namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Validates a compiler config against <c>schemas/compiler-config.schema.json</c>. Distinct from
/// <c>Bloqr.Compiler.Core</c>'s hand-rolled <c>ConfigurationValidator</c> (business rules like
/// "at least one source"): this checks structural JSON Schema conformance — the same kind of
/// check <see cref="IDashboardConfigurationStore"/> already performs for the Dashboard's own
/// config, applied here to the compiler configs the wizard (#268) generates.
/// </summary>
public interface ICompilerConfigSchemaValidator
{
    /// <summary>
    /// Validates a compiler configuration against the schema.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <returns>The validation result.</returns>
    ConfigurationValidationResult Validate(CompilerConfiguration configuration);
}
