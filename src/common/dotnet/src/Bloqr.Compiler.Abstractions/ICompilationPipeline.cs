namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Interface for executing the compilation pipeline.
/// </summary>
public interface ICompilationPipeline
{
    /// <summary>
    /// Executes the compilation pipeline.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The compilation result.</returns>
    Task<CompilerResult> ExecuteAsync(
        CompilerOptions options,
        CancellationToken cancellationToken = default);
}