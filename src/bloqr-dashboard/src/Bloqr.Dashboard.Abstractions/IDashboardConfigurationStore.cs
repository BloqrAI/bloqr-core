namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Owns reading, writing, validating, backing up, and recovering the Dashboard's own
/// <c>.jsonc</c> configuration file. See <c>schemas/dashboard-config.schema.json</c>.
/// </summary>
public interface IDashboardConfigurationStore
{
    /// <summary>
    /// Gets the resolved path to the configuration file this store reads/writes.
    /// </summary>
    string ConfigPath { get; }

    /// <summary>
    /// Loads the configuration file, creating a heavily-commented default file if none exists
    /// and running corruption recovery if the existing file is malformed or schema-invalid.
    /// </summary>
    /// <param name="allowInteractiveRecovery">
    /// Whether recovery may prompt the user interactively. When <c>false</c> (non-interactive
    /// mode), recovery fails fast instead of prompting.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The load result, including whether recovery ran.</returns>
    Task<ConfigurationLoadResult> LoadAsync(
        bool allowInteractiveRecovery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a configuration document against <c>schemas/dashboard-config.schema.json</c>
    /// without writing anything.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <returns>The validation result.</returns>
    ConfigurationValidationResult Validate(DashboardConfiguration configuration);

    /// <summary>
    /// Saves the configuration, writing a heavily-commented <c>.jsonc</c> file and creating a
    /// backup of the previous version first (subject to <see cref="BackupSettings.Enabled"/>).
    /// </summary>
    /// <param name="configuration">The configuration to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(DashboardConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists available backups of the configuration file, newest first.
    /// </summary>
    /// <returns>The backup file paths.</returns>
    IReadOnlyList<string> ListBackups();

    /// <summary>
    /// Restores the configuration file from a specific backup.
    /// </summary>
    /// <param name="backupPath">The backup file path, as returned by <see cref="ListBackups"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The restored configuration.</returns>
    Task<DashboardConfiguration> RestoreFromBackupAsync(
        string backupPath,
        CancellationToken cancellationToken = default);
}
