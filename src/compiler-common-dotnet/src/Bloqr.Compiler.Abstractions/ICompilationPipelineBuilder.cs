namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Interface for building and executing the compilation pipeline.
/// </summary>
public interface ICompilationPipelineBuilder
{
    /// <summary>
    /// Adds a middleware component to the pipeline.
    /// </summary>
    /// <typeparam name="T">The middleware type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    ICompilationPipelineBuilder Use<T>() where T : ICompilationMiddleware;

    /// <summary>
    /// Adds a middleware component to the pipeline.
    /// </summary>
    /// <param name="middleware">The middleware instance.</param>
    /// <returns>The builder for chaining.</returns>
    ICompilationPipelineBuilder Use(ICompilationMiddleware middleware);

    /// <summary>
    /// Adds a middleware delegate to the pipeline.
    /// </summary>
    /// <param name="middleware">The middleware delegate.</param>
    /// <returns>The builder for chaining.</returns>
    ICompilationPipelineBuilder Use(Func<CompilationContext, CompilationDelegate, Task> middleware);

    /// <summary>
    /// Builds the pipeline and returns the entry delegate.
    /// </summary>
    CompilationDelegate Build();
}