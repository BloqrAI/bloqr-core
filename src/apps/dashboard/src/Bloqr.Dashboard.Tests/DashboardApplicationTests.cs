using Bloqr.Dashboard.Console.Services;

namespace Bloqr.Dashboard.Tests;

/// <summary>
/// Covers the CLI-switch surface added/touched by #440 - <c>--compile --engine --browser-output</c>
/// and the non-interactive exit path - by exercising <see cref="DashboardApplication.RunAsync"/>
/// directly against fakes, the same way <see cref="DashboardServiceTests"/> exercises
/// <c>DashboardService</c>.
/// </summary>
public sealed class DashboardApplicationTests
{
    private readonly FakeDashboardService _dashboardService = new();
    private readonly FakeConsoleRenderer _renderer = new();
    private readonly FakeConfigurationStore _configStore = new();
    private readonly DashboardApplication _app;

    public DashboardApplicationTests()
    {
        _app = new DashboardApplication(
            _renderer,
            new FakeConsolePrompter(),
            new FakeMenuServiceFactory(),
            _configStore,
            _dashboardService,
            NullLogger<DashboardApplication>.Instance);
    }

    [Fact]
    public async Task Compile_WithEngineAndBrowserOutput_ForwardsBothToDashboardService()
    {
        _dashboardService.CompileResult = new CompilerResult
        {
            Success = true,
            RuleCount = 5,
            OutputPath = "out.txt",
            BrowserOutputPath = "out.browser.txt",
            BrowserRuleCount = 3,
        };

        var exitCode = await _app.RunAsync(
            ["--compile", "config.json", "--engine", "browser", "--browser-output", "out.browser.txt"],
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("config.json", _dashboardService.LastCompileConfigPath);
        Assert.Equal("browser", _dashboardService.LastEngine);
        Assert.Equal("out.browser.txt", _dashboardService.LastBrowserOutputPath);
        Assert.Contains(_renderer.Lines, l => l.Contains("Browser artifact", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Compile_WithoutEngineFlags_PassesNullsThrough()
    {
        _dashboardService.CompileResult = new CompilerResult { Success = true, OutputPath = "out.txt" };

        var exitCode = await _app.RunAsync(["--compile", "config.json"], CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Null(_dashboardService.LastEngine);
        Assert.Null(_dashboardService.LastBrowserOutputPath);
    }

    [Fact]
    public async Task Compile_WithFailedResult_ReturnsNonZeroExitCode()
    {
        _dashboardService.CompileResult = new CompilerResult { Success = false, ErrorMessage = "boom" };

        var exitCode = await _app.RunAsync(["--compile", "config.json"], CancellationToken.None);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task NonInteractive_WithRedirectedStdinFlag_ExitsCleanlyEvenWithUnusedEngineFlags()
    {
        // --engine/--browser-output are peers of --compile only; present without --compile they
        // must not prevent the non-interactive path from exiting cleanly (acceptance criterion).
        var exitCode = await _app.RunAsync(
            ["--non-interactive", "--engine", "browser", "--browser-output", "out.browser.txt"],
            CancellationToken.None);

        Assert.Equal(0, exitCode);
    }

    private sealed class FakeDashboardService : IDashboardService
    {
        public CompilerResult CompileResult { get; set; } = new();
        public string? LastCompileConfigPath { get; private set; }
        public string? LastEngine { get; private set; }
        public string? LastBrowserOutputPath { get; private set; }

        public Task<CompilerResult> CompileAsync(
            string compilerConfigPath,
            string? engine = null,
            string? browserOutputPath = null,
            CancellationToken cancellationToken = default)
        {
            LastCompileConfigPath = compilerConfigPath;
            LastEngine = engine;
            LastBrowserOutputPath = browserOutputPath;
            return Task.FromResult(CompileResult);
        }

        public Task<ValidationResult> ValidateCompilerConfigAsync(
            string compilerConfigPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ValidationResult());

        public Task<IReadOnlyList<string>> ListProfilesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string?> GetActiveProfileAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task ActivateProfileAsync(string profileName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<string>> GetActiveProfileCompilerConfigsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<List<BenchmarkRunResult>> RunBenchmarkAsync(
            string size = "all", string? dataDir = null, int numSources = 4, int maxParallel = 4,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<BenchmarkRunResult>());
    }

    private sealed class FakeConsoleRenderer : IConsoleRenderer
    {
        public List<string> Lines { get; } = [];

        public void WriteLine(string text) => Lines.Add(text);

        public void Write(string text) => Lines.Add(text);

        public void WriteLine() => Lines.Add(string.Empty);

        public void WriteStyled(string text, TextStyle style) => Lines.Add(text);

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

    private sealed class FakeConsolePrompter : IConsolePrompter
    {
        public string Prompt(string prompt, string? defaultValue = null) => defaultValue ?? string.Empty;

        public Task<string> PromptAsync(
            string prompt, string? defaultValue = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(defaultValue ?? string.Empty);

        public string PromptSecret(string prompt) => string.Empty;

        public bool Confirm(string prompt, bool defaultValue = false) => defaultValue;

        public T Select<T>(string prompt, IEnumerable<T> choices) where T : notnull => choices.First();

        public T Select<T>(string prompt, IEnumerable<T> choices, Func<T, string> displaySelector) where T : notnull =>
            choices.First();

        public IEnumerable<T> MultiSelect<T>(string prompt, IEnumerable<T> choices) where T : notnull => choices;
    }

    private sealed class FakeMenuServiceFactory : IMenuServiceFactory
    {
        public IReadOnlyList<IMenuService> GetMenuServices() => [];

        public TMenuService GetMenuService<TMenuService>() where TMenuService : class, IMenuService =>
            throw new NotImplementedException();
    }

    private sealed class FakeConfigurationStore : IDashboardConfigurationStore
    {
        public DashboardConfiguration Configuration { get; } = new();

        public string ConfigPath => "fake-config.jsonc";

        public Task<ConfigurationLoadResult> LoadAsync(
            bool allowInteractiveRecovery, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConfigurationLoadResult(Configuration, false, null));

        public ConfigurationValidationResult Validate(DashboardConfiguration configuration) =>
            throw new NotImplementedException();

        public Task SaveAsync(DashboardConfiguration configuration, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public IReadOnlyList<string> ListBackups() => [];

        public Task<DashboardConfiguration> RestoreFromBackupAsync(
            string backupPath, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
