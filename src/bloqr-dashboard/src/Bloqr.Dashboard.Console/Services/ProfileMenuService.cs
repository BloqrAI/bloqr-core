namespace Bloqr.Dashboard.Console.Services;

/// <summary>
/// Menu for creating, activating, and removing Dashboard profiles.
/// </summary>
public sealed class ProfileMenuService : MenuServiceBase
{
    private readonly IDashboardConfigurationStore _configStore;
    private readonly IProfileManager _profileManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileMenuService"/> class.
    /// </summary>
    public ProfileMenuService(
        IConsoleRenderer renderer,
        IConsolePrompter prompter,
        IDashboardConfigurationStore configStore,
        IProfileManager profileManager,
        ILogger<ProfileMenuService> logger)
        : base(renderer, prompter, logger)
    {
        _configStore = configStore;
        _profileManager = profileManager;
    }

    /// <inheritdoc />
    public override string Title => "Profile Management";

    /// <inheritdoc />
    protected override Dictionary<string, Func<Task>> GetMenuActions() => new()
    {
        ["List profiles"] = ListProfilesAsync,
        ["Create a profile"] = CreateProfileAsync,
        ["Activate a profile"] = ActivateProfileAsync,
        ["Clear active profile"] = ClearActiveProfileAsync,
        ["Remove a profile"] = RemoveProfileAsync,
    };

    private async Task ListProfilesAsync()
    {
        var load = await _configStore.LoadAsync(allowInteractiveRecovery: true).ConfigureAwait(false);

        if (load.Configuration.Profiles.Count == 0)
        {
            Renderer.WriteLine("No profiles defined yet.");
            return;
        }

        var table = new ConsoleTable { Title = "Profiles" };
        table.AddColumn("Name");
        table.AddColumn("Active", TextAlignment.Center);
        table.AddColumn("Compiler Configs");

        foreach (var (name, profile) in load.Configuration.Profiles)
        {
            var isActive = name == load.Configuration.Settings.ActiveProfile ? "*" : "";
            table.AddRow(name, isActive, string.Join(", ", profile.CompilerConfigs));
        }

        Renderer.RenderTable(table);
    }

    private async Task CreateProfileAsync()
    {
        var load = await _configStore.LoadAsync(allowInteractiveRecovery: true).ConfigureAwait(false);
        var configuration = load.Configuration;

        var name = Prompter.Prompt("Profile name");
        var description = Prompter.Prompt("Description (optional)", defaultValue: string.Empty);
        var configPath = Prompter.Prompt("Path to a compiler-config file for this profile");

        var profile = new DashboardProfile
        {
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
            CompilerConfigs = [configPath],
        };

        _profileManager.CreateProfile(configuration, name, profile);
        await _configStore.SaveAsync(configuration).ConfigureAwait(false);

        Renderer.WriteStyled($"Created profile '{name}'.", TextStyle.Success);
    }

    private async Task ActivateProfileAsync()
    {
        var load = await _configStore.LoadAsync(allowInteractiveRecovery: true).ConfigureAwait(false);
        var configuration = load.Configuration;

        if (configuration.Profiles.Count == 0)
        {
            Renderer.WriteStyled("No profiles to activate. Create one first.", TextStyle.Warning);
            return;
        }

        var name = Prompter.Select("Select a profile to activate", configuration.Profiles.Keys);
        _profileManager.SetActiveProfile(configuration, name);
        await _configStore.SaveAsync(configuration).ConfigureAwait(false);

        Renderer.WriteStyled($"Activated profile '{name}'.", TextStyle.Success);
    }

    private async Task ClearActiveProfileAsync()
    {
        var load = await _configStore.LoadAsync(allowInteractiveRecovery: true).ConfigureAwait(false);
        var configuration = load.Configuration;

        _profileManager.SetActiveProfile(configuration, null);
        await _configStore.SaveAsync(configuration).ConfigureAwait(false);

        Renderer.WriteStyled("Cleared the active profile.", TextStyle.Success);
    }

    private async Task RemoveProfileAsync()
    {
        var load = await _configStore.LoadAsync(allowInteractiveRecovery: true).ConfigureAwait(false);
        var configuration = load.Configuration;

        if (configuration.Profiles.Count == 0)
        {
            Renderer.WriteStyled("No profiles to remove.", TextStyle.Warning);
            return;
        }

        var name = Prompter.Select("Select a profile to remove", configuration.Profiles.Keys);
        if (!Prompter.Confirm($"Remove profile '{name}'?", false))
        {
            return;
        }

        _profileManager.RemoveProfile(configuration, name);
        await _configStore.SaveAsync(configuration).ConfigureAwait(false);

        Renderer.WriteStyled($"Removed profile '{name}'.", TextStyle.Success);
    }
}
