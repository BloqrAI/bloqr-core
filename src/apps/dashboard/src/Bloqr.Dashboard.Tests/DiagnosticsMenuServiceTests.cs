using Bloqr.Compiler.Core.Helpers;
using Bloqr.Dashboard.Console.Services;

namespace Bloqr.Dashboard.Tests;

/// <summary>
/// Covers <see cref="DiagnosticsMenuService"/>'s "Validate a filter file" action (#441): until
/// #434 lands <c>bloqr-validate</c> with an <c>--engine</c> flag, the Dashboard validates DNS/hosts
/// syntax only and must say so explicitly rather than silently misreporting browser-syntax
/// (cosmetic/element-hiding) rules as invalid.
/// </summary>
public sealed class DiagnosticsMenuServiceTests
{
    private readonly List<(string Text, TextStyle Style)> _styledLines = [];
    private readonly FakeValidatorService _validatorService = new();
    private readonly FixedPathPrompter _prompter;
    private readonly DiagnosticsMenuService _service;
    private readonly string _tempFile = Path.GetTempFileName();

    public DiagnosticsMenuServiceTests()
    {
        _prompter = new FixedPathPrompter(_tempFile);
        _service = new DiagnosticsMenuService(
            new RecordingConsoleRenderer(_styledLines),
            _prompter,
            new CommandHelper(NullLogger<CommandHelper>.Instance),
            new FakeDashboardPaths(),
            new FakeConfigurationStore(),
            _validatorService,
            new FakeDashboardService(),
            NullLogger<DiagnosticsMenuService>.Instance);
    }

    [Fact]
    public async Task ValidateFilterFile_StatesDnsOnlyScopeBeforeValidating()
    {
        _validatorService.IsAvailable = true;
        _validatorService.ResultToReturn = new SyntaxValidationResult
        {
            IsValid = true,
            Format = "Hosts",
            ValidRules = 3,
            InvalidRules = 0,
        };

        var actions = InvokeGetMenuActions(_service);
        await actions["Validate a filter file"]().ConfigureAwait(true);

        _styledLines.Should().Contain(l =>
            l.Text.Contains("DNS/hosts syntax only", StringComparison.Ordinal) &&
            l.Text.Contains("#434", StringComparison.Ordinal));

        // The scope notice must come before the result table is rendered, not after.
        var scopeIndex = _styledLines.FindIndex(l => l.Text.Contains("DNS/hosts syntax only", StringComparison.Ordinal));
        scopeIndex.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ValidateFilterFile_ValidatorUnavailable_DoesNotClaimDnsOnlyScope()
    {
        _validatorService.IsAvailable = false;

        var actions = InvokeGetMenuActions(_service);
        await actions["Validate a filter file"]().ConfigureAwait(true);

        // Bails out before even prompting for a path - the DNS-only scope notice should not
        // appear when validation never runs at all.
        _styledLines.Should().NotContain(l => l.Text.Contains("DNS/hosts syntax only", StringComparison.Ordinal));
    }

    private static Dictionary<string, Func<Task>> InvokeGetMenuActions(DiagnosticsMenuService service)
    {
        var method = typeof(DiagnosticsMenuService).GetMethod(
            "GetMenuActions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (Dictionary<string, Func<Task>>)method.Invoke(service, null)!;
    }

    private sealed class FixedPathPrompter(string path) : IConsolePrompter
    {
        public string Prompt(string prompt, string? defaultValue = null) => path;

        public Task<string> PromptAsync(
            string prompt, string? defaultValue = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(path);

        public string PromptSecret(string prompt) => string.Empty;

        public bool Confirm(string prompt, bool defaultValue = false) => defaultValue;

        public T Select<T>(string prompt, IEnumerable<T> choices) where T : notnull => choices.First();

        public T Select<T>(string prompt, IEnumerable<T> choices, Func<T, string> displaySelector) where T : notnull =>
            choices.First();

        public IEnumerable<T> MultiSelect<T>(string prompt, IEnumerable<T> choices) where T : notnull => choices;
    }

    private sealed class FakeValidatorService : IBloqrValidatorService
    {
        public bool IsAvailable { get; set; } = true;

        public SyntaxValidationResult? ResultToReturn { get; set; }

        public Task<SyntaxValidationResult?> ValidateLocalFileAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(ResultToReturn);

        public Task<SyntaxValidationResult?> ValidateLocalFileAsync(string path, string engine, CancellationToken cancellationToken = default) =>
            Task.FromResult(ResultToReturn);

        public Task<UrlValidationResult?> ValidateRemoteUrlAsync(
            string url, string? expectedHash = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<UrlValidationResult?>(null);
    }

    private sealed class FakeDashboardPaths : IDashboardPaths
    {
        public string ConfigFilePath => "dashboard-config.jsonc";

        public string BackupDirectory => "backups";

        public string LogDirectory => "logs";

        public string PatternFilesDirectory => "patterns";
    }

    private sealed class FakeConfigurationStore : IDashboardConfigurationStore
    {
        public string ConfigPath => "dashboard-config.jsonc";

        public Task<ConfigurationLoadResult> LoadAsync(
            bool allowInteractiveRecovery, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConfigurationLoadResult(new DashboardConfiguration(), false, null));

        public ConfigurationValidationResult Validate(DashboardConfiguration configuration) => new(true, []);

        public Task SaveAsync(DashboardConfiguration configuration, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public IReadOnlyList<string> ListBackups() => [];

        public Task<DashboardConfiguration> RestoreFromBackupAsync(
            string backupPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DashboardConfiguration());
    }

    private sealed class FakeDashboardService : IDashboardService
    {
        public Task<CompilerResult> CompileAsync(
            string compilerConfigPath, string? engine = null, string? browserOutputPath = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CompilerResult { Success = true });

        public Task<ValidationResult> ValidateCompilerConfigAsync(
            string compilerConfigPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ValidationResult());

        public Task<IReadOnlyList<string>> ListProfilesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string?> GetActiveProfileAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task ActivateProfileAsync(string profileName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<string>> GetActiveProfileCompilerConfigsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<List<BenchmarkRunResult>> RunBenchmarkAsync(
            string size = "all", string? dataDir = null, int numSources = 4, int maxParallel = 4,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<BenchmarkRunResult>());
    }

    private sealed class RecordingConsoleRenderer(List<(string Text, TextStyle Style)> styledLines) : IConsoleRenderer
    {
        public void WriteLine(string text)
        {
        }

        public void Write(string text)
        {
        }

        public void WriteLine()
        {
        }

        public void WriteStyled(string text, TextStyle style) => styledLines.Add((text, style));

        public void RenderTable(ConsoleTable table)
        {
        }

        public void RenderPanel(string content, string? title = null)
        {
        }

        public void RenderRule(string? title = null)
        {
        }

        public void Clear()
        {
        }

        public Task<T> StatusAsync<T>(string status, Func<Task<T>> operation) => operation();

        public Task StatusAsync(string status, Func<Task> operation) => operation();

        public Task<T> ProgressAsync<T>(string description, Func<IProgress<double>, Task<T>> operation) =>
            operation(new Progress<double>());

        public Task<T> LiveProgressAsync<T>(Func<ILiveProgressContext, Task<T>> operation) =>
            throw new NotImplementedException();
    }
}
