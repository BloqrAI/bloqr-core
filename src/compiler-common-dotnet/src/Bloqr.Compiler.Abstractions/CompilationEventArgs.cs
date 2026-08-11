namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Event arguments for compilation events.
/// </summary>
public class CompilationEventArgs : EventArgs
{
    /// <summary>
    /// Gets the compiler options used for this compilation.
    /// </summary>
    public CompilerOptions Options { get; }

    /// <summary>
    /// Gets the timestamp when the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    public CompilationEventArgs(CompilerOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }
}