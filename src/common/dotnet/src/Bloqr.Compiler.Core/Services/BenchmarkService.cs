namespace Bloqr.Compiler.Core.Services;

/// <inheritdoc cref="IBenchmarkService"/>
/// <remarks>
/// The unchunked and chunked paths currently shell out to two different underlying compilers
/// (see #424): <see cref="IBloqrCompilerService"/> uses Deno + the JSR
/// <c>@bloqr/compiler-core</c> package, <see cref="IChunkingService"/> uses
/// <c>hostlist-compiler</c>/<c>npx</c> directly. Part of any timing delta may reflect that
/// difference rather than chunking overhead alone, and each side needs its own tool installed
/// to succeed.
/// </remarks>
public sealed class BenchmarkService : IBenchmarkService
{
    private static readonly string[] Sizes = ["small", "medium", "large", "xlarge"];

    private static readonly string[] Transformations = ["Deduplicate", "RemoveEmptyLines", "TrimLines"];

    private readonly IBloqrCompilerService _compilerService;
    private readonly IChunkingService _chunkingService;
    private readonly IConfigurationReader _configurationReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="BenchmarkService"/> class.
    /// </summary>
    public BenchmarkService(
        IBloqrCompilerService compilerService,
        IChunkingService chunkingService,
        IConfigurationReader configurationReader)
    {
        _compilerService = compilerService ?? throw new ArgumentNullException(nameof(compilerService));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _configurationReader = configurationReader ?? throw new ArgumentNullException(nameof(configurationReader));
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> BenchmarkSizes => Sizes;

    /// <inheritdoc/>
    public string? FindBenchmarkDataDir()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "benchmarks", "data");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<List<BenchmarkRunResult>> RunBenchmarkAsync(
        string size,
        string? dataDir,
        int numSources,
        int maxParallel,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(size);

        var sizes = size.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? Sizes
            : [size];

        var invalidSize = sizes.FirstOrDefault(s => !Sizes.Contains(s, StringComparer.OrdinalIgnoreCase));
        if (invalidSize is not null)
        {
            throw new ArgumentException(
                $"Unknown benchmark size '{invalidSize}'. Expected one of: {string.Join(", ", Sizes)}, or 'all'.",
                nameof(size));
        }

        var resolvedDataDir = dataDir ?? FindBenchmarkDataDir();
        if (resolvedDataDir is null)
        {
            throw new DirectoryNotFoundException(
                "Could not find a benchmarks/data directory. Pass dataDir explicitly, or run "
                + "from within a clone of BloqrAI/bloqr-core.");
        }

        var results = new List<BenchmarkRunResult>();

        foreach (var s in sizes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dataPath = Path.Combine(resolvedDataDir, $"{s}.txt");
            if (!File.Exists(dataPath))
            {
                results.Add(new BenchmarkRunResult
                {
                    Size = s,
                    Sources = numSources,
                    MaxParallel = maxParallel,
                    Error = $"dataset file not found: {dataPath}"
                });
                continue;
            }

            var config = BuildBenchmarkConfiguration(s, dataPath, numSources);

            var unchunked = await RunUnchunkedAsync(config, cancellationToken).ConfigureAwait(false);
            var chunked = await RunChunkedAsync(config, maxParallel, cancellationToken).ConfigureAwait(false);

            double? speedup = unchunked.Success && chunked.Success && chunked.ElapsedMs > 0
                ? (double)unchunked.ElapsedMs / chunked.ElapsedMs
                : null;

            results.Add(new BenchmarkRunResult
            {
                Size = s,
                Sources = numSources,
                MaxParallel = maxParallel,
                UnchunkedSuccess = unchunked.Success,
                UnchunkedMs = unchunked.ElapsedMs,
                UnchunkedRuleCount = unchunked.RuleCount,
                ChunkedSuccess = chunked.Success,
                ChunkedMs = chunked.ElapsedMs,
                ChunkedRuleCount = chunked.RuleCount,
                Speedup = speedup,
                Error = unchunked.Error ?? chunked.Error
            });
        }

        return results;
    }

