namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Manages named profiles within the Dashboard configuration: creation, activation, and
/// resolving the effective settings for the active (or a specified) profile.
/// </summary>
public interface IProfileManager
{
    /// <summary>
    /// Resolves the effective settings for a profile by overlaying its
    /// <see cref="DashboardProfile.SettingsOverrides"/> on top of the global
    /// <see cref="DashboardSettings"/>. Passing <c>null</c> resolves the global settings unchanged.
    /// </summary>
    /// <param name="configuration">The Dashboard configuration.</param>
    /// <param name="profileName">The profile name to resolve, or <c>null</c> for the global settings.</param>
    /// <returns>The effective, merged settings.</returns>
    DashboardSettings ResolveEffectiveSettings(DashboardConfiguration configuration, string? profileName);

    /// <summary>
    /// Creates a new named profile and adds it to the configuration.
    /// </summary>
    /// <param name="configuration">The Dashboard configuration to modify.</param>
    /// <param name="name">The new profile's name. Must not already exist.</param>
    /// <param name="profile">The profile definition.</param>
    void CreateProfile(DashboardConfiguration configuration, string name, DashboardProfile profile);

    /// <summary>
    /// Removes a named profile from the configuration. Clears <see cref="DashboardSettings.ActiveProfile"/>
    /// if it referenced the removed profile.
    /// </summary>
    /// <param name="configuration">The Dashboard configuration to modify.</param>
    /// <param name="name">The profile name to remove.</param>
    /// <returns><c>true</c> if a profile was removed; otherwise, <c>false</c>.</returns>
    bool RemoveProfile(DashboardConfiguration configuration, string name);

    /// <summary>
    /// Sets the active profile.
    /// </summary>
    /// <param name="configuration">The Dashboard configuration to modify.</param>
    /// <param name="name">The profile name to activate, or <c>null</c> to clear the active profile.</param>
    /// <exception cref="KeyNotFoundException">Thrown when <paramref name="name"/> does not match a known profile.</exception>
    void SetActiveProfile(DashboardConfiguration configuration, string? name);
}
