using System.Globalization;
using Bloqr.Compiler.Core.Helpers;

namespace Bloqr.Dashboard.Console.Services;

/// <summary>
/// Menu for self-diagnostics: external tool presence, resolved filesystem paths, and
/// configuration health — per the epic's requirement that the Dashboard "be able to
/// troubleshoot itself and report/log errors that are fixable by the user or runtime."
/// </summary>
public sealed class DiagnosticsMenuService : MenuServiceBase
{
    // Remediation guidance for the "deleted binaries" failure mode: a tool being missing isn't
    // something the Dashboard can safely auto-fix (installing arbitrary external tools without
    // asking is exactly the kind of thing the epic elsewhere says to always confirm first), but
    // it can point the user at the fix instead of just reporting "not found".
    private static readonly IReadOnlyDictionary<string, string> RemediationGuidance = new Dictionary<string, string>
    {
        ["dotnet"] = "Install the .NET SDK from https://dotnet.microsoft.com/download (10.0 or later).",
        ["deno"] = "Install Deno from https://deno.com/ - required by the compilation pipeline.",
        ["cargo"] = "Install Rust (which includes cargo) from https://rustup.rs/.",
        ["hostlist-compiler"] = "Optional legacy fallback; install via 'npm install -g @adguard/hostlist-compiler' if needed.",
    };

    private readonly CommandHelper _commandHelper;
    private readonly IDashboardPaths _paths;
    private readonly IDashboardConfigurationStore _configStore;
    private readonly IRulesValidatorService _rulesValidatorService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsMenuService"/> class.
    /// </summary>
    public DiagnosticsMenuService(
        IConsoleRenderer renderer,
        IConsolePrompter prompter,
        CommandHelper commandHelper,
        IDashboardPaths paths,
        IDashboardConfigurationStore configStore,
        IRulesValidatorService rulesValidatorService,
        ILogger<DiagnosticsMenuService> logger)
        : base(renderer, prompter, logger)
    {
        _commandHelper = commandHelper;
        _paths = paths;
        _configStore = configStore;
        _rulesValidatorService = rulesValidatorService;
    }

    /// <inheritdoc />
    public override string Title => "Diagnostics";

    /// <inheritdoc />
    protected override Dictionary<string, Func<Task>> GetMenuActions() => new()
    {
        ["Check external tools"] = CheckExternalToolsAsync,
        ["Show resolved paths"] = ShowResolvedPathsAsync,
        ["Check configuration health"] = CheckConfigurationHealthAsync,
        ["Validate a filter file"] = ValidateFilterFileAsync,
        ["Check AdGuard API configuration"] = CheckAdGuardApiConfigurationAsync,
        ["Run quick benchmark (synthetic)"] = RunQuickBenchmarkAsync,
    };

    private Task CheckExternalToolsAsync()
    {
        var table = new ConsoleTable { Title = "External Tools" };
        table.AddColumn("Tool");
        table.AddColumn("Status");
        table.AddColumn("Path");

        var missingTools = new List<string>();
        foreach (var tool in new[] { "dotnet", "deno", "cargo", "hostlist-compiler" })
        {
            var path = _commandHelper.FindCommand(tool);
            table.AddRow(tool, path is null ? "not found" : "found", path ?? "-");
            if (path is null)
            {
                missingTools.Add(tool);
            }
        }

        table.AddRow(
            "bloqr-validator",
            _rulesValidatorService.IsAvailable ? "found" : "not found",
            "native library (bloqr_validator)");

        Renderer.RenderTable(table);

        if (!_rulesValidatorService.IsAvailable)
        {
            Renderer.WriteLine();
            Renderer.WriteStyled(
                "bloqr-validator native library not found - syntax/URL validation checks will be skipped "
                + "during compilation and here. Build it with 'cargo build --release -p bloqr-validator-core' "
                + "and deploy libbloqr_validator.so (or the platform equivalent) alongside this app (#276).",
                TextStyle.Warning);
        }

        if (missingTools.Count > 0)
        {
            Renderer.WriteLine();
            Renderer.WriteStyled("Missing tools — how to fix:", TextStyle.Warning);
            foreach (var tool in missingTools.Where(RemediationGuidance.ContainsKey))
            {
                Renderer.WriteLine($"  {tool}: {RemediationGuidance[tool]}");
            }
        }

        return Task.CompletedTask;
    }

    private Task ShowResolvedPathsAsync()
    {
        var table = new ConsoleTable { Title = "Resolved Paths" };
        table.AddColumn("Purpose");
        table.AddColumn("Path");
        table.AddRow("Configuration file", _paths.ConfigFilePath);
        table.AddRow("Backup directory", _paths.BackupDirectory);
        table.AddRow("Log directory", _paths.LogDirectory);
        table.AddRow("Pattern files directory", _paths.PatternFilesDirectory);

        Renderer.RenderTable(table);
        return Task.CompletedTask;
    }

