namespace Bloqr.Dashboard.Tests;

public sealed class JsoncWriterTests
{
    [Fact]
    public void Write_ProducesJsonc_ThatRoundTripsThroughTheJsoncTolerantReader()
    {
        var configuration = new DashboardConfiguration
        {
            Version = "2.1.0",
            Settings = new DashboardSettings
            {
                Theme = DashboardTheme.HighContrast,
                LogLevel = DashboardLogLevel.Warn,
                DefaultRulesDirectory = "output/rules",
                ActiveProfile = "default",
                Backup = new BackupSettings { Enabled = true, MaxBackups = 5, BackupDirectory = "my-backups" },
            },
        };
        configuration.Profiles["default"] = new DashboardProfile
        {
            Description = "The default profile",
            CompilerConfigs = ["a.json", "b.json"],
            SettingsOverrides = new DashboardSettingsOverrides
            {
                Theme = DashboardTheme.Dark,
                Backup = new BackupSettings { Enabled = false, MaxBackups = 1 },
            },
        };

        var jsonc = JsoncWriter.Write(configuration);
        var reparsed = JsonSerializer.Deserialize<DashboardConfiguration>(jsonc, DashboardJsonOptions.Instance);

        reparsed.Should().NotBeNull();
        reparsed!.Version.Should().Be("2.1.0");
        reparsed.Settings.Theme.Should().Be(DashboardTheme.HighContrast);
        reparsed.Settings.LogLevel.Should().Be(DashboardLogLevel.Warn);
        reparsed.Settings.DefaultRulesDirectory.Should().Be("output/rules");
        reparsed.Settings.ActiveProfile.Should().Be("default");
        reparsed.Settings.Backup.MaxBackups.Should().Be(5);
        reparsed.Settings.Backup.BackupDirectory.Should().Be("my-backups");

        reparsed.Profiles.Should().ContainKey("default");
        var profile = reparsed.Profiles["default"];
        profile.Description.Should().Be("The default profile");
        profile.CompilerConfigs.Should().Equal("a.json", "b.json");
        profile.SettingsOverrides.Should().NotBeNull();
        profile.SettingsOverrides!.Theme.Should().Be(DashboardTheme.Dark);
        profile.SettingsOverrides.Backup!.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Write_IncludesExplanatoryComments()
    {
        var jsonc = JsoncWriter.Write(new DashboardConfiguration());

        jsonc.Should().Contain("// Bloqr Dashboard configuration file.");
        jsonc.Should().Contain("// Dashboard config schema version");
        jsonc.Should().Contain("// Console UI color theme");
    }

    [Fact]
    public void Write_WithNoProfiles_EmitsEmptyProfilesObject()
    {
        var jsonc = JsoncWriter.Write(new DashboardConfiguration());

        var reparsed = JsonSerializer.Deserialize<DashboardConfiguration>(jsonc, DashboardJsonOptions.Instance);

        reparsed!.Profiles.Should().BeEmpty();
    }

    [Fact]
    public void Write_WithNullActiveProfile_EmitsJsonNull()
    {
        var jsonc = JsoncWriter.Write(new DashboardConfiguration());

        jsonc.Should().Contain("\"activeProfile\": null");
    }
}
