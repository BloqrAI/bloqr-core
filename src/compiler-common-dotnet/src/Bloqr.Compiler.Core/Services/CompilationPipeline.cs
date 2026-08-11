namespace Bloqr.Compiler.Core.Services;

/// <summary>
/// Default implementation of the compilation pipeline.
/// </summary>
public class CompilationPipeline : ICompilationPipeline
{
    private readonly CompilationDelegate _pipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationPipeline"/> class.
    /// </summary>
    /// <param name="builder">The pipeline builder.</param>
    public CompilationPipeline(ICompilationPipelineBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _pipeline = builder.Build();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationPipeline"/> class
    /// with a pre-built pipeline delegate.
    /// </summary>
    /// <param name="pipeline">The pipeline delegate.</param>
    public CompilationPipeline(CompilationDelegate pipeline)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    }

    /// <inheritdoc/>
    public async Task<CompilerResult> ExecuteAsync(
        CompilerOptions options,
        CancellationToken cancellationToken = default)
    {
        var context = new CompilationContext
        {
            Options = options,
            CancellationToken = cancellationToken
        };

        await _pipeline(context);

        if (context.IsCancelled)
        {
            return new CompilerResult
            {
                Success = false,
                ErrorMessage = context.CancellationReason ?? "Compilation was cancelled"
            };
        }

        return context.Result ?? new CompilerResult
        {
            Success = false,
            ErrorMessage = "No result produced from compilation pipeline"
        };
    }
}