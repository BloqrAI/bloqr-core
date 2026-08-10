namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// The Dashboard's embeddable-library API boundary: every operation the interactive menus and
/// the CLI surface expose, available as plain async calls with no dependency on Spectre.Console
/// or terminal rendering. Per the epic: "the WPF application will have the Dashboard console
/// application embedded as a library... Dashboard should expose all inner operations via CLI
/// switches, APIs, etc." (#271). A future WPF host depends on this interface (and the rest of
/// <c>Bloqr.Dashboard.Abstractions</c>/<c>Bloqr.Dashboard.Core</c>) instead of anything in
/// <c>Bloqr.Dashboard.Console</c>.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Compiles filter rules from the given compiler config, identical to what
    /// <c>CompileMenuService</c>'s interactive "Compile using a specific config file" action does
    /// minus the live progress display.
    /// </summary>
    /// <param name="compilerConfigPath">Path to the compiler config file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CompilerResult> CompileAsync(string compilerConfigPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a compiler config file without compiling it.
    /// </summary>
    /// <param name="compilerConfigPath">Path to the compiler config file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ValidationResult> ValidateCompilerConfigAsync(string compilerConfigPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the names of every profile defined in the Dashboard's own configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<string>> ListProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the currently active profile's name, or <c>null</c> if none is active.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string?> GetActiveProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a named profile, persisting the change to the Dashboard's own configuration.
    /// </summary>
    /// <param name="profileName">The profile name to activate. Must already exist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when <paramref name="profileName"/> does not match a known profile.</exception>
    Task ActivateProfileAsync(string profileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the compiler config path(s) registered to the active profile, so a caller can
    /// compile "whatever the active profile points at" without separately loading the Dashboard
    /// configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active profile's compiler config paths, or an empty list if no profile is active.</returns>
    Task<IReadOnlyList<string>> GetActiveProfileCompilerConfigsAsync(CancellationToken cancellationToken = default);
}
