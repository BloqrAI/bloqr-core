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
/// <remarks>
/// <b>Two-engine progress (#441)</b>: a mixed-engine config produces two artifacts (DNS and
/// browser), but they are compiled by one sequential call chain, not two concurrent ones -
/// confirmed in <c>BloqrCompilerService.RunAsyncCore</c>/<c>PublishAndVerifyBrowserArtifactAsync</c>,
/// which run the DNS artifact's full pipeline (chunk, merge, publish, hash, validate) to
/// completion before starting the browser artifact's. There is therefore only ever one active
/// "progress root" per compile, which is also all <see cref="LiveProgressSession"/> supports by
/// design (a single shared field behind a lock, deliberately not <c>AsyncLocal&lt;T&gt;</c> - see
/// its own remarks - because this Dashboard only ever runs one compile, and hence one live
/// display, at a time). So rather than two concurrent roots, this handler adds two named
/// <i>child</i> tasks - "DNS artifact" and "Browser artifact" - under the existing "Compiling"
/// root when the loaded config is mixed-engine, sequenced the same way the underlying compile is:
/// the DNS child completes at <see cref="OnChunksMergedAsync"/> (the DNS artifact is fully
/// written), and the browser child completes at <see cref="OnCompilationCompletedAsync"/> (the
/// point by which <c>PublishAndVerifyBrowserArtifactAsync</c> has finished). Both updates happen
/// under the same <see cref="_lock"/> as every other task mutation here, so there is no
/// interleaving-corruption risk even though the two child tasks are visible on screen at once.
/// </remarks>
public sealed class CompilationProgressEventHandler : CompilationEventHandlerBase
{
    private readonly LiveProgressSession _session;
    private readonly IConsoleRenderer _renderer;
    private readonly Lock _lock = new();

    private ILiveProgressTask? _overallTask;
    private ILiveProgressTask? _chunksTask;
    private ILiveProgressTask? _dnsTask;
    private ILiveProgressTask? _browserTask;

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
            _dnsTask = null;
            _browserTask = null;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnConfigurationLoadedAsync(
        ConfigurationLoadedEventArgs args, CancellationToken cancellationToken = default)
    {
        var context = _session.Current;

        lock (_lock)
        {
            if (_overallTask is not null)
            {
                _overallTask.Description = $"Compiling {args.Configuration.Name}";
                _overallTask.Increment(10);
            }

            // See the "Two-engine progress" class remarks: named child tasks, not concurrent
            // roots, sequenced to match the underlying compile.
            if (context is not null && IsMixedEngineConfiguration(args.Configuration))
            {
                _dnsTask = context.AddTask("  DNS artifact", maxValue: 100);
                _browserTask = context.AddTask("  Browser artifact", maxValue: 100);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Determines whether <paramref name="configuration"/> resolves to both engines across its
    /// sources - i.e. compiling it will produce both a DNS and a browser-syntax artifact - using
    /// the same per-source resolution order <see cref="FilterSource.Engine"/> and
    /// <see cref="CompilerConfiguration.DefaultEngine"/> document: a source's own
    /// <c>Engine</c> if set, else the config's <c>DefaultEngine</c>, else <c>"dns"</c>.
    /// </summary>
    private static bool IsMixedEngineConfiguration(CompilerConfiguration configuration)
    {
        var sawDns = false;
        var sawBrowser = false;

        foreach (var source in configuration.Sources)
        {
            var resolved = source.Engine ?? configuration.DefaultEngine ?? "dns";
            if (string.Equals(resolved, "browser", StringComparison.OrdinalIgnoreCase))
            {
                sawBrowser = true;
            }
            else
            {
                sawDns = true;
            }

            if (sawDns && sawBrowser)
            {
                return true;
            }
        }

        return false;
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

            // The DNS artifact is fully written by the time chunks are merged - see the
            // "Two-engine progress" class remarks.
            _dnsTask?.Complete();
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
            _dnsTask?.Complete();
            // The browser artifact (if any) is fully published/hashed/validated by the time
            // CompilationCompleted is raised - see the "Two-engine progress" class remarks.
            _browserTask?.Complete();
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
            _dnsTask?.Complete();
            _browserTask?.Complete();
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