    private async Task CheckConfigurationHealthAsync()
    {
        if (!File.Exists(_configStore.ConfigPath))
        {
            Renderer.WriteStyled("No configuration file exists yet; one will be created on first load.", TextStyle.Warning);
            return;
        }

        var load = await _configStore.LoadAsync(allowInteractiveRecovery: true).ConfigureAwait(false);

        if (load.WasRecovered)
        {
            Renderer.WriteStyled($"Configuration required recovery: {load.RecoveryDescription}", TextStyle.Warning);
        }
        else
        {
            Renderer.WriteStyled("Configuration file is present and schema-valid.", TextStyle.Success);
        }

        var backups = _configStore.ListBackups();
        Renderer.WriteLine($"Backups available: {backups.Count}");
    }

    private async Task ValidateFilterFileAsync()
    {
        if (!_rulesValidatorService.IsAvailable)
        {
            Renderer.WriteStyled(
                "bloqr-validator native library is unavailable; see 'Check external tools' for remediation.",
                TextStyle.Warning);
            return;
        }

        var path = Prompter.Prompt("Path to the filter file to validate");
        if (!File.Exists(path))
        {
            Renderer.WriteStyled($"File not found: {path}", TextStyle.Error);
            return;
        }

        var result = await Renderer
            .StatusAsync("Validating filter syntax...", () => _rulesValidatorService.ValidateLocalFileAsync(path))
            .ConfigureAwait(false);

        if (result is null)
        {
            Renderer.WriteStyled("Validation could not be completed; see logs for details.", TextStyle.Error);
            return;
        }

        var table = new ConsoleTable { Title = $"Validation Result: {Path.GetFileName(path)}" };
        table.AddColumn("Field");
        table.AddColumn("Value");
        table.AddRow("Format", result.Format);
        table.AddRow("Valid", result.IsValid ? "yes" : "no");
        table.AddRow("Valid rules", result.ValidRules.ToString(CultureInfo.InvariantCulture));
        table.AddRow("Invalid rules", result.InvalidRules.ToString(CultureInfo.InvariantCulture));
        Renderer.RenderTable(table);

        if (result.Messages.Count > 0)
        {
            Renderer.WriteLine();
            Renderer.WriteStyled("Messages:", result.IsValid ? TextStyle.Warning : TextStyle.Error);
            foreach (var message in result.Messages)
            {
                Renderer.WriteLine($"  {message}");
            }
        }
    }

    private async Task CheckAdGuardApiConfigurationAsync()
    {
        var load = await _configStore.LoadAsync(allowInteractiveRecovery: false).ConfigureAwait(false);
        var settings = load.Configuration.Settings.AdGuardApi;

        var table = new ConsoleTable { Title = "AdGuard API" };
        table.AddColumn("Field");
        table.AddColumn("Value");
        table.AddRow("Enabled", settings.Enabled ? "yes" : "no");
        table.AddRow("Base URL", settings.BaseUrl ?? "-");
        table.AddRow("API key env var", settings.ApiKeyEnvironmentVariable ?? "-");
        Renderer.RenderTable(table);

        Renderer.WriteLine();
        Renderer.WriteStyled(
            "This is a configuration placeholder only - no AdGuard DNS API client is wired in yet. "
            + "See #272: the actual adguard-api-dotnet integration is a separate future issue.",
            TextStyle.Warning);
    }

    private async Task RunQuickBenchmarkAsync()
    {
        var pythonPath = _commandHelper.FindCommand("python3") ?? _commandHelper.FindCommand("python");
        if (pythonPath is null)
        {
            Renderer.WriteStyled(
                "python3 not found on PATH - see 'Check external tools' for remediation.",
                TextStyle.Error);
            return;
        }

        var benchmarksDir = FindBenchmarksDirectory();
        if (benchmarksDir is null)
        {
            Renderer.WriteStyled(
                "benchmarks/quick_benchmark.py not found. This action only works from a full "
                + "source checkout (not a binary-only release) - see docs/architecture/"
                + "release-packaging-strategy.md.",
                TextStyle.Warning);
            return;
        }

        var (exitCode, stdOut, stdErr) = await Renderer
            .StatusAsync(
                "Running synthetic chunking benchmark...",
                () => _commandHelper.ExecuteAsync(pythonPath, "quick_benchmark.py", benchmarksDir))
            .ConfigureAwait(false);

        Renderer.WriteLine();
        if (!string.IsNullOrWhiteSpace(stdOut))
        {
            Renderer.WriteLine(stdOut);
        }

        if (exitCode != 0)
        {
            Renderer.WriteStyled($"quick_benchmark.py exited with code {exitCode}.", TextStyle.Error);
            if (!string.IsNullOrWhiteSpace(stdErr))
            {
                Renderer.WriteLine(stdErr);
            }
        }
    }

    // Walks up from the current directory looking for benchmarks/quick_benchmark.py. Only
    // relevant in source-checkout mode - a binary-only release (#277) doesn't bundle it, so
    // this returning null there is expected, not an error.
    private static string? FindBenchmarksDirectory()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var depth = 0; current is not null && depth < 6; depth++, current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "benchmarks", "quick_benchmark.py");
            if (File.Exists(candidate))
            {
                return Path.Combine(current.FullName, "benchmarks");
            }
        }

        return null;
    }
}
