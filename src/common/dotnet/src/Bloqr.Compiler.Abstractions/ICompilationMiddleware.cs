namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Interface for middleware components in the compilation pipeline.
/// </summary>
public interface ICompilationMiddleware
{
    /// <summary>
    /// Gets the execution order for this middleware. Lower values execute first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Processes the compilation context and optionally calls the next middleware.
    /// </summary>
    /// <param name="context">The compilation context.</param>
    /// <param name="next">The next middleware in the pipeline.</param>
    Task InvokeAsync(CompilationContext context, CompilationDelegate next);
}