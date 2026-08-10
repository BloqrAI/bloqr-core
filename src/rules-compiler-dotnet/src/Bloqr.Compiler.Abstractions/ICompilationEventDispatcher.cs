namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Interface for the compilation event dispatcher.
/// Supports zero-trust validation with events at each compilation stage.
/// </summary>
public interface ICompilationEventDispatcher
{
    /// <summary>
    /// Raises the compilation starting event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseCompilationStartingAsync(
        CompilationStartedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises the configuration loaded event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseConfigurationLoadedAsync(
        ConfigurationLoadedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises a validation checkpoint event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseValidationAsync(
        ValidationEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises the source loading event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseSourceLoadingAsync(
        SourceLoadingEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises the source loaded event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseSourceLoadedAsync(
        SourceLoadedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises the file lock acquired event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseFileLockAcquiredAsync(
        FileLockAcquiredEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises the file lock released event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseFileLockReleasedAsync(
        FileLockReleasedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises the file lock failed event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseFileLockFailedAsync(
        FileLockFailedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises the chunk started event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseChunkStartedAsync(
        ChunkStartedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises the chunk completed event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseChunkCompletedAsync(
        ChunkCompletedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises the chunks merging event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseChunksMergingAsync(
        ChunksMergingEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises the chunks merged event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseChunksMergedAsync(
        ChunksMergedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises the compilation completed event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseCompilationCompletedAsync(
        CompilationCompletedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises the compilation error event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseCompilationErrorAsync(
        CompilationErrorEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises the hash computed event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseHashComputedAsync(
        HashComputedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises the hash verified event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseHashVerifiedAsync(
        HashVerifiedEventArgs args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises the hash mismatch event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RaiseHashMismatchAsync(
        HashMismatchEventArgs args,
        CancellationToken cancellationToken = default);
}
