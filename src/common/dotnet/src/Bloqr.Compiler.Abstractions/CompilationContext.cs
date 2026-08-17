namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Represents context passed through the compilation pipeline.
/// </summary>
public class CompilationContext
{
    /// <summary>
    /// Gets or sets the compiler options.
    /// </summary>
    public CompilerOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the loaded configuration.
    /// </summary>
    public CompilerConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets or sets the compilation result.
    /// </summary>
    public CompilerResult? Result { get; set; }

    /// <summary>
    /// Gets the items collection for passing data between middleware.
    /// </summary>
    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets a value indicating whether compilation should be cancelled.
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    /// Gets or sets the reason for cancellation.
    /// </summary>
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Gets or sets the cancellation token.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }
}