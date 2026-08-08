namespace RulesCompiler.Services;

/// <summary>
/// Compiles filter rules using the adblock-compiler-core CLI
/// (published as @bloqr/compiler-core on JSR), run via Deno.
/// </summary>
public class FilterCompiler : IFilterCompiler
{
    private readonly ILogger<FilterCompiler> _logger;
    private readonly IConfigurationReader _configurationReader;
    private readonly CommandHelper _commandHelper;
    private readonly IChunkingService? _chunkingService;

    private const string DenoCommand = "deno";
    private const string JsrPackageSpecifier = "jsr:@bloqr/compiler-core/cli";

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterCompiler"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="configurationReader">The configuration reader.</param>
    /// <param name="commandHelper">The command helper.</param>
    /// <param name="chunkingService">Optional chunking service for parallel compilation.</param>
    public FilterCompiler(
        ILogger<FilterCompiler> logger,
        IConfigurationReader configurationReader,
        CommandHelper commandHelper,
        IChunkingService? chunkingService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configurationReader = configurationReader ?? throw new ArgumentNullException(nameof(configurationReader));
        _commandHelper = commandHelper ?? throw new ArgumentNullException(nameof(commandHelper));
        _chunkingService = chunkingService;
    }

    /// <inheritdoc/>
    public Task<CompilerResult> CompileAsync(
        string configPath,
        string? outputPath = null,
        ConfigurationFormat? format = null,
        bool verbose = false,
        CancellationToken cancellationToken = default)
    {
        var options = new CompilerOptions
        {
            ConfigPath = configPath,
            OutputPath = outputPath,
            Format = format,
            Verbose = verbose
        };
        return CompileAsync(options, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CompilerResult> CompileAsync(
        CompilerOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ConfigPath))
            throw new ArgumentException("ConfigPath is required", nameof(options));

        var result = new CompilerResult
        {
            StartTime = DateTime.UtcNow
        };

        var stopwatch = Stopwatch.StartNew();
        string? tempConfigPath = null;

        try
        {
            // Read configuration to get metadata
            var config = await _configurationReader.ReadConfigurationAsync(
                options.ConfigPath, options.Format, cancellationToken);
            result.ConfigName = config.Name;
            result.ConfigVersion = config.Version;

            // Check if chunking should be used
            if (_chunkingService != null &&
                options.Chunking != null &&
                _chunkingService.ShouldEnableChunking(config, options.Chunking))
            {
                return await CompileWithChunkingAsync(config, options, cancellationToken);
            }

            // Determine config path to use (convert to JSON if needed)
            var actualFormat = options.Format ?? _configurationReader.DetectFormat(options.ConfigPath);
            var configToUse = options.ConfigPath;

            if (actualFormat != ConfigurationFormat.Json)
            {
                // adblock-compiler-core only supports JSON, create temp file
                tempConfigPath = Path.Combine(Path.GetTempPath(), $"compiler-config-{Guid.NewGuid()}.json");
                var jsonContent = _configurationReader.ToJson(config);
                await File.WriteAllTextAsync(tempConfigPath, jsonContent, cancellationToken);
                configToUse = tempConfigPath;
                _logger.LogDebug("Created temporary JSON config at {Path}", tempConfigPath);
            }

            // Determine output path
            var actualOutputPath = options.OutputPath ?? Path.Combine(
                Path.GetDirectoryName(options.ConfigPath) ?? ".",
                "output",
                $"compiled-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt");

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(actualOutputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            result.OutputPath = actualOutputPath;

            // Find compiler command
            var (command, args) = await GetCompilerCommandAsync(
                configToUse, actualOutputPath, options.Verbose, cancellationToken);

            if (string.IsNullOrEmpty(command))
            {
                result.Success = false;
                result.ErrorMessage = "deno not found. Install from: https://deno.com/";
                return result;
            }

            _logger.LogInformation("Compiling filter rules using {Command}", command);
            if (options.Verbose)
            {
                _logger.LogInformation("Verbose mode enabled");
            }

            // Execute compiler
            var (exitCode, stdOut, stdErr) = await _commandHelper.ExecuteAsync(
                command,
                args,
                Path.GetDirectoryName(options.ConfigPath),
                cancellationToken);

            result.StandardOutput = stdOut;
            result.StandardError = stdErr;

            if (exitCode != 0)
            {
                result.Success = false;
                result.ErrorMessage = $"Compiler exited with code {exitCode}: {stdErr}";
                _logger.LogError("Compilation failed with exit code {ExitCode}", exitCode);
                return result;
            }

            // Verify output was created
            if (!File.Exists(actualOutputPath))
            {
                result.Success = false;
                result.ErrorMessage = "Compilation completed but output file was not created";
                return result;
            }

            result.Success = true;
            _logger.LogInformation("Compilation completed successfully: {OutputPath}", actualOutputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compilation failed");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            // Clean up temp file
            if (tempConfigPath != null && File.Exists(tempConfigPath))
            {
                try
                {
                    File.Delete(tempConfigPath);
                    _logger.LogDebug("Cleaned up temporary config file");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temporary config file");
                }
            }

            stopwatch.Stop();
            result.ElapsedMs = stopwatch.ElapsedMilliseconds;
            result.EndTime = DateTime.UtcNow;
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<VersionInfo> GetVersionInfoAsync(CancellationToken cancellationToken = default)
    {
        var info = new VersionInfo
        {
            ModuleVersion = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0",
            DotNetVersion = Environment.Version.ToString(),
            Platform = PlatformHelper.GetPlatformInfo()
        };

        // Get Deno runtime version (reported via the NodeVersion field for
        // backward compatibility with existing consumers of VersionInfo)
        var denoPath = _commandHelper.FindCommand(DenoCommand);
        if (denoPath != null)
        {
            info.NodeVersion = await _commandHelper.GetVersionAsync(denoPath, "--version", cancellationToken);
            info.HostlistCompilerPath = $"{denoPath} run {JsrPackageSpecifier}";
            info.HostlistCompilerVersion = await _commandHelper.GetVersionAsync(
                denoPath,
                $"run --allow-read --allow-write --allow-env --allow-net --allow-run {JsrPackageSpecifier} --version",
                cancellationToken);
        }

        return info;
    }

    /// <inheritdoc/>
    public async Task<bool> IsCompilerAvailableAsync()
    {
        var denoPath = _commandHelper.FindCommand(DenoCommand);
        return denoPath != null;
    }

    private async Task<(string Command, string Args)> GetCompilerCommandAsync(
        string configPath,
        string outputPath,
        bool verbose,
        CancellationToken cancellationToken)
    {
        var verboseFlag = verbose ? " --verbose" : "";

        var denoPath = _commandHelper.FindCommand(DenoCommand);
        if (denoPath != null)
        {
            return (denoPath,
                $"run --allow-read --allow-write --allow-env --allow-net --allow-run {JsrPackageSpecifier} " +
                $"--config \"{configPath}\" --output \"{outputPath}\"{verboseFlag}");
        }

        return (string.Empty, string.Empty);
    }

    /// <summary>
    /// Compiles filter rules using chunked parallel compilation.
    /// </summary>
    private async Task<CompilerResult> CompileWithChunkingAsync(
        CompilerConfiguration config,
        CompilerOptions options,
        CancellationToken cancellationToken)
    {
        var result = new CompilerResult
        {
            StartTime = DateTime.UtcNow,
            ConfigName = config.Name,
            ConfigVersion = config.Version
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting chunked parallel compilation for {Name}", config.Name);

            // Split into chunks
            var chunks = _chunkingService!.SplitIntoChunks(config, options.Chunking!);

            if (chunks.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "No chunks were created";
                return result;
            }

            _logger.LogInformation("Created {Count} chunks for parallel compilation", chunks.Count);

            // Compile chunks in parallel
            var chunkResult = await _chunkingService.CompileChunksAsync(
                chunks,
                options,
                options.Chunking!,
                cancellationToken);

            // Determine output path
            var actualOutputPath = options.OutputPath ?? Path.Combine(
                Path.GetDirectoryName(options.ConfigPath!) ?? ".",
                "output",
                $"compiled-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt");

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(actualOutputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            result.OutputPath = actualOutputPath;

            if (!chunkResult.Success)
            {
                result.Success = false;
                result.ErrorMessage = string.Join("; ", chunkResult.Errors);
                _logger.LogError("Chunked compilation failed: {Errors}", result.ErrorMessage);
                return result;
            }

            // Write merged output
            if (chunkResult.MergedRules != null)
            {
                await File.WriteAllLinesAsync(actualOutputPath, chunkResult.MergedRules, cancellationToken);
                _logger.LogInformation(
                    "Wrote {Count} rules to {Path} (removed {Duplicates} duplicates)",
                    chunkResult.FinalRuleCount,
                    actualOutputPath,
                    chunkResult.DuplicatesRemoved);
            }

            result.Success = true;
            result.StandardOutput = $"Chunked compilation complete: {chunkResult.FinalRuleCount} rules from {chunks.Count} chunks (speedup: {chunkResult.EstimatedSpeedup:F2}x)";

            _logger.LogInformation(
                "Chunked compilation completed successfully in {ElapsedMs}ms (estimated speedup: {Speedup:F2}x)",
                stopwatch.ElapsedMilliseconds,
                chunkResult.EstimatedSpeedup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chunked compilation failed");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.ElapsedMs = stopwatch.ElapsedMilliseconds;
            result.EndTime = DateTime.UtcNow;
        }

        return result;
    }
}
