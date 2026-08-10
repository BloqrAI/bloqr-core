namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Backs up and recovers compiler-config files a Dashboard profile references. Unlike
/// <see cref="IDashboardConfigurationStore"/>, the Dashboard doesn't own these files' shape or
/// content — they're authored by the user or another tool — so there is no "regenerate defaults"
/// fallback here, only backup-on-successful-load and restore-from-backup on corruption. If no
/// valid backup exists, recovery fails and the caller must supply or repair the file itself.
/// </summary>
public interface ICompilerConfigGuard
{
    /// <summary>
    /// Loads and validates the compiler config at <paramref name="configPath"/>. On success, the
    /// file is backed up (skipped if it's byte-identical to the most recent backup) so a later
    /// corruption can be recovered from via <see cref="RecoverAsync"/>.
    /// </summary>
    /// <param name="configPath">Path to the compiler-config file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The load result.</returns>
    Task<CompilerConfigGuardResult> LoadAsync(string configPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to recover a missing or corrupt compiler config at <paramref name="configPath"/>:
    /// quarantines whatever is currently there (if anything), then restores the newest backup
    /// that still parses and validates.
    /// </summary>
    /// <param name="configPath">Path to the compiler-config file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The recovery result; <see cref="CompilerConfigGuardResult.Success"/> is <c>false</c> if no valid backup exists.</returns>
    Task<CompilerConfigGuardResult> RecoverAsync(string configPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists available backups of the compiler config at <paramref name="configPath"/>, newest first.
    /// </summary>
    /// <param name="configPath">Path to the compiler-config file.</param>
    /// <returns>The backup file paths.</returns>
    IReadOnlyList<string> ListBackups(string configPath);
}
