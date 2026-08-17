namespace Bloqr.Dashboard.Core.Services;

/// <summary>
/// Default implementation of <see cref="IDashboardService"/>, the Dashboard's embeddable-library
/// API boundary (#271). Wraps <see cref="IBloqrCompilerService"/>,
/// <see cref="IDashboardConfigurationStore"/>, and <see cref="IProfileManager"/> - all of which
/// are already free of any Spectre.Console dependency - behind one cohesive facade, so a future
/// .NET MAUI host (or any other embedder) has a single entry point instead of needing to know about
/// and wire up several separate services itself.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly IBloqrCompilerService _compilerService;
    private readonly IDashboardConfigurationStore _configStore;
    private readonly IProfileManager _profileManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardService"/> class.
    /// </summary>
    public DashboardService(
        IBloqrCompilerService compilerService,
        IDashboardConfigurationStore configStore,
        IProfileManager profileManager)
    {
        _compilerService = compilerService ?? throw new ArgumentNullException(nameof(compilerService));
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
    }

    /// <inheritdoc/>
    public Task<CompilerResult> CompileAsync(string compilerConfigPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compilerConfigPath);
        return _compilerService.RunAsync(compilerConfigPath, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ValidationResult> ValidateCompilerConfigAsync(
        string compilerConfigPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compilerConfigPath);
        return _compilerService.ValidateConfigurationAsync(compilerConfigPath, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListProfilesAsync(CancellationToken cancellationToken = default)
    {
        var load = await _configStore.LoadAsync(allowInteractiveRecovery: false, cancellationToken).ConfigureAwait(false);
        return load.Configuration.Profiles.Keys.ToList();
    }

    /// <inheritdoc/>
    public async Task<string?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
    {
        var load = await _configStore.LoadAsync(allowInteractiveRecovery: false, cancellationToken).ConfigureAwait(false);
        return load.Configuration.Settings.ActiveProfile;
    }

    /// <inheritdoc/>
    public async Task ActivateProfileAsync(string profileName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        var load = await _configStore.LoadAsync(allowInteractiveRecovery: false, cancellationToken).ConfigureAwait(false);
        _profileManager.SetActiveProfile(load.Configuration, profileName);
        await _configStore.SaveAsync(load.Configuration, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetActiveProfileCompilerConfigsAsync(
        CancellationToken cancellationToken = default)
    {
        var load = await _configStore.LoadAsync(allowInteractiveRecovery: false, cancellationToken).ConfigureAwait(false);
        var activeProfileName = load.Configuration.Settings.ActiveProfile;

        if (activeProfileName is null || !load.Configuration.Profiles.TryGetValue(activeProfileName, out var profile))
        {
            return [];
        }

        return profile.CompilerConfigs;
    }
}
