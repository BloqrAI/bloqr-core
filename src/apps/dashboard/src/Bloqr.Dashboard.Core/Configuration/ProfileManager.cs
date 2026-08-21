namespace Bloqr.Dashboard.Core.Configuration;

/// <summary>
/// Manages named profiles within the Dashboard configuration.
/// </summary>
public sealed class ProfileManager : IProfileManager
{
    /// <inheritdoc />
    public DashboardSettings ResolveEffectiveSettings(DashboardConfiguration configuration, string? profileName)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrEmpty(profileName))
        {
            return configuration.Settings;
        }

        if (!configuration.Profiles.TryGetValue(profileName, out var profile) ||
            profile.SettingsOverrides is not { } overrides)
        {
            return configuration.Settings;
        }

        var settings = configuration.Settings;
        return new DashboardSettings
        {
            Theme = overrides.Theme ?? settings.Theme,
            LogLevel = overrides.LogLevel ?? settings.LogLevel,
            DefaultRulesDirectory = overrides.DefaultRulesDirectory ?? settings.DefaultRulesDirectory,
            Backup = overrides.Backup ?? settings.Backup,
            ActiveProfile = settings.ActiveProfile,
        };
    }

    /// <inheritdoc />
    public void CreateProfile(DashboardConfiguration configuration, string name, DashboardProfile profile)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(profile);

        if (configuration.Profiles.ContainsKey(name))
        {
            throw new InvalidOperationException($"A profile named '{name}' already exists.");
        }

        configuration.Profiles[name] = profile;
    }

    /// <inheritdoc />
    public bool RemoveProfile(DashboardConfiguration configuration, string name)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!configuration.Profiles.Remove(name))
        {
            return false;
        }

        if (configuration.Settings.ActiveProfile == name)
        {
            configuration.Settings.ActiveProfile = null;
        }

        return true;
    }

    /// <inheritdoc />
    public void SetActiveProfile(DashboardConfiguration configuration, string? name)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (name is not null && !configuration.Profiles.ContainsKey(name))
        {
            throw new KeyNotFoundException($"No profile named '{name}' exists.");
        }

        configuration.Settings.ActiveProfile = name;
    }
}
