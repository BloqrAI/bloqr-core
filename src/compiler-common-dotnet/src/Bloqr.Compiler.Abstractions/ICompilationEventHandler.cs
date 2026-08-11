namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Interface for handling compilation lifecycle events.
/// Supports zero-trust validation at each stage.
/// </summary>
public interface ICompilationEventHandler
{
    /// <summary>
    /// Called when compilation is about to start.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnCompilationStartingAsync(
        CompilationStartedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when configuration has been loaded.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnConfigurationLoadedAsync(
        ConfigurationLoadedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a validation checkpoint is reached.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnValidationAsync(
        ValidationEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a source is about to be loaded.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnSourceLoadingAsync(
        SourceLoadingEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a source has been loaded.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnSourceLoadedAsync(
        SourceLoadedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a file lock is acquired.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnFileLockAcquiredAsync(
        FileLockAcquiredEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a file lock is released.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnFileLockReleasedAsync(
        FileLockReleasedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a file lock fails.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnFileLockFailedAsync(
        FileLockFailedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a chunk is about to be processed.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnChunkStartedAsync(
        ChunkStartedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a chunk has completed processing.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnChunkCompletedAsync(
        ChunkCompletedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when chunks are about to be merged.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnChunksMergingAsync(
        ChunksMergingEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when chunks have been merged.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnChunksMergedAsync(
        ChunksMergedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when compilation has completed successfully.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnCompilationCompletedAsync(
        CompilationCompletedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when an error occurs during compilation.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnCompilationErrorAsync(
        CompilationErrorEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a hash is computed for any item.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnHashComputedAsync(
        HashComputedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a hash is verified successfully.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnHashVerifiedAsync(
        HashVerifiedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a hash verification fails.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnHashMismatchAsync(
        HashMismatchEventArgs args,
        CancellationToken cancellationToken = default);
}
