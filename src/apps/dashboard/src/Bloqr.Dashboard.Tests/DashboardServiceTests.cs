using Bloqr.Dashboard.Core.Services;

namespace Bloqr.Dashboard.Tests;

public sealed class DashboardServiceTests
{
    private readonly FakeBloqrCompilerService _compilerService = new();
    private readonly FakeBenchmarkService _benchmarkService = new();
    private readonly FakeDashboardConfigurationStore _configStore = new();
    private readonly ProfileManager _profileManager = new();
    private readonly DashboardService _service;

    public DashboardServiceTests()
    {
        _service = new DashboardService(_compilerService, _benchmarkService, _configStore, _profileManager);
    }

    [Fact]
    public async Task CompileAsync_DelegatesToBloqrCompilerService()
    {
        _compilerService.RunResult = new CompilerResult { Success = true, RuleCount = 42 };

        var result = await _service.CompileAsync("config.json");

        Assert.True(result.Success);
        Assert.Equal(42, result.RuleCount);
        Assert.Equal("config.json", _compilerService.LastRunConfigPath);
        Assert.Null(_compilerService.LastRunOptions?.Engine);
        Assert.Null(_compilerService.LastRunOptions?.BrowserOutputPath);
    }

    [Fact]
    public async Task CompileAsync_ForcedToBrowserEngine_ForwardsEngineAndBrowserOutputPath()
    {
        _compilerService.RunResult = new CompilerResult
        {
            Success = true,
            RuleCount = 10,
            BrowserOutputPath = "out.browser.txt",
            BrowserOutputHash = "deadbeef",
            BrowserRuleCount = 10,
        };

        var result = await _service.CompileAsync("config.json", engine: "browser", browserOutputPath: "out.browser.txt");

        Assert.True(result.Success);
        Assert.Equal("out.browser.txt", result.BrowserOutputPath);
        Assert.Equal("config.json", _compilerService.LastRunOptions?.ConfigPath);
        Assert.Equal("browser", _compilerService.LastRunOptions?.Engine);
        Assert.Equal("out.browser.txt", _compilerService.LastRunOptions?.BrowserOutputPath);
    }

    [Fact]
    public async Task ValidateCompilerConfigAsync_DelegatesToBloqrCompilerService()
    {
        var invalid = new ValidationResult();
        invalid.AddError("sources", "must not be empty");
        _compilerService.ValidateResult = invalid;

        var result = await _service.ValidateCompilerConfigAsync("config.json");

        Assert.False(result.IsValid);
        Assert.Equal("config.json", _compilerService.LastValidateConfigPath);
    }

    [Fact]
    public async Task ListProfilesAsync_ReturnsProfileNames()
    {
        _configStore.Configuration.Profiles["a"] = new DashboardProfile();
        _configStore.Configuration.Profiles["b"] = new DashboardProfile();

        var profiles = await _service.ListProfilesAsync();

        Assert.Equal(["a", "b"], profiles.OrderBy(p => p));
    }

    [Fact]
    public async Task GetActiveProfileAsync_ReturnsConfiguredActiveProfile()
    {
        _configStore.Configuration.Settings.ActiveProfile = "prod";

        var active = await _service.GetActiveProfileAsync();

        Assert.Equal("prod", active);
    }

    [Fact]
    public async Task ActivateProfileAsync_SetsActiveProfileAndSaves()
    {
        _configStore.Configuration.Profiles["prod"] = new DashboardProfile();

        await _service.ActivateProfileAsync("prod");

        Assert.Equal("prod", _configStore.Configuration.Settings.ActiveProfile);
        Assert.True(_configStore.SaveCalled);
    }

