namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Interface for plugins that process rules during compilation.
/// </summary>
public interface IRuleTransformationPlugin : IPlugin
{
    /// <summary>
    /// Gets the execution order for this transformation. Lower values execute first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Transforms the rules during compilation.
    /// </summary>
    /// <param name="rules">The current set of rules.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transformed rules.</returns>
    Task<IEnumerable<string>> TransformAsync(
        IEnumerable<string> rules,
        CancellationToken cancellationToken = default);
}