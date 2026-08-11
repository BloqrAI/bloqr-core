namespace Bloqr.Compiler.Core.Services;

/// <summary>
/// Default implementation of the compilation pipeline builder.
/// </summary>
public class CompilationPipelineBuilder : ICompilationPipelineBuilder
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly List<Func<CompilationDelegate, CompilationDelegate>> _middlewareFactories = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationPipelineBuilder"/> class.
    /// </summary>
    /// <param name="serviceProvider">Optional service provider for resolving middleware.</param>
    public CompilationPipelineBuilder(IServiceProvider? serviceProvider = null)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public ICompilationPipelineBuilder Use<T>() where T : ICompilationMiddleware
    {
        _middlewareFactories.Add(next =>
        {
            var middleware = _serviceProvider != null
                ? ActivatorUtilities.CreateInstance<T>(_serviceProvider)
                : Activator.CreateInstance<T>();

            return context => middleware.InvokeAsync(context, next);
        });

        return this;
    }

    /// <inheritdoc/>
    public ICompilationPipelineBuilder Use(ICompilationMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);

        _middlewareFactories.Add(next => context => middleware.InvokeAsync(context, next));
        return this;
    }

    /// <inheritdoc/>
    public ICompilationPipelineBuilder Use(Func<CompilationContext, CompilationDelegate, Task> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);

        _middlewareFactories.Add(next => context => middleware(context, next));
        return this;
    }

    /// <inheritdoc/>
    public CompilationDelegate Build()
    {
        // Terminal middleware - does nothing, end of pipeline
        CompilationDelegate pipeline = _ => Task.CompletedTask;

        // Build pipeline in reverse order
        for (var i = _middlewareFactories.Count - 1; i >= 0; i--)
        {
            pipeline = _middlewareFactories[i](pipeline);
        }

        return pipeline;
    }
}