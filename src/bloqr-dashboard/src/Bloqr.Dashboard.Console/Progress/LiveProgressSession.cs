namespace Bloqr.Dashboard.Console.Progress;

/// <summary>
/// Holds the <see cref="ILiveProgressContext"/> for whichever compile is currently running, if
/// any, so <see cref="CompilationProgressEventHandler"/> can find it.
/// </summary>
/// <remarks>
/// Deliberately a plain shared field behind a lock rather than an <c>AsyncLocal&lt;T&gt;</c>:
/// several of the events this handler cares about (<c>ChunkCompleted</c>, <c>ChunksMerged</c>,
/// <c>CompilationCompleted</c>) are processed on <see cref="Bloqr.Compiler.Core.Services.QueuedCompilationEventDispatcher"/>'s
/// single long-lived background consumer task (#274) when that decorator is registered. That
/// task's execution context - and therefore any <c>AsyncLocal</c> value flowing through it - was
/// captured once when the task started, not per dequeued item, so an <c>AsyncLocal</c> set by
/// <c>CompileMenuService</c> around a specific compile would never be visible inside the queued
/// handler calls for that compile. This Dashboard only ever runs one compile at a time, so a
/// single shared "current session" is both correct and simpler.
/// </remarks>
public sealed class LiveProgressSession
{
    private readonly Lock _lock = new();
    private ILiveProgressContext? _current;

    /// <summary>
    /// Gets or sets the currently active live progress context, or <c>null</c> when no compile
    /// is running with a live progress display attached.
    /// </summary>
    public ILiveProgressContext? Current
    {
        get { lock (_lock) { return _current; } }
        set { lock (_lock) { _current = value; } }
    }
}
