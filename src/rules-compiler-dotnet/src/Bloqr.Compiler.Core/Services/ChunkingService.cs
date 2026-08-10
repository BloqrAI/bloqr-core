namespace Bloqr.Compiler.Core.Services;

/// <summary>
/// Service for chunking and parallel compilation of filter rules.
/// </summary>
public class ChunkingService : IChunkingService
{
    private readonly ILogger<ChunkingService> _logger;
    private readonly IConfigurationReader _configurationReader;
    private readonly CommandHelper _commandHelper;
    private readonly ICompilationEventDispatcher _eventDispatcher;

    private const string CompilerCommand = "hostlist-compiler";
    private const string NpxCommand = "npx";

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkingService"/> class.
    /// </summary>
    public ChunkingService(
        ILogger<ChunkingService> logger,
        IConfigurationReader configurationReader,
        CommandHelper commandHelper,
        ICompilationEventDispatcher eventDispatcher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configurationReader = configurationReader ?? throw new ArgumentNullException(nameof(configurationReader));
        _commandHelper = commandHelper ?? throw new ArgumentNullException(nameof(commandHelper));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    /// <inheritdoc/>
    public bool ShouldEnableChunking(CompilerConfiguration configuration, ChunkingOptions? options)
    {
        // If no sources, don't chunk
        if (configuration.Sources == null || configuration.Sources.Count == 0)
        {
            return false;
        }

        // If explicitly disabled, don't chunk
        if (options?.Enabled == false)
        {
            return false;
        }

        // If explicitly enabled, chunk
        if (options?.Enabled == true)
        {
            _logger.LogDebug("Chunking explicitly enabled in options");
            return true;
        }

        // For source strategy (default), chunk if we have multiple sources
        var strategy = options?.Strategy ?? ChunkingStrategy.Source;
        if (strategy == ChunkingStrategy.Source && configuration.Sources.Count > 1)
        {
            _logger.LogDebug("Chunking enabled: {Count} sources detected", configuration.Sources.Count);
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public List<(CompilerConfiguration Config, ChunkMetadata Metadata)> SplitIntoChunks(
        CompilerConfiguration configuration,
        ChunkingOptions options)
    {
        var sources = configuration.Sources ?? [];
        var chunks = new List<(CompilerConfiguration, ChunkMetadata)>();

        if (sources.Count == 0)
        {
            _logger.LogWarning("No sources to chunk");
            return chunks;
        }

        _logger.LogInformation("Splitting configuration into chunks (strategy: {Strategy})", options.Strategy);

        if (options.Strategy == ChunkingStrategy.Source)
        {
            return SplitBySource(configuration, options);
        }
        else
        {
            _logger.LogWarning("LineCount strategy not yet implemented, falling back to Source strategy");
            return SplitBySource(configuration, options);
        }
    }

    private List<(CompilerConfiguration Config, ChunkMetadata Metadata)> SplitBySource(
        CompilerConfiguration configuration,
        ChunkingOptions options)
    {
        var sources = configuration.Sources ?? [];
        var chunks = new List<(CompilerConfiguration, ChunkMetadata)>();

        // Calculate sources per chunk to keep chunks balanced
        var sourcesPerChunk = Math.Max(1, (int)Math.Ceiling((double)sources.Count / options.MaxParallel));
        var totalChunks = (int)Math.Ceiling((double)sources.Count / sourcesPerChunk);

        _logger.LogInformation(
            "Creating {TotalChunks} chunks with ~{SourcesPerChunk} sources each",
            totalChunks,
            sourcesPerChunk);

        for (var i = 0; i < totalChunks; i++)
        {
            var startIdx = i * sourcesPerChunk;
            var endIdx = Math.Min(startIdx + sourcesPerChunk, sources.Count);
            var chunkSources = sources.Skip(startIdx).Take(endIdx - startIdx).ToList();

            var chunkConfig = new CompilerConfiguration
            {
                Name = $"{configuration.Name} (chunk {i + 1}/{totalChunks})",
                Description = configuration.Description,
                Homepage = configuration.Homepage,
                License = configuration.License,
                Version = configuration.Version,
                Sources = chunkSources,
                Transformations = configuration.Transformations,
                Inclusions = configuration.Inclusions,
                InclusionsSources = configuration.InclusionsSources,
                Exclusions = configuration.Exclusions,
                ExclusionsSources = configuration.ExclusionsSources
            };

            var metadata = new ChunkMetadata
            {
                Index = i,
                Total = totalChunks,
                EstimatedRules = 0, // Will be populated after compilation
                Sources = chunkSources
            };

            chunks.Add((chunkConfig, metadata));
        }

        _logger.LogDebug("Created {Count} chunks", chunks.Count);
        return chunks;
    }

    /// <inheritdoc/>
    public async Task<ChunkedCompilationResult> CompileChunksAsync(
        List<(CompilerConfiguration Config, ChunkMetadata Metadata)> chunks,
        CompilerOptions options,
        ChunkingOptions chunkingOptions,
        CancellationToken cancellationToken = default)
    {
        var result = new ChunkedCompilationResult();
        var stopwatch = Stopwatch.StartNew();
        var chunkResults = new List<string[]>();

        _logger.LogInformation(
            "Compiling {Count} chunks with max {MaxParallel} parallel workers",
            chunks.Count,
            chunkingOptions.MaxParallel);

        // Process chunks in batches to limit parallelism
        for (var batchStart = 0; batchStart < chunks.Count; batchStart += chunkingOptions.MaxParallel)
        {
            var batchEnd = Math.Min(batchStart + chunkingOptions.MaxParallel, chunks.Count);
            var batch = chunks.Skip(batchStart).Take(batchEnd - batchStart).ToList();

            var batchNumber = batchStart / chunkingOptions.MaxParallel + 1;
            var totalBatches = (int)Math.Ceiling((double)chunks.Count / chunkingOptions.MaxParallel);

            _logger.LogInformation(
                "Processing batch {BatchNumber}/{TotalBatches} (chunks {Start}-{End})",
                batchNumber,
                totalBatches,
                batchStart + 1,
                batchEnd);

            // Compile all chunks in this batch in parallel
            var batchTasks = batch.Select(async chunk =>
            {
                var chunkStopwatch = Stopwatch.StartNew();
                var (config, metadata) = chunk;

                await _eventDispatcher.RaiseChunkStartedAsync(
                    new ChunkStartedEventArgs(options, metadata, metadata.Sources), cancellationToken);

                try
                {
                    _logger.LogDebug(
                        "Starting chunk {Index}/{Total}: {Name}",
                        metadata.Index + 1,
                        metadata.Total,
                        config.Name);

                    var rules = await CompileSingleChunkAsync(config, options, cancellationToken);
                    chunkStopwatch.Stop();

                    metadata.Success = true;
                    metadata.ElapsedMs = chunkStopwatch.ElapsedMilliseconds;
                    metadata.ActualRules = rules.Length;

                    _logger.LogInformation(
                        "Chunk {Index}/{Total} complete: {RuleCount} rules in {ElapsedMs}ms",
                        metadata.Index + 1,
                        metadata.Total,
                        rules.Length,
                        metadata.ElapsedMs);

                    await _eventDispatcher.RaiseChunkCompletedAsync(
                        new ChunkCompletedEventArgs(
                            options, metadata, success: true, ruleCount: rules.Length,
                            duration: chunkStopwatch.Elapsed),
                        cancellationToken);

                    return (metadata, rules);
                }
                catch (Exception ex)
                {
                    chunkStopwatch.Stop();

                    metadata.Success = false;
                    metadata.ElapsedMs = chunkStopwatch.ElapsedMilliseconds;
                    metadata.ErrorMessage = ex.Message;

                    _logger.LogError(
                        ex,
                        "Chunk {Index}/{Total} failed: {Message}",
                        metadata.Index + 1,
                        metadata.Total,
                        ex.Message);

                    await _eventDispatcher.RaiseChunkCompletedAsync(
                        new ChunkCompletedEventArgs(
                            options, metadata, success: false, duration: chunkStopwatch.Elapsed, errorMessage: ex.Message),
                        cancellationToken);

                    return (metadata, Array.Empty<string>());
                }
            });

            var batchResults = await Task.WhenAll(batchTasks);

            foreach (var (metadata, rules) in batchResults)
            {
                result.Chunks.Add(metadata);
                if (metadata.Success && rules.Length > 0)
                {
                    chunkResults.Add(rules);
                }
                if (!metadata.Success && metadata.ErrorMessage != null)
                {
                    result.Errors.Add($"Chunk {metadata.Index + 1}: {metadata.ErrorMessage}");
                }
            }
        }

        stopwatch.Stop();

        // Merge results
        if (chunkResults.Count > 0)
        {
            var mergeStopwatch = Stopwatch.StartNew();
            var totalRulesBeforeMerge = chunkResults.Sum(r => r.Length);
            await _eventDispatcher.RaiseChunksMergingAsync(
                new ChunksMergingEventArgs(options, chunkResults.Count, totalRulesBeforeMerge), cancellationToken);

            var (mergedRules, duplicatesRemoved) = MergeChunks(chunkResults);
            result.MergedRules = mergedRules;
            result.DuplicatesRemoved = duplicatesRemoved;
            result.FinalRuleCount = mergedRules.Length;
            mergeStopwatch.Stop();

            await _eventDispatcher.RaiseChunksMergedAsync(
                new ChunksMergedEventArgs(
                    options, chunkResults.Count, totalRulesBeforeMerge, mergedRules.Length, duplicatesRemoved,
                    mergeStopwatch.Elapsed),
                cancellationToken);
        }

        result.TotalRules = result.Chunks.Sum(c => c.ActualRules ?? 0);
        result.TotalElapsedMs = stopwatch.ElapsedMilliseconds;
        result.Success = result.Errors.Count == 0;

        _logger.LogInformation(
            "Chunked compilation complete: {FinalCount} rules (removed {Duplicates} duplicates) in {ElapsedMs}ms",
            result.FinalRuleCount,
            result.DuplicatesRemoved,
            result.TotalElapsedMs);

        if (result.EstimatedSpeedup > 1.0)
        {
            _logger.LogInformation("Estimated speedup: {Speedup:F2}x", result.EstimatedSpeedup);
        }

        return result;
    }

    private async Task<string[]> CompileSingleChunkAsync(
        CompilerConfiguration config,
        CompilerOptions options,
        CancellationToken cancellationToken)
    {
        // Create temporary config file
        var tempConfigPath = Path.Combine(Path.GetTempPath(), $"chunk-config-{Guid.NewGuid()}.json");
        var tempOutputPath = Path.Combine(Path.GetTempPath(), $"chunk-output-{Guid.NewGuid()}.txt");

        try
        {
            // Write config to temp file
            var jsonContent = _configurationReader.ToJson(config);
            await File.WriteAllTextAsync(tempConfigPath, jsonContent, cancellationToken);

            // Get compiler command
            var (command, args) = GetCompilerCommand(tempConfigPath, tempOutputPath, options.Verbose);

            if (string.IsNullOrEmpty(command))
            {
                throw new InvalidOperationException("hostlist-compiler not found");
            }

            // Execute compiler
            var (exitCode, stdOut, stdErr) = await _commandHelper.ExecuteAsync(
                command,
                args,
                Path.GetTempPath(),
                cancellationToken);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"Compilation failed with exit code {exitCode}: {stdErr}");
            }

            // Read output
            if (!File.Exists(tempOutputPath))
            {
                throw new InvalidOperationException("Output file was not created");
            }

            var rules = await File.ReadAllLinesAsync(tempOutputPath, cancellationToken);
            return rules;
        }
        finally
        {
            // Clean up temp files
            if (File.Exists(tempConfigPath))
            {
                try { File.Delete(tempConfigPath); } catch { /* ignore */ }
            }
            if (File.Exists(tempOutputPath))
            {
                try { File.Delete(tempOutputPath); } catch { /* ignore */ }
            }
        }
    }

    private (string Command, string Args) GetCompilerCommand(
        string configPath,
        string outputPath,
        bool verbose)
    {
        var verboseFlag = verbose ? " --verbose" : "";

        // Try global hostlist-compiler first
        var compilerPath = _commandHelper.FindCommand(CompilerCommand);
        if (compilerPath != null)
        {
            return (compilerPath, $"--config \"{configPath}\" --output \"{outputPath}\"{verboseFlag}");
        }

        // Fall back to npx
        var npxPath = _commandHelper.FindCommand(NpxCommand);
        if (npxPath != null)
        {
            return (npxPath, $"@adguard/hostlist-compiler --config \"{configPath}\" --output \"{outputPath}\"{verboseFlag}");
        }

        return (string.Empty, string.Empty);
    }

    /// <inheritdoc/>
    public (string[] Rules, int DuplicatesRemoved) MergeChunks(List<string[]> chunkResults)
    {
        _logger.LogInformation("Merging {Count} chunks...", chunkResults.Count);

        // Flatten all chunks
        var allRules = chunkResults.SelectMany(r => r).ToList();
        _logger.LogDebug("Total rules before deduplication: {Count}", allRules.Count);

        // Deduplicate while preserving order
        var seen = new HashSet<string>();
        var deduplicated = new List<string>();

        foreach (var rule in allRules)
        {
            var trimmed = rule.Trim();

            // Keep comments and empty lines without deduplication
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('!') || trimmed.StartsWith('#'))
            {
                deduplicated.Add(rule);
                continue;
            }

            // Deduplicate actual rules
            if (seen.Add(rule))
            {
                deduplicated.Add(rule);
            }
        }

        var duplicatesRemoved = allRules.Count - deduplicated.Count;

        _logger.LogInformation(
            "Merged to {Count} rules (removed {Duplicates} duplicates)",
            deduplicated.Count,
            duplicatesRemoved);

        return (deduplicated.ToArray(), duplicatesRemoved);
    }

    /// <inheritdoc/>
    public double EstimateSpeedup(int totalRules, ChunkingOptions options)
    {
        if (!options.Enabled || totalRules == 0)
        {
            return 1.0;
        }

        // Simple linear model
        var numChunks = Math.Ceiling((double)totalRules / options.ChunkSize);
        var batches = Math.Ceiling(numChunks / options.MaxParallel);

        // Theoretical speedup = total time / parallel time
        // Parallel time = batches * (chunkSize / totalRules * time)
        // This simplifies to: numChunks / batches = min(numChunks, maxParallel)
        return Math.Min(numChunks, options.MaxParallel);
    }
}