    [Fact]
    public async Task ActivateProfileAsync_WithUnknownProfile_ThrowsAndDoesNotSave()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ActivateProfileAsync("nope"));
        Assert.False(_configStore.SaveCalled);
    }

    [Fact]
    public async Task GetActiveProfileCompilerConfigsAsync_WithNoActiveProfile_ReturnsEmpty()
    {
        var configs = await _service.GetActiveProfileCompilerConfigsAsync();

        Assert.Empty(configs);
    }

    [Fact]
    public async Task GetActiveProfileCompilerConfigsAsync_WithActiveProfile_ReturnsItsConfigs()
    {
        _configStore.Configuration.Settings.ActiveProfile = "prod";
        _configStore.Configuration.Profiles["prod"] = new DashboardProfile { CompilerConfigs = ["a.json", "b.json"] };

        var configs = await _service.GetActiveProfileCompilerConfigsAsync();

        Assert.Equal(["a.json", "b.json"], configs);
    }

    [Fact]
    public async Task RunBenchmarkAsync_DelegatesToBenchmarkService()
    {
        _benchmarkService.Result = [new BenchmarkRunResult { Size = "small", UnchunkedSuccess = true }];

        var results = await _service.RunBenchmarkAsync("small", numSources: 2, maxParallel: 1);

        Assert.Equal("small", _benchmarkService.LastSize);
        Assert.Equal(2, _benchmarkService.LastNumSources);
        Assert.Equal(1, _benchmarkService.LastMaxParallel);
        Assert.Single(results);
        Assert.Equal("small", results[0].Size);
    }

    private sealed class FakeBenchmarkService : IBenchmarkService
    {
        public List<BenchmarkRunResult> Result { get; set; } = [];
        public string? LastSize { get; private set; }
        public int LastNumSources { get; private set; }
        public int LastMaxParallel { get; private set; }

        public IReadOnlyList<string> BenchmarkSizes => ["small", "medium", "large", "xlarge"];

        public string? FindBenchmarkDataDir() => null;

        public Task<List<BenchmarkRunResult>> RunBenchmarkAsync(
            string size, string? dataDir, int numSources, int maxParallel,
            CancellationToken cancellationToken = default)
        {
            LastSize = size;
            LastNumSources = numSources;
            LastMaxParallel = maxParallel;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeBloqrCompilerService : IBloqrCompilerService
    {
        public CompilerResult RunResult { get; set; } = new();
        public ValidationResult ValidateResult { get; set; } = new();
        public string? LastRunConfigPath { get; private set; }
        public CompilerOptions? LastRunOptions { get; private set; }
        public string? LastValidateConfigPath { get; private set; }

        public Task<CompilerResult> RunAsync(
            string? configPath = null, string? outputPath = null, bool copyToRules = false,
            string? rulesDirectory = null, ConfigurationFormat? format = null,
            CancellationToken cancellationToken = default)
        {
            LastRunConfigPath = configPath;
            return Task.FromResult(RunResult);
        }

        public Task<CompilerResult> RunAsync(CompilerOptions options, CancellationToken cancellationToken = default)
        {
            LastRunConfigPath = options.ConfigPath;
            LastRunOptions = options;
            return Task.FromResult(RunResult);
        }

        public Task<VersionInfo> GetVersionInfoAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<CompilerConfiguration> ReadConfigurationAsync(
            string? configPath = null, ConfigurationFormat? format = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ValidationResult> ValidateConfigurationAsync(
            string? configPath = null, ConfigurationFormat? format = null, CancellationToken cancellationToken = default)
        {
            LastValidateConfigPath = configPath;
            return Task.FromResult(ValidateResult);
        }

        public ValidationResult ValidateConfiguration(CompilerConfiguration configuration) =>
            throw new NotImplementedException();
    }

    private sealed class FakeDashboardConfigurationStore : IDashboardConfigurationStore
    {
        public DashboardConfiguration Configuration { get; } = new();
        public bool SaveCalled { get; private set; }

        public string ConfigPath => "fake-config.jsonc";

        public Task<ConfigurationLoadResult> LoadAsync(
            bool allowInteractiveRecovery, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConfigurationLoadResult(Configuration, false, null));

        public ConfigurationValidationResult Validate(DashboardConfiguration configuration) =>
            throw new NotImplementedException();

        public Task SaveAsync(DashboardConfiguration configuration, CancellationToken cancellationToken = default)
        {
            SaveCalled = true;
            return Task.CompletedTask;
        }

        public IReadOnlyList<string> ListBackups() => throw new NotImplementedException();

        public Task<DashboardConfiguration> RestoreFromBackupAsync(
            string backupPath, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
