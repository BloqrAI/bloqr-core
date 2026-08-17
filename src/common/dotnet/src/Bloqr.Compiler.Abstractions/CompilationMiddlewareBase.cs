namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Base class for implementing compilation middleware.
/// </summary>
public abstract class CompilationMiddlewareBase : ICompilationMiddleware
{
    /// <inheritdoc/>
    public virtual int Order => 0;

    /// <inheritdoc/>
    public abstract Task InvokeAsync(CompilationContext context, CompilationDelegate next);

    /// <summary>
    /// Cancels the compilation with the specified reason.
    /// </summary>
    /// <param name="context">The compilation context.</param>
    /// <param name="reason">The cancellation reason.</param>
    protected static void Cancel(CompilationContext context, string reason)
    {
        context.IsCancelled = true;
        context.CancellationReason = reason;
    }
}