namespace Bloqr.Dashboard.Console.Progress;

/// <summary>
/// Drives a live, multi-task Spectre.Console progress display from the compilation event
/// pipeline: an overall progress bar, a per-chunk progress bar when chunking is in use, and
/// color-coded validation/error output as findings stream in - per the epic's "lots of visual
/// feedback... which stage is happening... progress for that task, the overall progress...
/// results from transformations... merge process with chunking... any rule errors, file errors,
/// color coded and beautiful" (#270).
/// </summary>
/// <remarks>
/// A no-op when <see cref="LiveProgressSession.Current"/> is unset - e.g. a non-interactive CLI
/// run with no live display attached - so this handler can stay registered unconditionally.
/// <see cref="CompilationLoggingEventHandler"/> already writes every event to the structured
/// JSON log regardless; this handler only adds the live terminal presentation on top.
/// </remarks>
public sealed class CompilationProgressEventHandler : CompilationEventHandlerBase
{
    private readonly LiveProgressSession _session;
    private readonly IConsoleRenderer _renderer;
    private readonly Lock _lock = new();

    private ILiveProgressTask? _overallTask;
    private ILiveProgressTask? _chunksTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilationProgressEventHandler"/> class.
    /// </summary>
    public CompilationProgressEventHandler(LiveProgressSession session, IConsoleRenderer renderer)
    {
        _session = session;
        _renderer = renderer;
    }

    /// <inheritdoc />
    public override Task OnCompilationStartingAsync(
        CompilationStartedEventArgs args, CancellationToken cancellationToken = default)
    {
        var context = _session.Current;
        if (context is null)
        {
            return Task.CompletedTask;
        }

        lock (_lock)
        {
            _overallTask = context.AddTask("Compiling", maxValue: 100);
            _chunksTask = null;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnConfigurationLoadedAsync(
        ConfigurationLoadedEventArgs args, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_overallTask is not null)
            {
                _overallTask.Description = $"Compiling {args.Configuration.Name}";
                _overallTask.Increment(10);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnValidationAsync(ValidationEventArgs args, CancellationToken cancellationToken = default)
    {
        foreach (var finding in args.Findings)
        {
            _renderer.WriteStyled($"[{args.StageName}/{finding.Code}] {finding.Message}", SeverityToStyle(finding.Severity));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnChunkStartedAsync(ChunkStartedEventArgs args, CancellationToken cancellationToken = default)
    {
        var context = _session.Current;
        if (context is null)
        {
            return Task.CompletedTask;
        }

        lock (_lock)
        {
            _chunksTask ??= context.AddTask("Chunks", maxValue: args.Chunk.Total);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnChunkCompletedAsync(ChunkCompletedEventArgs args, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _chunksTask?.Increment(1);
        }

        if (!args.Success)
        {
            _renderer.WriteStyled(
                $"Chunk {args.Chunk.Index + 1}/{args.Chunk.Total} failed: {args.ErrorMessage}", TextStyle.Error);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnChunksMergedAsync(ChunksMergedEventArgs args, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _overallTask?.Increment(10);
        }

        _renderer.WriteStyled(
            $"Merged {args.ChunkCount} chunks: {args.FinalRuleCount} rules ({args.DuplicatesRemoved} duplicates removed)",
            TextStyle.Muted);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnCompilationCompletedAsync(
        CompilationCompletedEventArgs args, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _overallTask?.Complete();
            _chunksTask?.Complete();
        }

        _renderer.WriteStyled(
            $"Compiled {args.Result.RuleCount} rules -> {args.Result.OutputPath} ({args.Duration.TotalMilliseconds:F0}ms)",
            TextStyle.Success);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnCompilationErrorAsync(
        CompilationErrorEventArgs args, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _overallTask?.Complete();
            _chunksTask?.Complete();
        }

        _renderer.WriteStyled($"Compilation failed: {args.Exception.Message}", TextStyle.Error);

        return Task.CompletedTask;
    }

    private static TextStyle SeverityToStyle(ValidationSeverity severity) => severity switch
    {
        ValidationSeverity.Info => TextStyle.Info,
        ValidationSeverity.Warning => TextStyle.Warning,
        ValidationSeverity.Error or ValidationSeverity.Critical => TextStyle.Error,
        _ => TextStyle.Muted,
    };
}
