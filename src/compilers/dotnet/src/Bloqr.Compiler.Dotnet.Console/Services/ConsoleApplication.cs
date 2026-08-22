namespace Bloqr.Compiler.Dotnet.Console.Services;

/// <summary>
/// Main console application that provides both interactive and command-line modes.
/// </summary>
/// <remarks>
/// <para>Command-line options:</para>
/// <list type="bullet">
///   <item><description>--config, -c: Path to configuration file</description></item>
///   <item><description>--output, -o: Path to output file</description></item>
///   <item><description>--copy, --CopyToRules: Copy output to rules directory</description></item>
///   <item><description>--verbose: Enable verbose output from hostlist-compiler</description></item>
///   <item><description>--validate: Validate configuration only (no compilation)</description></item>
///   <item><description>--validate-config: Enable configuration validation before compilation (default: true)</description></item>
///   <item><description>--no-validate-config: Disable configuration validation before compilation</description></item>
///   <item><description>--fail-on-warnings: Fail compilation if configuration has validation warnings</description></item>
///   <item><description>--version, -v: Show version information</description></item>
///   <item><description>--benchmark: Benchmark real compilation performance, chunked vs unchunked, against the canned benchmarks/data/ datasets</description></item>
///   <item><description>--benchmark-size: Dataset size to benchmark: small, medium, large, xlarge, or all (default: all)</description></item>
///   <item><description>--benchmark-data-dir: Directory containing the canned benchmark data (default: auto-discovered)</description></item>
///   <item><description>--benchmark-sources: Number of identical duplicated sources for the chunked run (default: 4)</description></item>
///   <item><description>--benchmark-max-parallel: Max parallel workers for the chunked run (default: CPU count, max 8)</description></item>
///   <item><description>--benchmark-json: Emit machine-readable JSON instead of a human-readable table</description></item>
/// </list>
/// </remarks>
public class ConsoleApplication
{
    private readonly ILogger<ConsoleApplication> _logger;
    private readonly IBloqrCompilerService _compilerService;
    private readonly IChunkingService _chunkingService;
    private readonly IConfigurationReader _configurationReader;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleApplication"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="compilerService">The compiler service.</param>
    /// <param name="chunkingService">The chunking service, used by <c>--benchmark</c>'s chunked run.</param>
    /// <param name="configurationReader">The configuration reader, used to serialize benchmark configs to temp files.</param>
    /// <param name="configuration">The application configuration.</param>
    public ConsoleApplication(
        ILogger<ConsoleApplication> logger,
        IBloqrCompilerService compilerService,
        IChunkingService chunkingService,
        IConfigurationReader configurationReader,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _compilerService = compilerService ?? throw new ArgumentNullException(nameof(compilerService));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _configurationReader = configurationReader ?? throw new ArgumentNullException(nameof(configurationReader));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Runs the console application.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Exit code.</returns>
    public async Task<int> RunAsync(string[] args)
    {
        // Check for command-line mode
        var configPath = _configuration["config"] ?? _configuration["c"];
        var outputPath = _configuration["output"] ?? _configuration["o"];
        var compileOnly = _configuration.GetValue<bool>("compile");
        var copyToRules = _configuration.GetValue<bool>("copy") || _configuration.GetValue<bool>("CopyToRules");
        var showVersion = _configuration.GetValue<bool>("version") || _configuration.GetValue<bool>("v");
        var verbose = _configuration.GetValue<bool>("verbose");
        var validateOnly = _configuration.GetValue<bool>("validate");

        // Check for benchmark mode - parsed directly from raw args, not IConfiguration.
        // IConfiguration's default CommandLineConfigurationProvider unconditionally treats
        // the token right after any "--flag" as that flag's value - even when the next
        // token is itself another "--flag" - so "--benchmark --benchmark-size small" sets
        // config["benchmark"] = "--benchmark-size" and silently drops "small" (and every
        // subsequent --key/value pair shifts out of alignment the same way). "--benchmark"
        // is a bare boolean switch with nothing of its own to consume, so it corrupts
        // parsing of every flag that follows it. Bypassing IConfiguration for the whole
        // benchmark option group sidesteps this rather than relying on flag order. This is
        // a real, pre-existing bug affecting the rest of the CLI's flags too - see #426.
        var runBenchmark = args.Contains("--benchmark", StringComparer.OrdinalIgnoreCase);
        var benchmarkSize = GetArgValue(args, "--benchmark-size") ?? "all";
        var benchmarkDataDir = GetArgValue(args, "--benchmark-data-dir");
        _ = int.TryParse(GetArgValue(args, "--benchmark-sources"), out var benchmarkSources);
        _ = int.TryParse(GetArgValue(args, "--benchmark-max-parallel"), out var benchmarkMaxParallel);
        var benchmarkJson = args.Contains("--benchmark-json", StringComparer.OrdinalIgnoreCase);

        // Parse validation options
        var validateConfig = ParseValidateConfigOption();
        var failOnWarnings = _configuration.GetValue<bool>("fail-on-warnings");

        if (showVersion)
        {
            await ShowVersionInfoAsync();
            return 0;
        }

        if (runBenchmark)
        {
            return await RunBenchmarkAsync(
                benchmarkSize,
                benchmarkDataDir,
                benchmarkSources > 0 ? benchmarkSources : 4,
                benchmarkMaxParallel > 0 ? benchmarkMaxParallel : Math.Min(Environment.ProcessorCount, 8),
                benchmarkJson);
        }

        if (validateOnly)
        {
            return await RunValidationAsync(configPath);
        }

        if (compileOnly || !string.IsNullOrEmpty(configPath))
        {
            // Command-line mode
            return await RunCompilationAsync(configPath, outputPath, copyToRules, verbose, validateConfig, failOnWarnings);
        }

        // Interactive mode
        return await RunInteractiveAsync();
    }

    private async Task<int> RunInteractiveAsync()
    {
        DisplayWelcomeBanner();

        var running = true;
        var exitCode = 0;

        while (running)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]What would you like to do?[/]")
                    .PageSize(12)
                    .HighlightStyle(new Style(Color.Green))
                    .AddChoices(
                        "View Configuration",
                        "Validate Configuration",
                        "Compile Rules",
                        "Compile Rules (Verbose)",
                        "Compile and Copy to Rules",
                        "Show Available Transformations",
                        "Version Info",
                        "Exit"));

