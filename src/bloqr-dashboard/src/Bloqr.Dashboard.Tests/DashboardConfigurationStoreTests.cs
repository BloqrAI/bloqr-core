namespace Bloqr.Dashboard.Tests;

public sealed class DashboardConfigurationStoreTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly DashboardPaths _paths;
    private readonly DashboardConfigurationStore _store;

    public DashboardConfigurationStoreTests()
    {
        _rootDirectory = Directory.CreateTempSubdirectory("bloqr-dashboard-tests-").FullName;
        _paths = DashboardPaths.ForDirectory(_rootDirectory);
        _store = new DashboardConfigurationStore(_paths, NullLogger<DashboardConfigurationStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenNoFileExists_CreatesDefaultsAndWritesJsonc()
    {
        File.Exists(_paths.ConfigFilePath).Should().BeFalse();

        var result = await _store.LoadAsync(allowInteractiveRecovery: true);

        result.WasRecovered.Should().BeFalse();
        result.Configuration.Version.Should().Be("1.0.0");
        File.Exists(_paths.ConfigFilePath).Should().BeTrue();

        var text = await File.ReadAllTextAsync(_paths.ConfigFilePath);
        text.Should().Contain("//", "the generated file must be heavily commented per the epic's requirement");
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsSettingsAndProfiles()
    {
        var configuration = new DashboardConfiguration
        {
            Version = "1.0.0",
            Settings = new DashboardSettings
            {
                Theme = DashboardTheme.HighContrast,
                LogLevel = DashboardLogLevel.Debug,
                DefaultRulesDirectory = "/tmp/rules",
                ActiveProfile = "prod",
            },
        };
        configuration.Profiles["prod"] = new DashboardProfile
        {
            Description = "Production filter list",
            CompilerConfigs = ["configs/prod.json"],
            SettingsOverrides = new DashboardSettingsOverrides { LogLevel = DashboardLogLevel.Warn },
        };

        await _store.SaveAsync(configuration);
        var reloaded = await _store.LoadAsync(allowInteractiveRecovery: true);

        reloaded.WasRecovered.Should().BeFalse();
        reloaded.Configuration.Settings.Theme.Should().Be(DashboardTheme.HighContrast);
        reloaded.Configuration.Settings.LogLevel.Should().Be(DashboardLogLevel.Debug);
        reloaded.Configuration.Settings.DefaultRulesDirectory.Should().Be("/tmp/rules");
        reloaded.Configuration.Settings.ActiveProfile.Should().Be("prod");
        reloaded.Configuration.Profiles.Should().ContainKey("prod");
        reloaded.Configuration.Profiles["prod"].CompilerConfigs.Should().Equal("configs/prod.json");
        reloaded.Configuration.Profiles["prod"].SettingsOverrides!.LogLevel.Should().Be(DashboardLogLevel.Warn);
    }

    [Fact]
    public async Task Validate_WhenVersionIsNotSemver_ReturnsErrors()
    {
        var configuration = new DashboardConfiguration { Version = "not-a-version" };

        var result = _store.Validate(configuration);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WhenFileIsCorrupt_NonInteractive_Throws()
    {
        await File.WriteAllTextAsync(_paths.ConfigFilePath, "{ this is not valid json");

        var act = async () => await _store.LoadAsync(allowInteractiveRecovery: false);

        await act.Should().ThrowAsync<DashboardConfigurationException>();
    }

    [Fact]
    public async Task LoadAsync_WhenFileIsCorrupt_Interactive_WithNoBackup_QuarantinesAndRegeneratesDefaults()
    {
        await File.WriteAllTextAsync(_paths.ConfigFilePath, "{ this is not valid json");

        var result = await _store.LoadAsync(allowInteractiveRecovery: true);

        result.WasRecovered.Should().BeTrue();
        result.RecoveryDescription.Should().Contain("regenerated");
        result.Configuration.Version.Should().Be("1.0.0");

        Directory.GetFiles(_rootDirectory, "dashboard-config.corrupt-*.jsonc").Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadAsync_WhenFileIsCorrupt_Interactive_WithValidBackup_RestoresFromBackup()
    {
        var goodConfig = new DashboardConfiguration
        {
            Settings = new DashboardSettings { Theme = DashboardTheme.Dark },
        };

        // The first save creates the file (nothing existed to back up yet); the second save of
        // the same content backs up that first version, giving us a valid backup to restore from.
        await _store.SaveAsync(goodConfig);
        await _store.SaveAsync(goodConfig);
        _store.ListBackups().Should().HaveCount(1);

        // Corrupt the on-disk file directly (bypassing SaveAsync, which would itself back it up).
        await File.WriteAllTextAsync(_paths.ConfigFilePath, "{ not json at all");

        var result = await _store.LoadAsync(allowInteractiveRecovery: true);

        result.WasRecovered.Should().BeTrue();
        result.RecoveryDescription.Should().Contain("restored the most recent valid backup");
        result.Configuration.Settings.Theme.Should().Be(DashboardTheme.Dark);
    }

    [Fact]
    public async Task SaveAsync_PrunesBackupsBeyondMaxBackups()
    {
        var configuration = new DashboardConfiguration
        {
            Settings = new DashboardSettings { Backup = new BackupSettings { Enabled = true, MaxBackups = 2 } },
        };

        // First save has nothing to back up yet.
        await _store.SaveAsync(configuration);

        for (var i = 0; i < 5; i++)
        {
            configuration.Settings.DefaultRulesDirectory = $"/tmp/run-{i}";
            await _store.SaveAsync(configuration);
            await Task.Delay(5); // ensure distinct backup filename timestamps
        }

        _store.ListBackups().Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveAsync_WhenBackupDisabled_WritesNoBackups()
    {
        var configuration = new DashboardConfiguration
        {
            Settings = new DashboardSettings { Backup = new BackupSettings { Enabled = false } },
        };

        await _store.SaveAsync(configuration);
        await _store.SaveAsync(configuration);

        _store.ListBackups().Should().BeEmpty();
    }
}
