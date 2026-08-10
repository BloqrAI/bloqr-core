namespace Bloqr.Dashboard.Console.Services;

/// <summary>
/// Writes compilation pipeline events to the Dashboard's structured JSON log, giving every
/// compile run an audit trail even without a live progress UI. This is the seam issue #270's
/// rich, live progress renderer replaces (or augments) — it does not implement that UI itself.
/// </summary>
public sealed class CompilationLoggingEventHandler : CompilationEventHandlerBase
{
    private readonly ILogger<CompilationLoggingEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationLoggingEventHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger to write compilation events to.</param>
    public CompilationLoggingEventHandler(ILogger<CompilationLoggingEventHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public override Task OnCompilationStartingAsync(
        CompilationStartedEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Compilation starting: {ConfigPath}", args.Options.ConfigPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnValidationAsync(ValidationEventArgs args, CancellationToken cancellationToken = default)
    {
        foreach (var finding in args.Findings)
        {
            var level = finding.Severity switch
            {
                ValidationSeverity.Critical or ValidationSeverity.Error => LogLevel.Error,
                ValidationSeverity.Warning => LogLevel.Warning,
                _ => LogLevel.Information,
            };

            _logger.Log(level, "[{Stage}/{Code}] {Message}", args.StageName, finding.Code, finding.Message);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnSourceLoadedAsync(SourceLoadedEventArgs args, CancellationToken cancellationToken = default)
    {
        if (args.Success)
        {
            _logger.LogInformation(
                "Source {SourceIndex}/{TotalSources} loaded: {SourceName} (~{EstimatedRuleCount} rules)",
                args.SourceIndex + 1,
                args.TotalSources,
                args.Source.Name ?? args.Source.Source,
                args.EstimatedRuleCount);
        }
        else
        {
            _logger.LogWarning(
                "Source {SourceIndex}/{TotalSources} failed to load: {SourceName} ({ErrorMessage})",
                args.SourceIndex + 1,
                args.TotalSources,
                args.Source.Name ?? args.Source.Source,
                args.ErrorMessage);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnChunkCompletedAsync(ChunkCompletedEventArgs args, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Chunk {ChunkIndex}/{ChunkTotal} completed: success={Success}, {RuleCount} rules in {DurationMs}ms",
            args.Chunk.Index,
            args.Chunk.Total,
            args.Success,
            args.RuleCount,
            args.Duration.TotalMilliseconds);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnChunksMergedAsync(ChunksMergedEventArgs args, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Merged {ChunkCount} chunks: {FinalRuleCount} rules ({DuplicatesRemoved} duplicates removed)",
            args.ChunkCount,
            args.FinalRuleCount,
            args.DuplicatesRemoved);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnCompilationCompletedAsync(
        CompilationCompletedEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Compilation completed: {RuleCount} rules -> {OutputPath} in {ElapsedMs}ms",
            args.Result.RuleCount,
            args.Result.OutputPath,
            args.Duration.TotalMilliseconds);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnCompilationErrorAsync(
        CompilationErrorEventArgs args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogError(args.Exception, "Compilation error");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnHashMismatchAsync(HashMismatchEventArgs args, CancellationToken cancellationToken = default)
    {
        _logger.LogError(
            "Hash mismatch for {ItemIdentifier} ({ItemType}): expected {ExpectedHash}, got {ActualHash}",
            args.ItemIdentifier,
            args.ItemType,
            args.ExpectedHash,
            args.ActualHash);
        return Task.CompletedTask;
    }
}
