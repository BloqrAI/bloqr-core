namespace Bloqr.Compiler.Core.Services;

/// <summary>
/// Default implementation of the compilation event dispatcher.
/// Supports zero-trust validation at each compilation stage.
/// </summary>
public class CompilationEventDispatcher : ICompilationEventDispatcher
{
    // Transient-fault retry around each individual handler invocation (not the dispatch loop as
    // a whole), per the epic's "use queueing, Polly, etc for durability" ask. Handlers can be
    // I/O-bound (e.g. a logging handler writing to disk, a future handler downloading a source),
    // so a handful of retries with backoff+jitter absorbs transient failures without masking
    // genuine bugs - OperationCanceledException is deliberately excluded so cancellation still
    // propagates immediately instead of being retried.
    private static readonly ResiliencePipeline HandlerRetryPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder()
                .Handle<IOException>()
                .Handle<TimeoutException>()
                .Handle<HttpRequestException>(),
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(200),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
        })
        .Build();

    private readonly ILogger<CompilationEventDispatcher> _logger;
    private readonly IEnumerable<ICompilationEventHandler> _handlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationEventDispatcher"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="handlers">The registered event handlers.</param>
    public CompilationEventDispatcher(
        ILogger<CompilationEventDispatcher> logger,
        IEnumerable<ICompilationEventHandler> handlers)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
    }

    /// <summary>
    /// Invokes a single handler through <see cref="HandlerRetryPipeline"/>, so a transient
    /// exception (e.g. a locked log file) is retried before the caller's existing
    /// throw-vs-swallow handling for that event type takes over.
    /// </summary>
    private static Task InvokeHandlerAsync(
        Func<CancellationToken, Task> invokeHandler, CancellationToken cancellationToken)
        => HandlerRetryPipeline.ExecuteAsync(ct => new ValueTask(invokeHandler(ct)), cancellationToken).AsTask();

    /// <inheritdoc/>
    public async Task RaiseCompilationStartingAsync(
        CompilationStartedEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Raising CompilationStarting event to {Count} handlers", _handlers.Count());

        foreach (var handler in _handlers)
        {
            try
            {
                await InvokeHandlerAsync(ct => handler.OnCompilationStartingAsync(args, ct), cancellationToken);
                if (args.Cancel)
                {
                    _logger.LogInformation(
                        "Compilation cancelled by handler {Handler}: {Reason}",
                        handler.GetType().Name,
                        args.CancelReason);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event handler {Handler} during CompilationStarting",
                    handler.GetType().Name);
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public async Task RaiseConfigurationLoadedAsync(
        ConfigurationLoadedEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Raising ConfigurationLoaded event to {Count} handlers", _handlers.Count());

        foreach (var handler in _handlers)
        {
            try
            {
                await InvokeHandlerAsync(ct => handler.OnConfigurationLoadedAsync(args, ct), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event handler {Handler} during ConfigurationLoaded",
                    handler.GetType().Name);
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public async Task RaiseValidationAsync(
        ValidationEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Raising Validation event ({Stage}) to {Count} handlers",
            args.StageName, _handlers.Count());

        foreach (var handler in _handlers)
        {
            try
            {
                await InvokeHandlerAsync(ct => handler.OnValidationAsync(args, ct), cancellationToken);
                if (args.Abort)
                {
                    _logger.LogWarning(
                        "Validation aborted by handler {Handler} at stage {Stage}: {Reason}",
                        handler.GetType().Name,
                        args.StageName,
                        args.AbortReason);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event handler {Handler} during Validation ({Stage})",
                    handler.GetType().Name,
                    args.StageName);
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public async Task RaiseSourceLoadingAsync(
        SourceLoadingEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Raising SourceLoading event ({Index}/{Total}) to {Count} handlers",
            args.SourceIndex + 1, args.TotalSources, _handlers.Count());

        foreach (var handler in _handlers)
        {
            try
            {
                await InvokeHandlerAsync(ct => handler.OnSourceLoadingAsync(args, ct), cancellationToken);
                if (args.Skip)
                {
                    _logger.LogInformation(
                        "Source {Index} skipped by handler {Handler}: {Reason}",
                        args.SourceIndex,
                        handler.GetType().Name,
                        args.SkipReason);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event handler {Handler} during SourceLoading",
                    handler.GetType().Name);
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public async Task RaiseSourceLoadedAsync(
        SourceLoadedEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Raising SourceLoaded event ({Index}/{Total}, Success: {Success}) to {Count} handlers",
            args.SourceIndex + 1, args.TotalSources, args.Success, _handlers.Count());

        foreach (var handler in _handlers)
        {
            try
            {
                await InvokeHandlerAsync(ct => handler.OnSourceLoadedAsync(args, ct), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event handler {Handler} during SourceLoaded",
                    handler.GetType().Name);
                // Don't rethrow - source is already loaded
            }
        }
    }

    /// <inheritdoc/>
    public async Task RaiseFileLockAcquiredAsync(
        FileLockAcquiredEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Raising FileLockAcquired event ({FilePath}, {LockType}) to {Count} handlers",
            args.FilePath, args.LockType, _handlers.Count());

        foreach (var handler in _handlers)
        {
            try
            {
                await InvokeHandlerAsync(ct => handler.OnFileLockAcquiredAsync(args, ct), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event handler {Handler} during FileLockAcquired",
                    handler.GetType().Name);
                // Don't rethrow - lock is already acquired
            }
        }
    }

    /// <inheritdoc/>
    public async Task RaiseFileLockReleasedAsync(
        FileLockReleasedEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Raising FileLockReleased event ({FilePath}) to {Count} handlers",
            args.FilePath, _handlers.Count());

        foreach (var handler in _handlers)
        {
            try
            {
                await InvokeHandlerAsync(ct => handler.OnFileLockReleasedAsync(args, ct), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event handler {Handler} during FileLockReleased",
                    handler.GetType().Name);
                // Don't rethrow - lock is already released
            }
        }
    }

    /// <inheritdoc/>
    public async Task RaiseFileLockFailedAsync(
        FileLockFailedEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Raising FileLockFailed event ({FilePath}) to {Count} handlers",
            args.FilePath, _handlers.Count());

        foreach (var handler in _handlers)
        {
            try
            {
                await InvokeHandlerAsync(ct => handler.OnFileLockFailedAsync(args, ct), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event handler {Handler} during FileLockFailed",
                    handler.GetType().Name);
                // Don't rethrow - lock already failed
            }
        }
    }

    /// <inheritdoc/>
    public async Task RaiseChunkStartedAsync(
        ChunkStartedEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Raising ChunkStarted event ({Index}/{Total}) to {Count} handlers",
            args.Chunk.Index + 1, args.Chunk.Total, _handlers.Count());

        foreach (var handler in _handlers)
        {
            try
            {
                await InvokeHandlerAsync(ct => handler.OnChunkStartedAsync(args, ct), cancellationToken);
                if (args.Skip)
                {
                    _logger.LogInformation(
                        "Chunk {Index} skipped by handler {Handler}: {Reason}",
                        args.Chunk.Index,
                        handler.GetType().Name,
                        args.SkipReason);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event handler {Handler} during ChunkStarted",
                    handler.GetType().Name);
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public async Task RaiseChunkCompletedAsync(
        ChunkCompletedEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Raising ChunkCompleted event ({Index}/{Total}, Success: {Success}) to {Count} handlers",
            args.Chunk.Index + 1, args.Chunk.Total, args.Success, _handlers.Count());

        foreach (var handler in _handlers)
        {
            try
            {
                await InvokeHandlerAsync(ct => handler.OnChunkCompletedAsync(args, ct), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event handler {Handler} during ChunkCompleted",
                    handler.GetType().Name);
                // Don't rethrow - chunk is already completed
            }
        }
    }

    /// <inheritdoc/>
    public async Task RaiseChunksMergingAsync(
        ChunksMergingEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Raising ChunksMerging event ({ChunkCount} chunks, {TotalRules} rules) to {Count} handlers",
            args.ChunkCount, args.TotalRulesBeforeMerge, _handlers.Count());

        foreach (var handler in _handlers)
        {
            try
            {
                await InvokeHandlerAsync(ct => handler.OnChunksMergingAsync(args, ct), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event handler {Handler} during ChunksMerging",
                    handler.GetType().Name);
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public async Task RaiseChunksMergedAsync(
        ChunksMergedEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Raising ChunksMerged event ({FinalRules} rules, {Duplicates} duplicates removed) to {Count} handlers",
            args.FinalRuleCount, args.DuplicatesRemoved, _handlers.Count());

        foreach (var handler in _handlers)
        {
            try
            {
                await InvokeHandlerAsync(ct => handler.OnChunksMergedAsync(args, ct), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event handler {Handler} during ChunksMerged",
                    handler.GetType().Name);
                // Don't rethrow - merge is already completed
            }
        }
    }

    /// <inheritdoc/>
    public async Task RaiseCompilationCompletedAsync(
        CompilationCompletedEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Raising CompilationCompleted event to {Count} handlers", _handlers.Count());

        foreach (var handler in _handlers)
        {
            try
            {
                await InvokeHandlerAsync(ct => handler.OnCompilationCompletedAsync(args, ct), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event handler {Handler} during CompilationCompleted",
                    handler.GetType().Name);
                // Don't rethrow for completion events - compilation already succeeded
            }
        }
    }

    /// <inheritdoc/>
    public async Task RaiseCompilationErrorAsync(
        CompilationErrorEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Raising CompilationError event to {Count} handlers", _handlers.Count());

        foreach (var handler in _handlers)
        {
            try
            {
                await InvokeHandlerAsync(ct => handler.OnCompilationErrorAsync(args, ct), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event handler {Handler} during CompilationError",
                    handler.GetType().Name);
                // Don't rethrow for error events
            }
        }
    }

    /// <inheritdoc/>
    public async Task RaiseHashComputedAsync(
        HashComputedEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Raising HashComputed event ({ItemType}: {ItemIdentifier}) to {Count} handlers",
            args.ItemType, args.ItemIdentifier, _handlers.Count());

        foreach (var handler in _handlers)
        {
            try
            {
                await InvokeHandlerAsync(ct => handler.OnHashComputedAsync(args, ct), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event handler {Handler} during HashComputed",
                    handler.GetType().Name);
                // Don't rethrow - the hash has already been computed
            }
        }
    }

    /// <inheritdoc/>
    public async Task RaiseHashVerifiedAsync(
        HashVerifiedEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Raising HashVerified event ({ItemType}: {ItemIdentifier}) to {Count} handlers",
            args.ItemType, args.ItemIdentifier, _handlers.Count());

        foreach (var handler in _handlers)
        {
            try
            {
                await InvokeHandlerAsync(ct => handler.OnHashVerifiedAsync(args, ct), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event handler {Handler} during HashVerified",
                    handler.GetType().Name);
                // Don't rethrow - verification already succeeded
            }
        }
    }

    /// <inheritdoc/>
    public async Task RaiseHashMismatchAsync(
        HashMismatchEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Raising HashMismatch event ({ItemType}: {ItemIdentifier}) to {Count} handlers",
            args.ItemType, args.ItemIdentifier, _handlers.Count());

        foreach (var handler in _handlers)
        {
            try
            {
                await InvokeHandlerAsync(ct => handler.OnHashMismatchAsync(args, ct), cancellationToken);
                if (args.Abort)
                {
                    _logger.LogWarning(
                        "Hash mismatch for {ItemIdentifier} ({ItemType}): {Reason}",
                        args.ItemIdentifier,
                        args.ItemType,
                        args.AbortReason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in event handler {Handler} during HashMismatch",
                    handler.GetType().Name);
                throw;
            }
        }
    }
}
