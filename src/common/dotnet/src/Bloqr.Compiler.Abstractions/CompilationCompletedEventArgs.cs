namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Event arguments for when compilation completes.
/// </summary>
public class CompilationCompletedEventArgs : CompilationEventArgs
{
    /// <summary>
    /// Gets the compilation result.
    /// </summary>
    public CompilerResult Result { get; }

    /// <summary>
    /// Gets the compilation duration.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationCompletedEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    /// <param name="result">The compilation result.</param>
    /// <param name="duration">The compilation duration.</param>
    public CompilationCompletedEventArgs(
        CompilerOptions options,
        CompilerResult result,
        TimeSpan duration)
        : base(options)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
        Duration = duration;
    }
}