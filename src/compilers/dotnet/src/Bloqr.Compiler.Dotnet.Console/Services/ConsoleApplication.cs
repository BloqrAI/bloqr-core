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
    private readonly IBenchmarkService _benchmarkService;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleApplication"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="compilerService">The compiler service.</param>
    /// <param name="benchmarkService">The benchmark service backing <c>--benchmark</c>.</param>
    /// <param name="configuration">The application configuration.</param>
    public ConsoleApplication(
        ILogger<ConsoleApplication> logger,
        IBloqrCompilerService compilerService,
        IBenchmarkService benchmarkService,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _compilerService = compilerService ?? throw new ArgumentNullException(nameof(compilerService));
        _benchmarkService = benchmarkService ?? throw new ArgumentNullException(nameof(benchmarkService));
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

    /// <summary>
    /// Benchmarks real compilation performance (chunked vs unchunked) across the requested
    /// canned dataset size(s), via the shared <see cref="IBenchmarkService"/> (#423) - not a
    /// synthetic simulation. Purely a rendering layer: all benchmarking logic lives in
    /// <see cref="IBenchmarkService"/> so the Dashboard's Diagnostics menu can reuse it too.
    /// </summary>
    private async Task<int> RunBenchmarkAsync(
        string size,
        string? dataDir,
        int numSources,
        int maxParallel,
        bool jsonOutput)
    {
        List<BenchmarkRunResult> results;

        try
        {
            if (jsonOutput)
            {
                results = await _benchmarkService.RunBenchmarkAsync(size, dataDir, numSources, maxParallel);
            }
            else
            {
                var resolvedDataDir = dataDir ?? _benchmarkService.FindBenchmarkDataDir();
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Rule("[green]Chunking Performance Benchmark (real compiler pipeline)[/]").RuleStyle("grey"));
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[grey]Data directory:[/] {Markup.Escape(resolvedDataDir ?? "(not found)")}");
                AnsiConsole.MarkupLine($"[grey]Sources per dataset:[/] {numSources} (identical copies, one per chunk)");
                AnsiConsole.MarkupLine($"[grey]Max parallel workers:[/] {maxParallel}");
                AnsiConsole.WriteLine();

                results = await AnsiConsole.Status()
                    .StartAsync(
                        $"Benchmarking (size: {size}, {numSources} sources)...",
                        _ => _benchmarkService.RunBenchmarkAsync(size, dataDir, numSources, maxParallel));

                foreach (var r in results)
                {
                    if (!r.UnchunkedSuccess && !r.ChunkedSuccess)
                    {
                        AnsiConsole.MarkupLine($"[yellow]SKIP {r.Size}:[/] {Markup.Escape(r.Error ?? "unknown")}");
                        continue;
                    }

                    var speedupText = r.Speedup.HasValue ? $", {r.Speedup.Value:F2}x" : string.Empty;
                    AnsiConsole.MarkupLine(
                        $"[green]✓[/] {Markup.Escape(r.Size)}: unchunked {r.UnchunkedMs}ms, chunked {r.ChunkedMs}ms{speedupText}");
                }
            }
        }
        catch (ArgumentException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }
        catch (DirectoryNotFoundException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
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
