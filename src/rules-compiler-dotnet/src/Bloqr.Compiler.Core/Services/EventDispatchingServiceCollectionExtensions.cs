namespace Bloqr.Compiler.Core.Services;

/// <summary>
/// Extension methods for opting a service collection into background-queued compilation event
/// dispatching (<see cref="QueuedCompilationEventDispatcher"/>).
/// </summary>
public static class EventDispatchingServiceCollectionExtensions
{
    /// <summary>
    /// Decorates <see cref="ICompilationEventDispatcher"/> with <see cref="QueuedCompilationEventDispatcher"/>,
    /// so fire-and-forget events (source loaded, file lock, chunk completed/merged, compilation
    /// completed/error, hash computed/verified) are processed on a background queue instead of
    /// blocking the compilation pipeline that raised them.
    /// </summary>
    /// <remarks>
    /// Call this <em>after</em> <c>AddRulesCompiler()</c> (or any other registration of
    /// <see cref="ICompilationEventDispatcher"/>) - it registers a new
    /// <see cref="ICompilationEventDispatcher"/> that resolution returns instead of the plain
    /// <see cref="CompilationEventDispatcher"/>, and the last registration wins for resolution.
    /// This is deliberately opt-in rather than the default: <see cref="CompilationEventDispatcher"/>
    /// on its own remains fully synchronous, which existing tests and callers depend on.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddQueuedCompilationEventDispatching(this IServiceCollection services)
    {
        services.AddSingleton<ICompilationEventDispatcher>(provider =>
        {
            var inner = ActivatorUtilities.CreateInstance<CompilationEventDispatcher>(provider);
            var logger = provider.GetRequiredService<ILogger<QueuedCompilationEventDispatcher>>();
            return new QueuedCompilationEventDispatcher(inner, logger);
        });

        return services;
    }
}
