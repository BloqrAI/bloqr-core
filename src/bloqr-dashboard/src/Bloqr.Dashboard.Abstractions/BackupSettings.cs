namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Backup/restore settings for both the Dashboard's own configuration file and any compiler
/// configs it references. See <c>schemas/dashboard-config.schema.json</c>.
/// </summary>
public sealed class BackupSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether automatic backups are enabled. Defaults to <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of backup copies to retain per config file. Defaults to 10.
    /// </summary>
    public int MaxBackups { get; set; } = 10;

    /// <summary>
    /// Gets or sets the path to store backup copies, relative to the Dashboard config's own
    /// directory unless absolute. Null uses the default <c>backups/</c> subdirectory.
    /// </summary>
    public string? BackupDirectory { get; set; }
}
