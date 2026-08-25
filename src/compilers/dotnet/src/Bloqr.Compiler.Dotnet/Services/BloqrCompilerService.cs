namespace Bloqr.Compiler.Dotnet.Services;

/// <summary>
/// Main orchestration service for the rules compiler pipeline.
/// </summary>
public class BloqrCompilerService : IBloqrCompilerService
{
    private readonly ILogger<BloqrCompilerService> _logger;
    private readonly IConfigurationReader _configurationReader;
    private readonly IFilterCompiler _filterCompiler;
    private readonly IOutputWriter _outputWriter;
    private readonly IOutputPublisher _outputPublisher;
    private readonly IHashDatabaseService _hashDatabaseService;
    private readonly IBloqrValidatorService _rulesValidatorService;
    private readonly ICompilationEventDispatcher _eventDispatcher;

    private const string DefaultConfigFileName = "compiler-config.json";
    private const string DefaultRulesFileName = "adguard_user_filter.txt";

    /// <summary>
    /// Initializes a new instance of the <see cref="BloqrCompilerService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="configurationReader">The configuration reader.</param>
    /// <param name="filterCompiler">The filter compiler.</param>
    /// <param name="outputWriter">The output writer.</param>
    /// <param name="outputPublisher">Publishes compiled output to its configured durable destination.</param>
    /// <param name="hashDatabaseService">Reads and writes the <c>.hashes.json</c> sidecar database.</param>
    /// <param name="rulesValidatorService">Runs the native bloqr-validator syntax check on the compiled output (#264).</param>
    /// <param name="eventDispatcher">Raises hash-verification and lifecycle events.</param>
    public BloqrCompilerService(
        ILogger<BloqrCompilerService> logger,
        IConfigurationReader configurationReader,
        IFilterCompiler filterCompiler,
        IOutputWriter outputWriter,
        IOutputPublisher outputPublisher,
        IHashDatabaseService hashDatabaseService,
        IBloqrValidatorService rulesValidatorService,
        ICompilationEventDispatcher eventDispatcher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configurationReader = configurationReader ?? throw new ArgumentNullException(nameof(configurationReader));
        _filterCompiler = filterCompiler ?? throw new ArgumentNullException(nameof(filterCompiler));
        _outputWriter = outputWriter ?? throw new ArgumentNullException(nameof(outputWriter));
        _outputPublisher = outputPublisher ?? throw new ArgumentNullException(nameof(outputPublisher));
        _hashDatabaseService = hashDatabaseService ?? throw new ArgumentNullException(nameof(hashDatabaseService));
        _rulesValidatorService = rulesValidatorService ?? throw new ArgumentNullException(nameof(rulesValidatorService));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    /// <inheritdoc/>
    public Task<CompilerResult> RunAsync(
        string? configPath = null,
        string? outputPath = null,
        bool copyToRules = false,
        string? rulesDirectory = null,
        ConfigurationFormat? format = null,
        CancellationToken cancellationToken = default)
    {
        var options = new CompilerOptions
        {
            ConfigPath = configPath,
            OutputPath = outputPath,
            CopyToRules = copyToRules,
            RulesDirectory = rulesDirectory,
            Format = format
        };
        return RunAsync(options, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CompilerResult> RunAsync(
        CompilerOptions options,
        CancellationToken cancellationToken = default)
    {
        // CompilationStarting/Completed/Error were declared on the dispatcher and handled on
        // ICompilationEventHandler, but nothing ever raised them - confirmed by grep before this
        // change (#270). Wrapping the whole run this way, rather than raising Completed/Error at
        // each of RunAsyncCore's several early-return branches, guarantees exactly one raise per
        // outcome regardless of which branch returns.
        var startingArgs = new CompilationStartedEventArgs(options);
        await _eventDispatcher.RaiseCompilationStartingAsync(startingArgs, cancellationToken);

        if (startingArgs.Cancel)
        {
            var cancelResult = new CompilerResult
            {
                Success = false,
                ErrorMessage = startingArgs.CancelReason ?? "Compilation cancelled by an event handler.",
            };
            await _eventDispatcher.RaiseCompilationErrorAsync(
                new CompilationErrorEventArgs(options, new OperationCanceledException(cancelResult.ErrorMessage)),
                cancellationToken);
            return cancelResult;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await RunAsyncCore(options, cancellationToken);
            stopwatch.Stop();

            if (result.Success)
            {
                await _eventDispatcher.RaiseCompilationCompletedAsync(
                    new CompilationCompletedEventArgs(options, result, stopwatch.Elapsed), cancellationToken);
            }
            else
            {
                await _eventDispatcher.RaiseCompilationErrorAsync(
                    new CompilationErrorEventArgs(options, new InvalidOperationException(result.ErrorMessage ?? "Compilation failed.")),
                    cancellationToken);
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _eventDispatcher.RaiseCompilationErrorAsync(new CompilationErrorEventArgs(options, ex), cancellationToken);
            throw;
        }
    }

    private async Task<CompilerResult> RunAsyncCore(
        CompilerOptions options,
        CancellationToken cancellationToken)
    {
        // Resolve config path
        var actualConfigPath = ResolveConfigPath(options.ConfigPath);
        _logger.LogInformation("Starting compilation with config: {ConfigPath}", actualConfigPath);

        // Validate configuration if requested
        if (options.ValidateConfig)
        {
            var validation = await ValidateConfigurationAsync(actualConfigPath, options.Format, cancellationToken);

            if (!validation.IsValid)
            {
                _logger.LogError("Configuration validation failed:");
                foreach (var error in validation.Errors)
                {
                    _logger.LogError("  [{Field}] {Message}", error.Field, error.Message);
                }

                return new CompilerResult
                {
                    Success = false,
                    ErrorMessage = $"Configuration validation failed: {string.Join("; ", validation.Errors.Select(e => $"{e.Field}: {e.Message}"))}"
                };
            }

            if (validation.Warnings.Count > 0)
            {
                foreach (var warning in validation.Warnings)
                {
                    _logger.LogWarning("Configuration warning: [{Field}] {Message}", warning.Field, warning.Message);
                }

                if (options.FailOnWarnings)
                {
                    return new CompilerResult
                    {
                        Success = false,
                        ErrorMessage = $"Configuration has warnings (FailOnWarnings is enabled): {string.Join("; ", validation.Warnings.Select(w => $"{w.Field}: {w.Message}"))}"
                    };
                }
            }
        }

        // Update options with resolved path. FailOnWarnings/AllowUnvalidatedOutput must be
        // carried over explicitly - they're read downstream by ValidateOutputSyntaxAsync, and
        // silently dropping them here would make that method's fail-closed default unreachable
        // for the option that's supposed to opt out of it.
        var compilerOptions = new CompilerOptions
        {
            ConfigPath = actualConfigPath,
            OutputPath = options.OutputPath,
            Format = options.Format,
            Verbose = options.Verbose,
            FailOnWarnings = options.FailOnWarnings,
            AllowUnvalidatedOutput = options.AllowUnvalidatedOutput,
            Engine = options.Engine,
            BrowserOutputPath = options.BrowserOutputPath,
        };

        // Read configuration once for the settings this method acts on directly
        // (output publishing, hash verification) rather than the compiler itself.
        var config = await ReadConfigurationAsync(actualConfigPath, options.Format, cancellationToken);
        await _eventDispatcher.RaiseConfigurationLoadedAsync(
            new ConfigurationLoadedEventArgs(compilerOptions, config), cancellationToken);

        // Stage 1 of the hash-verification pipeline: hash the config file itself for
        // the audit trail. Not checked against the sidecar database - the database's
        // own location and policy live inside this file, so there's nothing to compare against.
        var configHash = await _outputWriter.ComputeHashAsync(actualConfigPath, cancellationToken);
        await _eventDispatcher.RaiseHashComputedAsync(
            new HashComputedEventArgs(compilerOptions, actualConfigPath, "config_file", configHash, new FileInfo(actualConfigPath).Length),
            cancellationToken);

        // Run compilation
        var result = await _filterCompiler.CompileAsync(compilerOptions, cancellationToken);

        if (!result.Success)
        {
            _logger.LogError("Compilation failed: {Error}", result.ErrorMessage);
            return result;
        }

        // Publish to the configured durable destination, if any, applying the
        // conflict strategy and archiving policy before anything downstream sees the file.
        if (config.Output is { } output && !string.IsNullOrWhiteSpace(output.Path))
        {
            var resolvedOutput = new OutputSettings
            {
                Path = ResolvePathRelativeToConfig(output.Path, actualConfigPath),
                ConflictStrategy = output.ConflictStrategy,
            };

            var publishResult = await _outputPublisher.PublishAsync(
                result.OutputPath, resolvedOutput, config.Archiving, cancellationToken);

            if (!publishResult.Success)
            {
                result.Success = false;
                result.ErrorMessage = publishResult.ErrorMessage;
                _logger.LogError("Failed to publish output: {Error}", publishResult.ErrorMessage);
                return result;
            }

            result.OutputPath = publishResult.FinalPath!;
            if (publishResult.ArchivedPath is not null)
            {
                _logger.LogInformation("Archived previous output to {ArchivedPath}", publishResult.ArchivedPath);
            }
        }

        // Count rules and compute hash
        result.RuleCount = await _outputWriter.CountRulesAsync(result.OutputPath, cancellationToken);
        result.OutputHash = await _outputWriter.ComputeHashAsync(result.OutputPath, cancellationToken);

        _logger.LogInformation("Compiled {RuleCount} rules, hash: {Hash}", result.RuleCount, result.OutputHash[..16] + "...");

        await _eventDispatcher.RaiseHashComputedAsync(
            new HashComputedEventArgs(
                compilerOptions, result.OutputPath, "output_file", result.OutputHash, new FileInfo(result.OutputPath).Length),
            cancellationToken);

        var (canContinueAfterValidation, validationErrorMessage) =
            await ValidateOutputSyntaxAsync(result.OutputPath, compilerOptions, cancellationToken);
        if (!canContinueAfterValidation)
        {
            result.Success = false;
            result.ErrorMessage = validationErrorMessage;
            return result;
        }

        if (config.HashVerification is { } outputHashVerification &&
            !string.Equals(outputHashVerification.Mode, "disabled", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(outputHashVerification.HashDatabasePath))
        {
            var hashDatabasePath = ResolvePathRelativeToConfig(outputHashVerification.HashDatabasePath, actualConfigPath);
            var (canContinue, errorMessage) = await VerifyAndRecordHashAsync(
                hashDatabasePath, result.OutputPath, "output_file", result.OutputHash,
                outputHashVerification, compilerOptions, cancellationToken);

            if (!canContinue)
            {
                result.Success = false;
                result.ErrorMessage = errorMessage;
                return result;
            }
        }

        // Dual-engine: a mixed-engine configuration produced a second (browser-syntax)
        // artifact, published/hashed/validated independently of the DNS one above. The
        // .hashes.json sidecar's IHashDatabaseService.RecordAsync already keys entries by
        // itemIdentifier (a path), so recording both artifacts under their own paths in the
        // same database "just works" - no interface change needed for the key shape.
        if (!string.IsNullOrWhiteSpace(result.BrowserOutputPath))
        {
            var canContinueBrowser = await PublishAndVerifyBrowserArtifactAsync(
                result, config, actualConfigPath, compilerOptions, cancellationToken);
            if (!canContinueBrowser)
            {
                return result;
            }
        }

        // Copy to rules directory if requested
        if (options.CopyToRules)
        {
            var rulesPath = ResolveRulesPath(options.RulesDirectory, actualConfigPath);
            result.CopiedToRules = await _outputWriter.CopyOutputAsync(result.OutputPath, rulesPath, cancellationToken);
            result.RulesDestination = rulesPath;

            if (result.CopiedToRules)
            {
                _logger.LogInformation("Copied output to rules directory: {Path}", rulesPath);

                var copiedHash = await _outputWriter.ComputeHashAsync(rulesPath, cancellationToken);
                await _eventDispatcher.RaiseHashComputedAsync(
                    new HashComputedEventArgs(
                        compilerOptions, rulesPath, "copied_rules_file", copiedHash, new FileInfo(rulesPath).Length),
                    cancellationToken);

                if (config.HashVerification is { } copyHashVerification &&
                    !string.Equals(copyHashVerification.Mode, "disabled", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(copyHashVerification.HashDatabasePath))
                {
                    var hashDatabasePath = ResolvePathRelativeToConfig(copyHashVerification.HashDatabasePath, actualConfigPath);
                    var (canContinue, errorMessage) = await VerifyAndRecordHashAsync(
                        hashDatabasePath, rulesPath, "copied_rules_file", copiedHash,
                        copyHashVerification, compilerOptions, cancellationToken);

                    if (!canContinue)
                    {
                        result.Success = false;
                        result.ErrorMessage = errorMessage;
                        return result;
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Publishes, hashes, syntax-validates, and (if configured) hash-verifies the
    /// browser-syntax artifact of a mixed-engine compilation - the same pipeline stages
    /// <see cref="RunAsyncCore"/> already runs for the DNS/primary artifact, applied to
    /// <paramref name="result"/>'s <see cref="CompilerResult.BrowserOutputPath"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Partial-publish behavior</b>: by the time this runs, the DNS artifact (if any) has
    /// already been published successfully - <see cref="RunAsyncCore"/> returns early on that
    /// failure before reaching here. If the browser artifact then fails to publish, hash, or
    /// validate, this method does <i>not</i> roll back the DNS artifact - undoing a successful
    /// publish (including a possible archive-and-overwrite) has its own failure modes and
    /// would destroy a legitimately-published file. Instead, <paramref name="result"/> is
    /// marked unsuccessful with an error message that names the DNS artifact as already
    /// published at <see cref="CompilerResult.OutputPath"/>, so the failure is visible and
    /// actionable rather than silently leaving a half-published pair.
    /// </para>
    /// <para>
    /// The browser-syntax artifact is published to a path derived the same way the compiler
    /// wrapper derives its default (<c>.browser.txt</c>), rooted next to the DNS artifact's
    /// resolved publish destination when one is configured; otherwise the file compiled by
    /// <see cref="IFilterCompiler"/> is used in place, matching how the DNS artifact behaves
    /// when <c>config.Output</c> is unset.
    /// </para>
    /// </remarks>
    /// <returns><see langword="true"/> if compilation can continue; <see langword="false"/> otherwise.</returns>
    private async Task<bool> PublishAndVerifyBrowserArtifactAsync(
        CompilerResult result,
        CompilerConfiguration config,
        string actualConfigPath,
        CompilerOptions compilerOptions,
        CancellationToken cancellationToken)
    {
        var browserOutputPath = result.BrowserOutputPath!;

        if (config.Output is { } output && !string.IsNullOrWhiteSpace(output.Path))
        {
            var resolvedPrimaryPath = ResolvePathRelativeToConfig(output.Path, actualConfigPath);
            var resolvedBrowserDestination = new OutputSettings
            {
                Path = DeriveBrowserArtifactPath(resolvedPrimaryPath),
                ConflictStrategy = output.ConflictStrategy,
            };

            var publishResult = await _outputPublisher.PublishAsync(
                browserOutputPath, resolvedBrowserDestination, config.Archiving, cancellationToken);

            if (!publishResult.Success)
            {
                result.Success = false;
                result.ErrorMessage = "Browser-syntax artifact failed to publish " +
                    $"(DNS artifact was already published successfully at {result.OutputPath}): " +
                    publishResult.ErrorMessage;
                _logger.LogError("Failed to publish browser-syntax output: {Error}", publishResult.ErrorMessage);
                return false;
            }

            browserOutputPath = publishResult.FinalPath!;
            result.BrowserOutputPath = browserOutputPath;
            if (publishResult.ArchivedPath is not null)
            {
                _logger.LogInformation("Archived previous browser-syntax output to {ArchivedPath}", publishResult.ArchivedPath);
            }
        }

        result.BrowserRuleCount = await _outputWriter.CountRulesAsync(browserOutputPath, cancellationToken);
        result.BrowserOutputHash = await _outputWriter.ComputeHashAsync(browserOutputPath, cancellationToken);

        _logger.LogInformation(
            "Compiled browser-syntax artifact: {RuleCount} rules, hash: {Hash}",
            result.BrowserRuleCount, result.BrowserOutputHash[..16] + "...");

        await _eventDispatcher.RaiseHashComputedAsync(
            new HashComputedEventArgs(
                compilerOptions, browserOutputPath, "browser_output_file", result.BrowserOutputHash,
                new FileInfo(browserOutputPath).Length),
            cancellationToken);

        var (canContinueAfterValidation, validationErrorMessage) =
            await ValidateOutputSyntaxAsync(browserOutputPath, compilerOptions, cancellationToken, engine: "browser");
        if (!canContinueAfterValidation)
        {
            result.Success = false;
            result.ErrorMessage = "Browser-syntax artifact failed syntax validation " +
                $"(DNS artifact was already published successfully at {result.OutputPath}): " +
                validationErrorMessage;
            return false;
        }

        if (config.HashVerification is { } browserHashVerification &&
            !string.Equals(browserHashVerification.Mode, "disabled", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(browserHashVerification.HashDatabasePath))
        {
            var hashDatabasePath = ResolvePathRelativeToConfig(browserHashVerification.HashDatabasePath, actualConfigPath);
            var (canContinue, errorMessage) = await VerifyAndRecordHashAsync(
                hashDatabasePath, browserOutputPath, "browser_output_file", result.BrowserOutputHash,
                browserHashVerification, compilerOptions, cancellationToken);

            if (!canContinue)
            {
                result.Success = false;
                result.ErrorMessage = "Browser-syntax artifact failed hash verification " +
                    $"(DNS artifact was already published successfully at {result.OutputPath}): {errorMessage}";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Derives the browser-syntax artifact's publish destination from the DNS/primary
    /// artifact's: <c>.txt</c> is replaced with <c>.browser.txt</c>; any other extension (or
    /// none) has <c>.browser.txt</c> appended. Mirrors the TypeScript CLI's
    /// <c>deriveBrowserOutputPath</c> so every wrapper agrees on the default.
    /// </summary>
    private static string DeriveBrowserArtifactPath(string primaryPath)
    {
        return primaryPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            ? string.Concat(primaryPath.AsSpan(0, primaryPath.Length - ".txt".Length), ".browser.txt")
            : primaryPath + ".browser.txt";
    }

    /// <summary>
    /// Runs the native bloqr-validator syntax check (#264) against the compiled output file
    /// and raises a <c>Validation</c> event with its findings.
    /// </summary>
    /// <remarks>
    /// Fail-closed by default (<see cref="CompilerOptions.AllowUnvalidatedOutput"/>/
    /// <see cref="CompilerOptions.FailOnWarnings"/> control this): an unavailable or failed
    /// native library, or any Error/Critical finding, stops compilation - an
    /// unavailable/failed validator tells us nothing about the output's safety, so it can't
    /// be treated as "no findings" and silently skipped. <see cref="CompilerOptions.FailOnWarnings"/>
    /// additionally escalates Warning findings to abort. A registered handler may still set
    /// <see cref="ValidationEventArgs.Abort"/> explicitly for custom logic, but no handler is
    /// required for the default checks to hold.
    /// </remarks>
    /// <returns>
    /// Whether compilation can continue, and an error message to surface if it cannot.
    /// </returns>
    private async Task<(bool CanContinue, string? ErrorMessage)> ValidateOutputSyntaxAsync(
        string outputPath,
        CompilerOptions options,
        CancellationToken cancellationToken,
        string engine = "dns")
    {
        if (!_rulesValidatorService.IsAvailable)
        {
            return options.AllowUnvalidatedOutput
                ? (true, null)
                : (false, "bloqr-validator native library is unavailable, so compiled output " +
                          "could not be validated. Set CompilerOptions.AllowUnvalidatedOutput " +
                          "to bypass this check (not recommended).");
        }

        // "engine" selects which grammar bloqr-validator-core validates against - "dns" (the
        // default, for the server-side artifact) rejects cosmetic/browser-only syntax by
        // design, while "browser" (passed for the browser-syntax artifact below) natively
        // accepts it. Per #434, this is what makes browser-artifact validation fail-closed
        // WITHOUT needing CompilerOptions.AllowUnvalidatedOutput - see
        // docs/adr/0005-browser-syntax-validation-engine.md.
        var syntaxResult = await _rulesValidatorService.ValidateLocalFileAsync(outputPath, engine, cancellationToken);
        if (syntaxResult is null)
        {
            return options.AllowUnvalidatedOutput
                ? (true, null)
                : (false, $"bloqr-validator could not run against {outputPath}. Set " +
                          "CompilerOptions.AllowUnvalidatedOutput to bypass this check (not recommended).");
        }

        var validationArgs = new ValidationEventArgs(options, "bloqr-validator", new List<ValidationFinding>());
        var severity = syntaxResult.IsValid ? ValidationSeverity.Warning : ValidationSeverity.Error;
        foreach (var message in syntaxResult.Messages)
        {
            validationArgs.AddFinding(severity, "RV001", message, outputPath);
        }

        if (!syntaxResult.IsValid && syntaxResult.Messages.Count == 0)
        {
            validationArgs.AddError(
                "RV001",
                $"Output file failed bloqr-validator syntax validation ({syntaxResult.InvalidRules} invalid rule(s) of {syntaxResult.ValidRules + syntaxResult.InvalidRules}).",
                outputPath);
        }

        await _eventDispatcher.RaiseValidationAsync(validationArgs, cancellationToken);

        var hasWarnings = validationArgs.Findings.Any(f => f.Severity == ValidationSeverity.Warning);
        var shouldAbort = validationArgs.Abort
            || (!options.AllowUnvalidatedOutput
                && (!validationArgs.Passed || (options.FailOnWarnings && hasWarnings)));

        if (shouldAbort)
        {
            return (false, validationArgs.AbortReason ?? $"bloqr-validator validation failed for {outputPath}");
        }

        return (true, null);
    }

    /// <summary>
    /// Compares a freshly computed hash against the sidecar database, raising
    /// <c>HashVerified</c>/<c>HashMismatch</c> events and recording the hash when it is
    /// new or a mismatch was allowed to continue.
    /// </summary>
    /// <returns>
    /// Whether compilation can continue, and an error message to surface if it cannot.
    /// </returns>
    private async Task<(bool CanContinue, string? ErrorMessage)> VerifyAndRecordHashAsync(
        string hashDatabasePath,
        string itemIdentifier,
        string itemType,
        string computedHash,
        HashVerificationSettings settings,
        CompilerOptions options,
        CancellationToken cancellationToken)
    {
        var sizeBytes = new FileInfo(itemIdentifier).Length;
        var entries = await _hashDatabaseService.LoadAsync(hashDatabasePath, cancellationToken);

        if (!entries.TryGetValue(itemIdentifier, out var existing))
        {
            // First time this item has been seen: bootstrap trust for future runs.
            await _hashDatabaseService.RecordAsync(
                hashDatabasePath,
                itemIdentifier,
                new HashDatabaseEntry
                {
                    Hash = computedHash,
                    SizeBytes = sizeBytes,
                    ComputedAt = DateTimeOffset.UtcNow,
                    ItemType = itemType,
                },
                cancellationToken);
            return (true, null);
        }

        if (string.Equals(existing.Hash, computedHash, StringComparison.OrdinalIgnoreCase))
        {
            await _eventDispatcher.RaiseHashVerifiedAsync(
                new HashVerifiedEventArgs(options, itemIdentifier, itemType, existing.Hash, computedHash, sizeBytes, TimeSpan.Zero),
                cancellationToken);
            return (true, null);
        }

        var mismatchArgs = new HashMismatchEventArgs(options, itemIdentifier, itemType, existing.Hash, computedHash, sizeBytes);
        var strict = settings.FailOnMismatch || string.Equals(settings.Mode, "strict", StringComparison.OrdinalIgnoreCase);
        if (!strict)
        {
            mismatchArgs.Abort = false;
            mismatchArgs.AllowContinuation = true;
        }

        await _eventDispatcher.RaiseHashMismatchAsync(mismatchArgs, cancellationToken);

        var shouldAbort = mismatchArgs.Abort && !mismatchArgs.AllowContinuation;
        if (shouldAbort)
        {
            return (false, mismatchArgs.AbortReason ?? $"Hash mismatch for {itemIdentifier}");
        }

        // Continuation was allowed - accept the new hash as the trusted baseline going forward.
        await _hashDatabaseService.RecordAsync(
            hashDatabasePath,
            itemIdentifier,
            new HashDatabaseEntry
            {
                Hash = computedHash,
                SizeBytes = sizeBytes,
                ComputedAt = DateTimeOffset.UtcNow,
                ItemType = itemType,
            },
            cancellationToken);
        return (true, null);
    }

    private static string ResolvePathRelativeToConfig(string path, string configPath)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        var configDirectory = Path.GetDirectoryName(configPath) ?? ".";
        return Path.GetFullPath(Path.Combine(configDirectory, path));
    }

    /// <inheritdoc/>
    public async Task<VersionInfo> GetVersionInfoAsync(CancellationToken cancellationToken = default)
    {
        return await _filterCompiler.GetVersionInfoAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CompilerConfiguration> ReadConfigurationAsync(
        string? configPath = null,
        ConfigurationFormat? format = null,
        CancellationToken cancellationToken = default)
    {
        var actualConfigPath = ResolveConfigPath(configPath);
        return await _configurationReader.ReadConfigurationAsync(actualConfigPath, format, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ValidationResult> ValidateConfigurationAsync(
        string? configPath = null,
        ConfigurationFormat? format = null,
        CancellationToken cancellationToken = default)
    {
        var config = await ReadConfigurationAsync(configPath, format, cancellationToken);
        return ValidateConfiguration(config);
    }

    /// <inheritdoc/>
    public ValidationResult ValidateConfiguration(CompilerConfiguration configuration)
    {
        return ConfigurationValidator.Validate(configuration);
    }

    private static string ResolveConfigPath(string? configPath)
    {
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            if (Path.IsPathRooted(configPath))
                return configPath;

            return Path.GetFullPath(configPath);
        }

        // Try to find default config in common locations
        var searchPaths = new[]
        {
            DefaultConfigFileName,
            Path.Combine("src", "compilers", "typescript", DefaultConfigFileName),
            Path.Combine("..", "compilers", "typescript", DefaultConfigFileName),
            Path.Combine("..", "..", "src", "compilers", "typescript", DefaultConfigFileName)
        };

        foreach (var path in searchPaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
                return fullPath;
        }

        throw new FileNotFoundException(
            $"Configuration file not found. Searched: {string.Join(", ", searchPaths)}. " +
            "Please specify the config path explicitly.");
    }

    private static string ResolveRulesPath(string? rulesDirectory, string configPath)
    {
        if (!string.IsNullOrWhiteSpace(rulesDirectory))
        {
            return Path.Combine(rulesDirectory, DefaultRulesFileName);
        }

        // Default: relative to config location
        var configDir = Path.GetDirectoryName(configPath) ?? ".";
        var defaultRulesDir = Path.Combine(configDir, "..", "..", "rules");

        return Path.GetFullPath(Path.Combine(defaultRulesDir, DefaultRulesFileName));
    }
}
