namespace Bloqr.Dashboard.Tests;

public sealed class CompilerConfigGuardTests : IDisposable
{
    private const string ValidConfigJson = """
        { "name": "test-list", "sources": [ { "source": "https://example.com/list.txt" } ] }
        """;

    private readonly string _rootDirectory;
    private readonly string _configDirectory;
    private readonly DashboardPaths _paths;
    private readonly CompilerConfigGuard _guard;

    public CompilerConfigGuardTests()
    {
        _rootDirectory = Directory.CreateTempSubdirectory("bloqr-dashboard-tests-").FullName;
        _configDirectory = Path.Combine(_rootDirectory, "configs");
        Directory.CreateDirectory(_configDirectory);
        _paths = DashboardPaths.ForDirectory(Path.Combine(_rootDirectory, "dashboard"));
        _guard = new CompilerConfigGuard(
            new ConfigurationReader(NullLogger<ConfigurationReader>.Instance),
            _paths,
            NullLogger<CompilerConfigGuard>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WithValidConfig_SucceedsAndCreatesABackup()
    {
        var configPath = WriteConfig("compiler-config.json", ValidConfigJson);

        var result = await _guard.LoadAsync(configPath);

        result.Success.Should().BeTrue();
        result.Configuration.Should().NotBeNull();
        result.Configuration!.Name.Should().Be("test-list");
        _guard.ListBackups(configPath).Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadAsync_CalledTwiceWithUnchangedContent_DoesNotDuplicateTheBackup()
    {
        var configPath = WriteConfig("compiler-config.json", ValidConfigJson);

        await _guard.LoadAsync(configPath);
        await _guard.LoadAsync(configPath);

        _guard.ListBackups(configPath).Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadAsync_WhenConfigChanges_CreatesAnAdditionalBackup()
    {
        var configPath = WriteConfig("compiler-config.json", ValidConfigJson);
        await _guard.LoadAsync(configPath);

        File.WriteAllText(
            configPath,
            """{ "name": "test-list-v2", "sources": [ { "source": "https://example.com/list.txt" } ] }""");
        await Task.Delay(5); // ensure a distinct backup filename timestamp
        await _guard.LoadAsync(configPath);

        _guard.ListBackups(configPath).Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAsync_WithMissingFile_ReturnsFailure()
    {
        var configPath = Path.Combine(_configDirectory, "does-not-exist.json");

        var result = await _guard.LoadAsync(configPath);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task LoadAsync_WithMalformedJson_ReturnsFailureAndDoesNotBackUp()
    {
        var configPath = WriteConfig("compiler-config.json", "{ not valid json");

        var result = await _guard.LoadAsync(configPath);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Failed to parse");
        _guard.ListBackups(configPath).Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WithNoSources_FailsValidation()
    {
        var configPath = WriteConfig("compiler-config.json", """{ "name": "test-list", "sources": [] }""");

        var result = await _guard.LoadAsync(configPath);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("sources");
    }

    [Fact]
    public async Task RecoverAsync_WithValidBackup_QuarantinesCorruptFileAndRestores()
    {
        var configPath = WriteConfig("compiler-config.json", ValidConfigJson);
        await _guard.LoadAsync(configPath);

        File.WriteAllText(configPath, "{ corrupted");

        var result = await _guard.RecoverAsync(configPath);

        result.Success.Should().BeTrue();
        result.Configuration!.Name.Should().Be("test-list");
        result.Message.Should().Contain("Quarantined").And.Contain("restored");
        File.ReadAllText(configPath).Should().Be(ValidConfigJson);
    }

    [Fact]
    public async Task RecoverAsync_WithNoBackups_ReturnsFailure()
    {
        var configPath = WriteConfig("compiler-config.json", "{ corrupted");

        var result = await _guard.RecoverAsync(configPath);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("cannot regenerate");
    }

    [Fact]
    public async Task ListBackups_ForDifferentDirectoriesWithSameFileName_DoesNotCollide()
    {
        var directoryA = Path.Combine(_configDirectory, "a");
        var directoryB = Path.Combine(_configDirectory, "b");
        Directory.CreateDirectory(directoryA);
        Directory.CreateDirectory(directoryB);

        var pathA = Path.Combine(directoryA, "compiler-config.json");
        var pathB = Path.Combine(directoryB, "compiler-config.json");
        File.WriteAllText(pathA, ValidConfigJson);
        File.WriteAllText(pathB, ValidConfigJson);

        await _guard.LoadAsync(pathA);

        _guard.ListBackups(pathA).Should().HaveCount(1);
        _guard.ListBackups(pathB).Should().BeEmpty();
    }

    private string WriteConfig(string fileName, string content)
    {
        var path = Path.Combine(_configDirectory, fileName);
        File.WriteAllText(path, content);
        return path;
    }
}