            AnsiConsole.WriteLine();

            try
            {
                switch (choice)
                {
                    case "View Configuration":
                        await ViewConfigurationAsync();
                        break;
                    case "Validate Configuration":
                        await ValidateConfigurationAsync();
                        break;
                    case "Compile Rules":
                        await CompileRulesAsync(copyToRules: false, verbose: false);
                        break;
                    case "Compile Rules (Verbose)":
                        await CompileRulesAsync(copyToRules: false, verbose: true);
                        break;
                    case "Compile and Copy to Rules":
                        await CompileRulesAsync(copyToRules: true, verbose: false);
                        break;
                    case "Show Available Transformations":
                        ShowTransformations();
                        break;
                    case "Version Info":
                        await ShowVersionInfoAsync();
                        break;
                    case "Exit":
                        running = false;
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing menu option");
                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
                exitCode = 1;
            }

            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine("[grey]Goodbye![/]");
        return exitCode;
    }

    private static void DisplayWelcomeBanner()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText("Rules Compiler")
                .LeftJustified()
                .Color(Color.Green));

        AnsiConsole.Write(new Rule("[green]AdGuard Filter Rules Compiler[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();
    }

    private async Task ViewConfigurationAsync()
    {
        var configPath = await PromptForConfigPathAsync();

        await AnsiConsole.Status()
            .StartAsync("Reading configuration...", async ctx =>
            {
                var config = await _compilerService.ReadConfigurationAsync(configPath);
                DisplayConfiguration(config);
            });
    }

    private async Task CompileRulesAsync(bool copyToRules, bool verbose)
    {
        var configPath = await PromptForConfigPathAsync();

        await AnsiConsole.Status()
            .StartAsync("Compiling rules...", async ctx =>
            {
                ctx.Status("Reading configuration...");
                var options = new CompilerOptions
                {
                    ConfigPath = configPath,
                    CopyToRules = copyToRules,
                    Verbose = verbose,
                    ValidateConfig = true
                };

                var result = await _compilerService.RunAsync(options);
                DisplayResult(result);

                if (verbose && !string.IsNullOrEmpty(result.StandardOutput))
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.Write(new Panel(Markup.Escape(result.StandardOutput))
                        .Header("[yellow]Compiler Output[/]")
                        .Border(BoxBorder.Rounded));
                }
            });
    }

    /// <summary>
    /// Returns the value immediately following <paramref name="name"/> in <paramref name="args"/>,
    /// or <c>null</c> if the flag isn't present. See the comment on <c>runBenchmark</c> in
    /// <see cref="RunAsync"/> for why the benchmark option group is parsed this way instead
    /// of via <see cref="IConfiguration"/>.
    /// </summary>
    private static string? GetArgValue(string[] args, string name)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>
    /// Parses the validate-config option from configuration.
    /// Defaults to true, but can be disabled with --no-validate-config.
    /// </summary>
    /// <returns>True if validation should be performed, false otherwise.</returns>
    private bool ParseValidateConfigOption()
    {
        // Check for explicit --no-validate-config flag
        if (_configuration.GetValue<bool>("no-validate-config"))
        {
            return false;
        }

        // Check for explicit --validate-config flag
        if (_configuration.GetValue<bool>("validate-config"))
        {
            return true;
        }

        // Default to true (validation enabled by default)
        return true;
    }

    private async Task<int> RunCompilationAsync(string? configPath, string? outputPath, bool copyToRules, bool verbose, bool validateConfig, bool failOnWarnings)
    {
        try
        {
            CompilerResult? result = null;

            await AnsiConsole.Status()
                .StartAsync("Compiling rules...", async ctx =>
                {
                    var options = new CompilerOptions
                    {
                        ConfigPath = configPath,
                        OutputPath = outputPath,
                        CopyToRules = copyToRules,
                        Verbose = verbose,
                        ValidateConfig = validateConfig,
                        FailOnWarnings = failOnWarnings
                    };

                    result = await _compilerService.RunAsync(options);
                    DisplayResult(result);

                    if (verbose && !string.IsNullOrEmpty(result.StandardOutput))
                    {
                        AnsiConsole.WriteLine();
                        AnsiConsole.Write(new Panel(Markup.Escape(result.StandardOutput))
                            .Header("[yellow]Compiler Output[/]")
                            .Border(BoxBorder.Rounded));
                    }
                });

            return result?.Success == true ? 0 : 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static readonly string[] BenchmarkSizes = ["small", "medium", "large", "xlarge"];

    private static readonly string[] BenchmarkTransformations = ["Deduplicate", "RemoveEmptyLines", "TrimLines"];

    /// <summary>
    /// Result of benchmarking one canned dataset size, unchunked vs chunked.
    /// </summary>
    private sealed class BenchmarkRunResult
    {
        [JsonPropertyName("size")]
        public string Size { get; set; } = string.Empty;

        [JsonPropertyName("sources")]
        public int Sources { get; set; }

        [JsonPropertyName("maxParallel")]
        public int MaxParallel { get; set; }

        [JsonPropertyName("unchunkedSuccess")]
        public bool UnchunkedSuccess { get; set; }

        [JsonPropertyName("unchunkedMs")]
        public long UnchunkedMs { get; set; }

        [JsonPropertyName("unchunkedRuleCount")]
        public int UnchunkedRuleCount { get; set; }

        [JsonPropertyName("chunkedSuccess")]
        public bool ChunkedSuccess { get; set; }

        [JsonPropertyName("chunkedMs")]
        public long ChunkedMs { get; set; }

        [JsonPropertyName("chunkedRuleCount")]
        public int ChunkedRuleCount { get; set; }

        [JsonPropertyName("speedup")]
        public double? Speedup { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    /// <summary>
    /// Locates the repo's <c>benchmarks/data</c> directory by walking up from the current
    /// directory, mirroring configuration-file discovery.
    /// </summary>
    private static string? FindBenchmarkDataDir()
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

    /// <summary>
    /// Builds a <see cref="CompilerConfiguration"/> with <paramref name="numSources"/>
    /// identical sources, all pointing at <paramref name="dataPath"/> - the same "N copies
    /// of one file" shape the (otherwise unusable, machine-specific-path) canned
    /// <c>benchmarks/data/config-multi-Nsources.json</c> fixtures use. Identical sources
    /// keep the unchunked and chunked runs directly comparable: same total workload, same
    /// total rule count after dedup, only the chunking strategy differs.
    /// </summary>
    private static CompilerConfiguration BuildBenchmarkConfiguration(string size, string dataPath, int numSources)
    {
        var config = new CompilerConfiguration
        {
            Name = $"Benchmark - {size}",
            Description = $"Real-pipeline benchmark of the '{size}' canned dataset",
            Version = "1.0.0",
            Transformations = [.. BenchmarkTransformations]
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
        CompilerConfiguration config)
    {
        var tempConfigPath = Path.Combine(Path.GetTempPath(), $"benchmark-config-{Guid.NewGuid()}.json");
        var tempOutputPath = Path.Combine(Path.GetTempPath(), $"benchmark-output-{Guid.NewGuid()}.txt");

        try
        {
            var json = _configurationReader.ToJson(config);
            await File.WriteAllTextAsync(tempConfigPath, json);

            var options = new CompilerOptions
            {
                ConfigPath = tempConfigPath,
                OutputPath = tempOutputPath,
                ValidateConfig = false
            };

            var result = await _compilerService.RunAsync(options);
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
    /// Runs the chunked path: splits <paramref name="config"/> into one chunk per source
    /// (the only implemented chunking strategy) and compiles the chunks in parallel through
    /// the real <see cref="IChunkingService"/> pipeline, up to <paramref name="maxParallel"/>
    /// at a time.
    /// </summary>
    private async Task<(bool Success, long ElapsedMs, int RuleCount, string? Error)> RunChunkedAsync(
        CompilerConfiguration config,
        int maxParallel)
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
            var result = await _chunkingService.CompileChunksAsync(chunks, options, chunkingOptions);

            var error = result.Success ? null : string.Join("; ", result.Errors);
            return (result.Success, result.TotalElapsedMs, result.FinalRuleCount, error);
        }
        catch (Exception ex)
        {
            return (false, 0, 0, ex.Message);
        }
    }

    /// <summary>
    /// Benchmarks real compilation performance (chunked vs unchunked) across the requested
    /// canned dataset size(s), using the same <see cref="IBloqrCompilerService"/>/
    /// <see cref="IChunkingService"/> pipeline the <c>compile</c> command uses - not a
    /// synthetic simulation.
    /// </summary>
    /// <remarks>
    /// The unchunked and chunked paths currently shell out to two different underlying
    /// compilers (see #424): <see cref="IBloqrCompilerService"/> uses Deno + the JSR
    /// <c>@bloqr/compiler-core</c> package, <see cref="IChunkingService"/> uses
    /// <c>hostlist-compiler</c>/<c>npx</c> directly. Part of any timing delta may reflect
    /// that difference rather than chunking overhead alone, and each side needs its own
    /// tool installed to succeed.
    /// </remarks>
    private async Task<int> RunBenchmarkAsync(
        string size,
        string? dataDir,
        int numSources,
        int maxParallel,
        bool jsonOutput)
    {
        var sizes = size.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? BenchmarkSizes
            : [size];

        var invalidSize = sizes.FirstOrDefault(s => !BenchmarkSizes.Contains(s, StringComparer.OrdinalIgnoreCase));
        if (invalidSize is not null)
        {
            AnsiConsole.MarkupLine(
                $"[red]Unknown benchmark size '{Markup.Escape(invalidSize)}'. Expected one of: {string.Join(", ", BenchmarkSizes)}, or 'all'.[/]");
            return 1;
        }

        var resolvedDataDir = dataDir ?? FindBenchmarkDataDir();
        if (resolvedDataDir is null)
        {
            AnsiConsole.MarkupLine("[red]Could not find a benchmarks/data directory.[/]");
            AnsiConsole.MarkupLine("[grey]Pass --benchmark-data-dir to point at one explicitly, or run this[/]");
            AnsiConsole.MarkupLine("[grey]from within a clone of BloqrAI/bloqr-core.[/]");
            return 1;
        }

        if (!jsonOutput)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[green]Chunking Performance Benchmark (real compiler pipeline)[/]").RuleStyle("grey"));
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[grey]Data directory:[/] {Markup.Escape(resolvedDataDir)}");
            AnsiConsole.MarkupLine($"[grey]Sources per dataset:[/] {numSources} (identical copies, one per chunk)");
            AnsiConsole.MarkupLine($"[grey]Max parallel workers:[/] {maxParallel}");
            AnsiConsole.WriteLine();
        }

        var results = new List<BenchmarkRunResult>();

        foreach (var s in sizes)
        {
            var dataPath = Path.Combine(resolvedDataDir, $"{s}.txt");
            if (!File.Exists(dataPath))
            {
                var missingMsg = $"dataset file not found: {dataPath}";
                if (!jsonOutput)
                {
                    AnsiConsole.MarkupLine($"[yellow]SKIP {s}:[/] {Markup.Escape(missingMsg)}");
                }

                results.Add(new BenchmarkRunResult
                {
                    Size = s,
                    Sources = numSources,
                    MaxParallel = maxParallel,
                    Error = missingMsg
                });
                continue;
            }

            var config = BuildBenchmarkConfiguration(s, dataPath, numSources);

            (bool Success, long ElapsedMs, int RuleCount, string? Error) unchunked = default;
            (bool Success, long ElapsedMs, int RuleCount, string? Error) chunked = default;

            if (jsonOutput)
            {
                unchunked = await RunUnchunkedAsync(config);
                chunked = await RunChunkedAsync(config, maxParallel);
            }
            else
            {
                await AnsiConsole.Status()
                    .StartAsync($"Benchmarking '{s}' ({numSources} sources)...", async ctx =>
                    {
                        unchunked = await RunUnchunkedAsync(config);
                        chunked = await RunChunkedAsync(config, maxParallel);
                    });
            }

            double? speedup = unchunked.Success && chunked.Success && chunked.ElapsedMs > 0
                ? (double)unchunked.ElapsedMs / chunked.ElapsedMs
                : null;

            if (!jsonOutput)
            {
                var speedupText = speedup.HasValue ? $", {speedup.Value:F2}x" : string.Empty;
                AnsiConsole.MarkupLine(
                    $"[green]✓[/] {Markup.Escape(s)}: unchunked {unchunked.ElapsedMs}ms, chunked {chunked.ElapsedMs}ms{speedupText}");
            }

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

        if (jsonOutput)
        {
            var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
            // Plain Console.WriteLine, not AnsiConsole - --benchmark-json output is meant to be
            // piped into other tools (the root comparison script), so it should be exactly the
            // JSON text with nothing else mixed in, unaffected by Spectre's console-width/ANSI
            // detection in non-interactive environments.
            System.Console.WriteLine(json);
        }
        else
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[yellow]Results[/]").RuleStyle("grey"));
            AnsiConsole.WriteLine();

            var resultsTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[green]Size[/]")
                .AddColumn("[green]Unchunked[/]")
                .AddColumn("[green]Chunked[/]")
                .AddColumn("[green]Speedup[/]")
                .AddColumn("[green]Rules[/]");

            foreach (var r in results)
            {
                if (!r.UnchunkedSuccess && !r.ChunkedSuccess)
                {
                    resultsTable.AddRow(r.Size, $"[red]FAILED: {Markup.Escape(r.Error ?? "unknown")}[/]", "", "", "");
                    continue;
                }

                resultsTable.AddRow(
                    r.Size,
                    $"{r.UnchunkedMs:N0} ms",
                    $"{r.ChunkedMs:N0} ms",
                    r.Speedup.HasValue ? $"{r.Speedup.Value:F2}x" : "n/a",
                    r.ChunkedRuleCount.ToString("N0"));
            }

            AnsiConsole.Write(resultsTable);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Note: this exercises the real compiler pipeline, so results depend on this[/]");
            AnsiConsole.MarkupLine("[grey]machine's CPU/I-O characteristics. Unchunked needs Deno on PATH; chunked[/]");
            AnsiConsole.MarkupLine("[grey]needs hostlist-compiler or npx on PATH - see #424 (they aren't the same[/]");
            AnsiConsole.MarkupLine("[grey]underlying compiler today).[/]");
            AnsiConsole.WriteLine();
        }

        return results.Any(r => !r.UnchunkedSuccess && !r.ChunkedSuccess) ? 1 : 0;
    }

    private async Task<int> RunValidationAsync(string? configPath)
    {
        try
        {
            var result = await _compilerService.ValidateConfigurationAsync(configPath);
            DisplayValidationResult(result);
            return result.IsValid ? 0 : 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private async Task ValidateConfigurationAsync()
    {
        var configPath = await PromptForConfigPathAsync();

        await AnsiConsole.Status()
            .StartAsync("Validating configuration...", async ctx =>
            {
                var result = await _compilerService.ValidateConfigurationAsync(configPath);
                DisplayValidationResult(result);
            });
    }

    private static void ShowTransformations()
    {
        AnsiConsole.Write(new Rule("[green]Available Transformations[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[green]Transformation[/]")
            .AddColumn("[green]Description[/]");

        table.AddRow("RemoveComments", "Removes all comment lines (! or #)");
        table.AddRow("Compress", "Converts hosts format to adblock syntax");
        table.AddRow("RemoveModifiers", "Removes unsupported modifiers from rules");
        table.AddRow("Validate", "Removes dangerous/incompatible rules");
        table.AddRow("ValidateAllowIp", "Like Validate but allows IP address rules");
        table.AddRow("Deduplicate", "Removes duplicate rules");
        table.AddRow("InvertAllow", "Converts @@exceptions to blocking rules");
        table.AddRow("RemoveEmptyLines", "Removes blank lines");
        table.AddRow("TrimLines", "Trims whitespace from lines");
        table.AddRow("InsertFinalNewLine", "Ensures file ends with newline");
        table.AddRow("ConvertToAscii", "Converts IDN to punycode");

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Note: Transformations are always applied in a fixed order regardless of configuration order.[/]");
    }

    private static void DisplayValidationResult(Bloqr.Compiler.Abstractions.ValidationResult result)
    {
        AnsiConsole.WriteLine();

        if (result.IsValid && result.Warnings.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]Configuration is valid![/]");
            return;
        }

        if (result.IsValid)
        {
            AnsiConsole.MarkupLine("[yellow]Configuration is valid but has warnings:[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Configuration has errors:[/]");
        }

        if (result.Errors.Count > 0)
        {
            var errorsTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[red]Field[/]")
                .AddColumn("[red]Error[/]");

            foreach (var error in result.Errors)
            {
                errorsTable.AddRow(
                    Markup.Escape(error.Field),
                    Markup.Escape(error.Message));
            }

            AnsiConsole.Write(errorsTable);
        }

        if (result.Warnings.Count > 0)
        {
            AnsiConsole.WriteLine();
            var warningsTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[yellow]Field[/]")
                .AddColumn("[yellow]Warning[/]");

            foreach (var warning in result.Warnings)
            {
                warningsTable.AddRow(
                    Markup.Escape(warning.Field),
                    Markup.Escape(warning.Message));
            }

            AnsiConsole.Write(warningsTable);
        }
    }

    private async Task ShowVersionInfoAsync()
    {
        var info = await _compilerService.GetVersionInfoAsync();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[green]Component[/]")
            .AddColumn("[green]Version[/]");

        table.AddRow("Module", info.ModuleVersion);
        table.AddRow(".NET Runtime", info.DotNetVersion);
        table.AddRow("Node.js", info.NodeVersion ?? "[grey]Not found[/]");
        table.AddRow("hostlist-compiler", info.HostlistCompilerVersion ?? "[grey]Not found[/]");
        table.AddRow("Platform", $"{info.Platform.OSName} {info.Platform.Architecture}");

        AnsiConsole.Write(table);
    }

    private async Task<string?> PromptForConfigPathAsync()
    {
        var defaultPath = _configuration["config"];

        if (!string.IsNullOrEmpty(defaultPath))
            return defaultPath;

        return AnsiConsole.Prompt(
            new TextPrompt<string>("Enter config path (or press Enter for default):")
                .AllowEmpty());
    }

    private static void DisplayConfiguration(CompilerConfiguration config)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[green]Property[/]")
            .AddColumn("[green]Value[/]");

        table.AddRow("Name", Markup.Escape(config.Name));
        table.AddRow("Description", Markup.Escape(config.Description ?? "[grey]Not set[/]"));
        table.AddRow("Version", config.Version ?? "[grey]Not set[/]");
        table.AddRow("License", config.License ?? "[grey]Not set[/]");
        table.AddRow("Homepage", Markup.Escape(config.Homepage ?? "[grey]Not set[/]"));
        table.AddRow("Sources", config.Sources.Count.ToString());
        table.AddRow("Transformations", config.Transformations.Count > 0
            ? string.Join(", ", config.Transformations)
            : "[grey]None[/]");
        table.AddRow("Inclusions", config.Inclusions.Count > 0
            ? $"{config.Inclusions.Count} patterns"
            : "[grey]None[/]");
        table.AddRow("Inclusion Sources", config.InclusionsSources.Count > 0
            ? $"{config.InclusionsSources.Count} files"
            : "[grey]None[/]");
        table.AddRow("Exclusions", config.Exclusions.Count > 0
            ? $"{config.Exclusions.Count} patterns"
            : "[grey]None[/]");
        table.AddRow("Exclusion Sources", config.ExclusionsSources.Count > 0
            ? $"{config.ExclusionsSources.Count} files"
            : "[grey]None[/]");

        AnsiConsole.Write(table);

        if (config.Sources.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]Sources:[/]");

            var sourcesTable = new Table()
                .Border(TableBorder.Simple)
                .AddColumn("Name")
                .AddColumn("Type")
                .AddColumn("Source")
                .AddColumn("Transformations");

            foreach (var source in config.Sources)
            {
                sourcesTable.AddRow(
                    Markup.Escape(source.Name ?? "[unnamed]"),
                    source.Type,
                    Markup.Escape(source.Source.Length > 50
                        ? source.Source[..47] + "..."
                        : source.Source),
                    source.Transformations.Count > 0
                        ? string.Join(", ", source.Transformations)
                        : "[grey]None[/]");
            }

            AnsiConsole.Write(sourcesTable);
        }
    }

    private static void DisplayResult(CompilerResult result)
    {
        AnsiConsole.WriteLine();

        if (result.Success)
        {
            AnsiConsole.MarkupLine("[green]Compilation successful![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Compilation failed![/]");
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(result.ErrorMessage)}[/]");
            }
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[green]Property[/]")
            .AddColumn("[green]Value[/]");

        table.AddRow("Config Name", Markup.Escape(result.ConfigName));
        table.AddRow("Config Version", result.ConfigVersion ?? "[grey]Not set[/]");
        table.AddRow("Rule Count", result.RuleCount.ToString("N0"));
        table.AddRow("Output Path", Markup.Escape(result.OutputPath));
        table.AddRow("Output Hash", !string.IsNullOrEmpty(result.OutputHash) && result.OutputHash.Length >= 32
            ? result.OutputHash[..32] + "..."
            : result.OutputHash ?? "[grey]N/A[/]");
        table.AddRow("Elapsed Time", $"{result.ElapsedMs:N0} ms");

        if (result.CopiedToRules)
        {
            table.AddRow("Copied To", Markup.Escape(result.RulesDestination ?? "N/A"));
        }

        AnsiConsole.Write(table);
    }
}
