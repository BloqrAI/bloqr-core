namespace Bloqr.Dashboard.Tests;

public sealed class ProfileManagerTests
{
    private readonly ProfileManager _manager = new();

    [Fact]
    public void ResolveEffectiveSettings_WithNoProfileName_ReturnsGlobalSettings()
    {
        var configuration = new DashboardConfiguration
        {
            Settings = new DashboardSettings { Theme = DashboardTheme.Light },
        };

        var effective = _manager.ResolveEffectiveSettings(configuration, null);

        effective.Should().BeSameAs(configuration.Settings);
    }

    [Fact]
    public void ResolveEffectiveSettings_WithOverrides_MergesOverProfileFieldsOnly()
    {
        var configuration = new DashboardConfiguration
        {
            Settings = new DashboardSettings
            {
                Theme = DashboardTheme.Default,
                LogLevel = DashboardLogLevel.Error,
                DefaultRulesDirectory = "/global/rules",
            },
        };
        configuration.Profiles["fast"] = new DashboardProfile
        {
            CompilerConfigs = ["fast.json"],
            SettingsOverrides = new DashboardSettingsOverrides { LogLevel = DashboardLogLevel.Debug },
        };

        var effective = _manager.ResolveEffectiveSettings(configuration, "fast");

        effective.LogLevel.Should().Be(DashboardLogLevel.Debug, "the profile override should win");
        effective.Theme.Should().Be(DashboardTheme.Default, "unset override fields should fall back to the global setting");
        effective.DefaultRulesDirectory.Should().Be("/global/rules");
    }

    [Fact]
    public void ResolveEffectiveSettings_WithUnknownProfileName_ReturnsGlobalSettings()
    {
        var configuration = new DashboardConfiguration();

        var effective = _manager.ResolveEffectiveSettings(configuration, "does-not-exist");

        effective.Should().BeSameAs(configuration.Settings);
    }

    [Fact]
    public void CreateProfile_WithDuplicateName_Throws()
    {
        var configuration = new DashboardConfiguration();
        _manager.CreateProfile(configuration, "prod", new DashboardProfile { CompilerConfigs = ["a.json"] });

        var act = () => _manager.CreateProfile(configuration, "prod", new DashboardProfile { CompilerConfigs = ["b.json"] });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveProfile_WhenActive_ClearsActiveProfile()
    {
        var configuration = new DashboardConfiguration();
        _manager.CreateProfile(configuration, "prod", new DashboardProfile { CompilerConfigs = ["a.json"] });
        _manager.SetActiveProfile(configuration, "prod");

        var removed = _manager.RemoveProfile(configuration, "prod");

        removed.Should().BeTrue();
        configuration.Settings.ActiveProfile.Should().BeNull();
    }

    [Fact]
    public void RemoveProfile_WhenNotFound_ReturnsFalse()
    {
        var configuration = new DashboardConfiguration();

        _manager.RemoveProfile(configuration, "missing").Should().BeFalse();
    }

    [Fact]
    public void SetActiveProfile_WithUnknownName_Throws()
    {
        var configuration = new DashboardConfiguration();

        var act = () => _manager.SetActiveProfile(configuration, "missing");

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void SetActiveProfile_WithNull_ClearsActiveProfile()
    {
        var configuration = new DashboardConfiguration();
        _manager.CreateProfile(configuration, "prod", new DashboardProfile { CompilerConfigs = ["a.json"] });
        _manager.SetActiveProfile(configuration, "prod");

        _manager.SetActiveProfile(configuration, null);

        configuration.Settings.ActiveProfile.Should().BeNull();
    }
}
