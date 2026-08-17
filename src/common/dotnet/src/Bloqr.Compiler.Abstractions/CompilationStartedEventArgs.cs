namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Event arguments for when compilation starts.
/// </summary>
public class CompilationStartedEventArgs : CompilationEventArgs
{
    /// <summary>
    /// Gets or sets a value indicating whether to cancel the compilation.
    /// </summary>
    public bool Cancel { get; set; }

    /// <summary>
    /// Gets or sets the reason for cancellation.
    /// </summary>
    public string? CancelReason { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationStartedEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    public CompilationStartedEventArgs(CompilerOptions options) : base(options) { }
}