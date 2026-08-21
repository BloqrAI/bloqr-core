namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// A live, multi-task progress display session, active for the duration of a single
/// <see cref="IConsoleRenderer.LiveProgressAsync{T}"/> call. Mirrors Spectre.Console's own
/// <c>ProgressContext</c>/<c>ProgressTask</c> API surface, kept rendering-library-agnostic per
/// the same seam <see cref="IConsoleRenderer"/> already provides - only
/// <c>Rendering/Spectre*.cs</c> may reference Spectre.Console directly.
/// </summary>
public interface ILiveProgressContext
{
    /// <summary>
    /// Adds a new task to the live display and returns a handle for updating it.
    /// </summary>
    /// <param name="description">The task's display description.</param>
    /// <param name="maxValue">The value <see cref="ILiveProgressTask.Value"/> represents 100% completion.</param>
    ILiveProgressTask AddTask(string description, double maxValue = 100);
}

/// <summary>
/// A single task within a <see cref="ILiveProgressContext"/>.
/// </summary>
public interface ILiveProgressTask
{
    /// <summary>
    /// Gets or sets the task's display description.
    /// </summary>
    string Description { get; set; }

    /// <summary>
    /// Gets or sets the current progress value, from 0 to <see cref="MaxValue"/>.
    /// </summary>
    double Value { get; set; }

    /// <summary>
    /// Gets the value that represents 100% completion.
    /// </summary>
    double MaxValue { get; }

    /// <summary>
    /// Gets a value indicating whether this task has reached <see cref="MaxValue"/>.
    /// </summary>
    bool IsFinished { get; }

    /// <summary>
    /// Advances <see cref="Value"/> by the given amount.
    /// </summary>
    /// <param name="amount">The amount to advance by.</param>
    void Increment(double amount);

    /// <summary>
    /// Sets <see cref="Value"/> to <see cref="MaxValue"/>.
    /// </summary>
    void Complete();
}
