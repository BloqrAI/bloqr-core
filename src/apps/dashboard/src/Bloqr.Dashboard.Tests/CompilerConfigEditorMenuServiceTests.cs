using Bloqr.Dashboard.Console.Services;

namespace Bloqr.Dashboard.Tests;

/// <summary>
/// Covers <see cref="CompilerConfigEditorMenuService"/>'s round-trip editing (#269, extended by
/// #441). The critical case per #441's own emphasis: editing a mixed-engine config via an
/// unrelated field change must not silently drop <c>engine</c>/<c>defaultEngine</c> - "a silent
/// drop here would be a data-loss bug."
/// </summary>
public sealed class CompilerConfigEditorMenuServiceTests : IDisposable
{
    private readonly string _tempDirectory = Directory.CreateTempSubdirectory("editor-tests-").FullName;
    private readonly string _configPath;
    private readonly FakeConfigurationReader _configurationReader = new();
    private readonly FakeCompilerConfigGuard _configGuard = new();
    private readonly FakeSchemaValidator _schemaValidator = new();
    private readonly FakeVersionHistoryService _versionHistory = new();
    private readonly FakeDashboardPaths _paths;
    private readonly RenamingFakePrompter _prompter;
    private readonly CompilerConfigEditorMenuService _service;

    public CompilerConfigEditorMenuServiceTests()
    {
        _configPath = Path.Combine(_tempDirectory, "mixed.json");
        File.WriteAllText(_configPath, "{}"); // Only needs to exist; the reader/guard are faked.

        _paths = new FakeDashboardPaths(_tempDirectory);
        _prompter = new RenamingFakePrompter("Renamed Filter List", _configPath);
        _service = new CompilerConfigEditorMenuService(
            new NoOpConsoleRenderer(),
            _prompter,
            _configurationReader,
            _configGuard,
            _schemaValidator,
            _versionHistory,
            _paths,
            NullLogger<CompilerConfigEditorMenuService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task EditConfig_UnrelatedRename_PreservesEngineAndDefaultEngineUnchanged()
    {
        var configPath = _configPath;

        var original = new CompilerConfiguration
        {
            Name = "Mixed Engine List",
            DefaultEngine = "browser",
            Output = new OutputSettings { Path = "out.txt", ConflictStrategy = "overwrite" },
            Sources =
            [
                new FilterSource { Name = "Dns Source", Source = "dns.txt", Type = "hosts", Engine = "dns" },
                new FilterSource { Name = "Browser Source", Source = "browser.txt", Type = "adblock", Engine = "browser" },
                new FilterSource { Name = "Auto Source", Source = "auto.txt", Type = "adblock" },
            ],
            Transformations = ["Deduplicate"],
        };

        _configGuard.ConfigurationToReturn = original;
        _schemaValidator.ResultToReturn = new ConfigurationValidationResult(true, []);

        // Exercises the "Edit a compiler config" action end-to-end via reflection on the menu's
        // action dictionary, the same public surface a real menu invocation goes through.
        var actions = InvokeGetMenuActions(_service);
        await actions["Edit a compiler config"]().ConfigureAwait(true);

        // The editor writes the edited config straight back to configPath (same path, JSON
        // format, since the save-path prompt defaults to the current path/extension) - read it
        // back the same way the wizard's own JSONC round-trip tests do.
        var writtenJson = await File.ReadAllTextAsync(configPath).ConfigureAwait(true);
        var deserialized = JsonSerializer.Deserialize<CompilerConfiguration>(writtenJson);
        deserialized.Should().NotBeNull();
        var saved = deserialized!;

        // The rename is the only thing that should have changed.
        saved.Name.Should().Be("Renamed Filter List");

        // The critical assertion: engine/defaultEngine survive an edit that doesn't touch them.
        saved.DefaultEngine.Should().Be("browser");
        saved.Sources.Should().HaveCount(3);
        saved.Sources[0].Engine.Should().Be("dns");
        saved.Sources[1].Engine.Should().Be("browser");
        saved.Sources[2].Engine.Should().BeNull();
    }

    private static Dictionary<string, Func<Task>> InvokeGetMenuActions(CompilerConfigEditorMenuService service)
    {
        var method = typeof(CompilerConfigEditorMenuService).GetMethod(
            "GetMenuActions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (Dictionary<string, Func<Task>>)method.Invoke(service, null)!;
    }

    /// <summary>
    /// A minimal <see cref="IConsolePrompter"/> fake: returns every default value as-is (so every
    /// re-prompted field is preserved unless explicitly overridden below), except the filter-list
    /// name prompt, which it overrides to simulate an unrelated rename edit. <see cref="Select{T}"/>
    /// always returns the first choice, which - because every "current value" prompt in
    /// <c>CompilerConfigFormMenuServiceBase</c> orders the current value first via
    /// <c>WithCurrentFirst</c> - means every such prompt effectively keeps its current value too.
    /// </summary>
    private sealed class RenamingFakePrompter(string newName, string configPath) : IConsolePrompter
    {
        public string Prompt(string prompt, string? defaultValue = null)
        {
            if (prompt == "Filter list name")
            {
                return newName;
            }

            // PromptRequired's "Path to compiler config file to edit" has no defaultValue to fall
            // back on and re-prompts forever on blank - answer it with the config path under test
            // like a real user would, rather than looping.
            if (prompt.StartsWith("Path to compiler config file", StringComparison.Ordinal))
            {
                return configPath;
            }

            return defaultValue ?? string.Empty;
        }

        public Task<string> PromptAsync(
            string prompt, string? defaultValue = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Prompt(prompt, defaultValue));

        public string PromptSecret(string prompt) => string.Empty;

        public bool Confirm(string prompt, bool defaultValue = false) => defaultValue;

        public T Select<T>(string prompt, IEnumerable<T> choices) where T : notnull => choices.First();

        public T Select<T>(string prompt, IEnumerable<T> choices, Func<T, string> displaySelector) where T : notnull =>
            choices.First();

        public IEnumerable<T> MultiSelect<T>(string prompt, IEnumerable<T> choices) where T : notnull => choices;
    }

    private sealed class FakeConfigurationReader : IConfigurationReader
    {
        public CompilerConfiguration? ConfigurationToReturn { get; set; }

        public Task<CompilerConfiguration> ReadConfigurationAsync(
            string configPath, ConfigurationFormat? format = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(ConfigurationToReturn ?? new CompilerConfiguration { Name = "unused" });

        public ConfigurationFormat DetectFormat(string filePath) => ConfigurationFormat.Json;

        public string ToJson(CompilerConfiguration configuration) => "{}";
    }

    private sealed class FakeCompilerConfigGuard : ICompilerConfigGuard
    {
        public CompilerConfiguration? ConfigurationToReturn { get; set; }

        public Task<CompilerConfigGuardResult> LoadAsync(string configPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CompilerConfigGuardResult(true, ConfigurationToReturn, null));

        public Task<CompilerConfigGuardResult> RecoverAsync(string configPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CompilerConfigGuardResult(false, null, "not implemented"));

        public IReadOnlyList<string> ListBackups(string configPath) => [];

        public void PruneBackups(string configPath, int maxBackups)
        {
        }
    }

    private sealed class FakeSchemaValidator : ICompilerConfigSchemaValidator
    {
        public ConfigurationValidationResult ResultToReturn { get; set; } = new(true, []);

        public ConfigurationValidationResult Validate(CompilerConfiguration configuration) => ResultToReturn;
    }

    private sealed class FakeVersionHistoryService : ICompilerConfigVersionHistoryService
    {
        public Task<bool> IsUnderVersionControlAsync(string configPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<CompilerConfigRevision>> GetHistoryAsync(
            string configPath, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CompilerConfigRevision>>([]);

        public Task<string> GetContentAtRevisionAsync(
            string configPath, string revision, CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);

        public Task<string> GetDiffAsync(string configPath, string revision, CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);

        public Task RestoreAsync(string configPath, string revision, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeDashboardPaths(string tempDirectory) : IDashboardPaths
    {
        public string ConfigFilePath => Path.Combine(tempDirectory, "dashboard-config.jsonc");

        public string BackupDirectory => Path.Combine(tempDirectory, "backups");

        public string LogDirectory => Path.Combine(tempDirectory, "logs");

        public string PatternFilesDirectory => Path.Combine(tempDirectory, "patterns");
    }

    private sealed class NoOpConsoleRenderer : IConsoleRenderer
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

        public void WriteStyled(string text, TextStyle style)
        {
        }

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
