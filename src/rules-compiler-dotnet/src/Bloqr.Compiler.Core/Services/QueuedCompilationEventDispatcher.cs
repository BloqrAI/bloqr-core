namespace Bloqr.Compiler.Core.Services;

/// <summary>
/// Decorates an <see cref="ICompilationEventDispatcher"/>, deferring the events that don't
/// influence the compilation pipeline's own control flow onto a background queue - so a slow or
/// misbehaving handler (e.g. writing structured logs, updating a progress UI) never blocks the
/// pipeline that raised the event - per the epic's "use queueing, Polly, etc for durability" ask.
/// </summary>
/// <remarks>
/// Events whose <c>EventArgs</c> the pipeline inspects afterward to decide whether to continue
/// (<c>Cancel</c>/<c>Abort</c>/<c>Skip</c>), or that the pipeline itself must observe failing
/// (via a rethrown exception), are passed straight through to the decorated dispatcher
/// synchronously - queueing those would silently break the zero-trust abort semantics documented
/// in <c>docs/event-pipeline.md</c>. Only the "fire and forget" events - the ones
/// <see cref="CompilationEventDispatcher"/> itself already logs-and-continues on handler failure
/// for - are queued.
/// </remarks>
public sealed class QueuedCompilationEventDispatcher : ICompilationEventDispatcher, IAsyncDisposable
{
    private readonly ICompilationEventDispatcher _inner;
    private readonly ILogger<QueuedCompilationEventDispatcher> _logger;
    private readonly Channel<Func<Task>> _queue;
    private readonly Task _processingTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedCompilationEventDispatcher"/> class.
    /// </summary>
    /// <param name="inner">The dispatcher to decorate.</param>
    /// <param name="logger">The logger instance.</param>
    public QueuedCompilationEventDispatcher(
        ICompilationEventDispatcher inner,
        ILogger<QueuedCompilationEventDispatcher> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _queue = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions { SingleReader = true });
        _processingTask = Task.Run(ProcessQueueAsync);
    }

    /// <inheritdoc/>
    public Task RaiseCompilationStartingAsync(
        CompilationStartedEventArgs args, CancellationToken cancellationToken = default)
        => _inner.RaiseCompilationStartingAsync(args, cancellationToken);

    /// <inheritdoc/>
    public Task RaiseConfigurationLoadedAsync(
        ConfigurationLoadedEventArgs args, CancellationToken cancellationToken = default)
        => _inner.RaiseConfigurationLoadedAsync(args, cancellationToken);

    /// <inheritdoc/>
    public Task RaiseValidationAsync(
        ValidationEventArgs args, CancellationToken cancellationToken = default)
        => _inner.RaiseValidationAsync(args, cancellationToken);

    /// <inheritdoc/>
    public Task RaiseSourceLoadingAsync(
        SourceLoadingEventArgs args, CancellationToken cancellationToken = default)
        => _inner.RaiseSourceLoadingAsync(args, cancellationToken);

    /// <inheritdoc/>
    public Task RaiseChunkStartedAsync(
        ChunkStartedEventArgs args, CancellationToken cancellationToken = default)
        => _inner.RaiseChunkStartedAsync(args, cancellationToken);

    /// <inheritdoc/>
    public Task RaiseChunksMergingAsync(
        ChunksMergingEventArgs args, CancellationToken cancellationToken = default)
        => _inner.RaiseChunksMergingAsync(args, cancellationToken);

    /// <inheritdoc/>
    public Task RaiseHashMismatchAsync(
        HashMismatchEventArgs args, CancellationToken cancellationToken = default)
        => _inner.RaiseHashMismatchAsync(args, cancellationToken);

    /// <inheritdoc/>
    public Task RaiseSourceLoadedAsync(
        SourceLoadedEventArgs args, CancellationToken cancellationToken = default)
        => Enqueue(ct => _inner.RaiseSourceLoadedAsync(args, ct), cancellationToken);

    /// <inheritdoc/>
    public Task RaiseFileLockAcquiredAsync(
        FileLockAcquiredEventArgs args, CancellationToken cancellationToken = default)
        => Enqueue(ct => _inner.RaiseFileLockAcquiredAsync(args, ct), cancellationToken);

    /// <inheritdoc/>
    public Task RaiseFileLockReleasedAsync(
        FileLockReleasedEventArgs args, CancellationToken cancellationToken = default)
        => Enqueue(ct => _inner.RaiseFileLockReleasedAsync(args, ct), cancellationToken);

    /// <inheritdoc/>
    public Task RaiseFileLockFailedAsync(
        FileLockFailedEventArgs args, CancellationToken cancellationToken = default)
        => Enqueue(ct => _inner.RaiseFileLockFailedAsync(args, ct), cancellationToken);

    /// <inheritdoc/>
    public Task RaiseChunkCompletedAsync(
        ChunkCompletedEventArgs args, CancellationToken cancellationToken = default)
        => Enqueue(ct => _inner.RaiseChunkCompletedAsync(args, ct), cancellationToken);

    /// <inheritdoc/>
    public Task RaiseChunksMergedAsync(
        ChunksMergedEventArgs args, CancellationToken cancellationToken = default)
        => Enqueue(ct => _inner.RaiseChunksMergedAsync(args, ct), cancellationToken);

    /// <inheritdoc/>
    public Task RaiseCompilationCompletedAsync(
        CompilationCompletedEventArgs args, CancellationToken cancellationToken = default)
        => Enqueue(ct => _inner.RaiseCompilationCompletedAsync(args, ct), cancellationToken);

    /// <inheritdoc/>
    public Task RaiseCompilationErrorAsync(
        CompilationErrorEventArgs args, CancellationToken cancellationToken = default)
        => Enqueue(ct => _inner.RaiseCompilationErrorAsync(args, ct), cancellationToken);

    /// <inheritdoc/>
    public Task RaiseHashComputedAsync(
        HashComputedEventArgs args, CancellationToken cancellationToken = default)
        => Enqueue(ct => _inner.RaiseHashComputedAsync(args, ct), cancellationToken);

    /// <inheritdoc/>
    public Task RaiseHashVerifiedAsync(
        HashVerifiedEventArgs args, CancellationToken cancellationToken = default)
        => Enqueue(ct => _inner.RaiseHashVerifiedAsync(args, ct), cancellationToken);

    /// <summary>
    /// Queues <paramref name="raiseAsync"/> for background processing and returns immediately.
    /// <paramref name="cancellationToken"/> is captured for the deferred call - the enqueue
    /// itself never blocks or throws for cancellation, matching the "don't block the pipeline"
    /// intent.
    /// </summary>
    private Task Enqueue(Func<CancellationToken, Task> raiseAsync, CancellationToken cancellationToken)
    {
        if (!_queue.Writer.TryWrite(() => raiseAsync(cancellationToken)))
        {
            _logger.LogWarning("Compilation event queue is closed; dropping a queued event");
        }

        return Task.CompletedTask;
    }

    private async Task ProcessQueueAsync()
    {
        await foreach (var work in _queue.Reader.ReadAllAsync())
        {
            try
            {
                await work().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception processing a queued compilation event");
            }
        }
    }

    /// <summary>
    /// Completes the queue and waits for all pending events to finish processing.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _queue.Writer.Complete();
        await _processingTask.ConfigureAwait(false);
    }
}
