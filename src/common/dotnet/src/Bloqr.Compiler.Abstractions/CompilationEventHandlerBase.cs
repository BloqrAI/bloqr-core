namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Base class for implementing compilation event handlers.
/// Override only the methods you need.
/// </summary>
public abstract class CompilationEventHandlerBase : ICompilationEventHandler
{
    /// <inheritdoc/>
    public virtual Task OnCompilationStartingAsync(
        CompilationStartedEventArgs args,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnConfigurationLoadedAsync(
        ConfigurationLoadedEventArgs args,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnValidationAsync(
        ValidationEventArgs args,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnSourceLoadingAsync(
        SourceLoadingEventArgs args,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnSourceLoadedAsync(
        SourceLoadedEventArgs args,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnFileLockAcquiredAsync(
        FileLockAcquiredEventArgs args,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnFileLockReleasedAsync(
        FileLockReleasedEventArgs args,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnFileLockFailedAsync(
        FileLockFailedEventArgs args,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnChunkStartedAsync(
        ChunkStartedEventArgs args,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnChunkCompletedAsync(
        ChunkCompletedEventArgs args,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnChunksMergingAsync(
        ChunksMergingEventArgs args,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnChunksMergedAsync(
        ChunksMergedEventArgs args,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnCompilationCompletedAsync(
        CompilationCompletedEventArgs args,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnCompilationErrorAsync(
        CompilationErrorEventArgs args,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnHashComputedAsync(
        HashComputedEventArgs args,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnHashVerifiedAsync(
        HashVerifiedEventArgs args,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task OnHashMismatchAsync(
        HashMismatchEventArgs args,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
