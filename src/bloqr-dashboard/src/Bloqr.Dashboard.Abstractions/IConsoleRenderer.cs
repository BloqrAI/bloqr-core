namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Interface for console rendering operations. Enables decoupling menu/service logic from
/// any specific console rendering library (Spectre.Console today, a future WPF host later).
/// </summary>
public interface IConsoleRenderer
{
    /// <summary>
    /// Writes a line of text to the console.
    /// </summary>
    /// <param name="text">The text to write.</param>
    void WriteLine(string text);

    /// <summary>
    /// Writes text to the console without a newline.
    /// </summary>
    /// <param name="text">The text to write.</param>
    void Write(string text);

    /// <summary>
    /// Writes a blank line to the console.
    /// </summary>
    void WriteLine();

    /// <summary>
    /// Writes a styled text line to the console.
    /// </summary>
    /// <param name="text">The text to write.</param>
    /// <param name="style">The style to apply.</param>
    void WriteStyled(string text, TextStyle style);

    /// <summary>
    /// Renders a table to the console.
    /// </summary>
    /// <param name="table">The table to render.</param>
    void RenderTable(ConsoleTable table);

    /// <summary>
    /// Renders a panel (boxed content) to the console.
    /// </summary>
    /// <param name="content">The panel content.</param>
    /// <param name="title">Optional panel title.</param>
    void RenderPanel(string content, string? title = null);

    /// <summary>
    /// Renders a rule (horizontal separator line) to the console.
    /// </summary>
    /// <param name="title">Optional title for the rule.</param>
    void RenderRule(string? title = null);

    /// <summary>
    /// Clears the console.
    /// </summary>
    void Clear();

    /// <summary>
    /// Displays a status spinner while an asynchronous operation runs.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="status">The status message.</param>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>The operation result.</returns>
    Task<T> StatusAsync<T>(string status, Func<Task<T>> operation);

    /// <summary>
    /// Displays a status spinner while an asynchronous operation runs.
    /// </summary>
    /// <param name="status">The status message.</param>
    /// <param name="operation">The operation to execute.</param>
    Task StatusAsync(string status, Func<Task> operation);

    /// <summary>
    /// Displays a progress bar for an asynchronous operation.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="description">The progress description.</param>
    /// <param name="operation">The operation, which reports fractional (0.0-1.0) progress.</param>
    /// <returns>The operation result.</returns>
    Task<T> ProgressAsync<T>(string description, Func<IProgress<double>, Task<T>> operation);

    /// <summary>
    /// Displays a live, multi-task progress session for an asynchronous operation - per-stage
    /// progress bars, overall progress, and free-form status lines can all be shown at once via
    /// the returned <see cref="ILiveProgressContext"/>, unlike the single-bar <see cref="ProgressAsync{T}"/>.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation, given the live progress context to drive.</param>
    /// <returns>The operation result.</returns>
    Task<T> LiveProgressAsync<T>(Func<ILiveProgressContext, Task<T>> operation);
}
