namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Event arguments for when an error occurs during compilation.
/// </summary>
public class CompilationErrorEventArgs : CompilationEventArgs
{
    /// <summary>
    /// Gets the exception that occurred.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the error was handled.
    /// </summary>
    public bool Handled { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationErrorEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    /// <param name="exception">The exception that occurred.</param>
    public CompilationErrorEventArgs(CompilerOptions options, Exception exception)
        : base(options)
    {
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
    }
}