    /// <summary>
    /// Builds a <see cref="CompilerConfiguration"/> with <paramref name="numSources"/> identical
    /// sources, all pointing at <paramref name="dataPath"/>. Identical sources keep the
    /// unchunked and chunked runs directly comparable: same total workload, same total rule
    /// count after dedup, only the chunking strategy differs.
    /// </summary>
    private static CompilerConfiguration BuildBenchmarkConfiguration(string size, string dataPath, int numSources)
    {
        var config = new CompilerConfiguration
        {
            Name = $"Benchmark - {size}",
            Description = $"Real-pipeline benchmark of the '{size}' canned dataset",
            Version = "1.0.0",
            Transformations = [.. Transformations]
        };

        for (var i = 0; i < Math.Max(1, numSources); i++)
        {
            config.Sources.Add(new FilterSource
            {
                Name = $"source-{i + 1}",
                Source = dataPath,
                Type = "adblock"
            });
        }

        return config;
    }

    /// <summary>
    /// Runs the unchunked path: writes <paramref name="config"/> to a temp JSON file and
    /// compiles it through the real <see cref="IBloqrCompilerService"/> pipeline (a single
    /// compiler invocation covering all of <paramref name="config"/>'s sources).
    /// </summary>
    private async Task<(bool Success, long ElapsedMs, int RuleCount, string? Error)> RunUnchunkedAsync(
        CompilerConfiguration config,
        CancellationToken cancellationToken)
    {
        var tempConfigPath = Path.Combine(Path.GetTempPath(), $"benchmark-config-{Guid.NewGuid()}.json");
        var tempOutputPath = Path.Combine(Path.GetTempPath(), $"benchmark-output-{Guid.NewGuid()}.txt");

        try
        {
            var json = _configurationReader.ToJson(config);
            await File.WriteAllTextAsync(tempConfigPath, json, cancellationToken).ConfigureAwait(false);

            var options = new CompilerOptions
            {
                ConfigPath = tempConfigPath,
                OutputPath = tempOutputPath,
                ValidateConfig = false
            };

            var result = await _compilerService.RunAsync(options, cancellationToken).ConfigureAwait(false);
            return (result.Success, result.ElapsedMs, result.RuleCount, result.Success ? null : result.ErrorMessage);
        }
        catch (Exception ex)
        {
            return (false, 0, 0, ex.Message);
        }
        finally
        {
            if (File.Exists(tempConfigPath)) { try { File.Delete(tempConfigPath); } catch { /* ignore */ } }
            if (File.Exists(tempOutputPath)) { try { File.Delete(tempOutputPath); } catch { /* ignore */ } }
        }
    }

    /// <summary>
    /// Runs the chunked path: splits <paramref name="config"/> into one chunk per source (the
    /// only implemented chunking strategy) and compiles the chunks in parallel through the real
    /// <see cref="IChunkingService"/> pipeline, up to <paramref name="maxParallel"/> at a time.
    /// </summary>
    private async Task<(bool Success, long ElapsedMs, int RuleCount, string? Error)> RunChunkedAsync(
        CompilerConfiguration config,
        int maxParallel,
        CancellationToken cancellationToken)
    {
        var chunkingOptions = new ChunkingOptions
        {
            Enabled = true,
            MaxParallel = Math.Max(1, maxParallel),
            Strategy = ChunkingStrategy.Source
        };

        try
        {
            var chunks = _chunkingService.SplitIntoChunks(config, chunkingOptions);
            var options = new CompilerOptions { ValidateConfig = false };
            var result = await _chunkingService.CompileChunksAsync(chunks, options, chunkingOptions, cancellationToken)
                .ConfigureAwait(false);

            var error = result.Success ? null : string.Join("; ", result.Errors);
            return (result.Success, result.TotalElapsedMs, result.FinalRuleCount, error);
        }
        catch (Exception ex)
        {
            return (false, 0, 0, ex.Message);
        }
    }
}
