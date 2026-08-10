using System.Security.Cryptography;
using System.Text;

namespace Bloqr.Dashboard.Core.Configuration;

/// <summary>
/// Backs up and recovers compiler-config files a Dashboard profile references. Backups are keyed
/// by a hash of the config file's full path (since compiler configs can live anywhere on disk,
/// not just under the Dashboard's own directory tree) so two different directories' identically
/// named files never collide.
/// </summary>
public sealed class CompilerConfigGuard : ICompilerConfigGuard
{
    private const string BackupFilePrefix = "compiler-config.backup-";
    private const string QuarantineFilePrefix = "compiler-config.corrupt-";

    private readonly IConfigurationReader _configurationReader;
    private readonly IDashboardPaths _paths;
    private readonly ILogger<CompilerConfigGuard> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilerConfigGuard"/> class.
    /// </summary>
    /// <param name="configurationReader">Reads and parses compiler-config files (JSON/YAML/TOML).</param>
    /// <param name="paths">Resolves the Dashboard's backup directory root.</param>
    /// <param name="logger">Logger for recovery diagnostics.</param>
    public CompilerConfigGuard(
        IConfigurationReader configurationReader,
        IDashboardPaths paths,
        ILogger<CompilerConfigGuard> logger)
    {
        _configurationReader = configurationReader;
        _paths = paths;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CompilerConfigGuardResult> LoadAsync(
        string configPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);

        if (!File.Exists(configPath))
        {
            return new CompilerConfigGuardResult(false, null, $"Compiler config not found: {configPath}");
        }

        CompilerConfiguration configuration;
        try
        {
            // Broad catch by design: this method's contract is "always return a result describing
            // success or failure", never throw for malformed input - the three compiler-config
            // formats (JSON/YAML/TOML) surface parse failures as different exception types from
            // their respective libraries, and callers shouldn't need to know which.
            configuration = await _configurationReader
                .ReadConfigurationAsync(configPath, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Compiler config at {ConfigPath} failed to parse", configPath);
            return new CompilerConfigGuardResult(false, null, $"Failed to parse {configPath}: {ex.Message}");
        }

        var validation = ConfigurationValidator.Validate(configuration);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors.Select(e => $"{e.Field}: {e.Message}"));
            _logger.LogWarning("Compiler config at {ConfigPath} failed validation: {Errors}", configPath, errors);
            return new CompilerConfigGuardResult(false, null, $"Validation failed: {errors}");
        }

        await BackupAsync(configPath, cancellationToken).ConfigureAwait(false);
        return new CompilerConfigGuardResult(true, configuration, null);
    }

    /// <inheritdoc />
    public async Task<CompilerConfigGuardResult> RecoverAsync(
        string configPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);

        string? quarantinePath = null;
        if (File.Exists(configPath))
        {
            quarantinePath = Quarantine(configPath);
            _logger.LogInformation("Quarantined {ConfigPath} to {QuarantinePath}", configPath, quarantinePath);
        }

        foreach (var backupPath in ListBackups(configPath))
        {
            CompilerConfiguration candidate;
            try
            {
                candidate = await _configurationReader
                    .ReadConfigurationAsync(backupPath, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                continue;
            }

            if (!ConfigurationValidator.Validate(candidate).IsValid)
            {
                continue;
            }

            File.Copy(backupPath, configPath, overwrite: true);
            var description = quarantinePath is null
                ? $"Restored from backup: {backupPath}"
                : $"Quarantined the corrupt config to {quarantinePath} and restored from backup: {backupPath}";
            return new CompilerConfigGuardResult(true, candidate, description);
        }

        return new CompilerConfigGuardResult(
            false,
            null,
            $"No valid backup was found for {configPath}. The Dashboard cannot regenerate a compiler " +
            "config on its own - supply a new file or repair this one, then try again.");
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListBackups(string configPath)
    {
        var directory = GetBackupSubdirectory(configPath);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.GetFiles(directory, $"{BackupFilePrefix}*")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
    }

    private async Task BackupAsync(string configPath, CancellationToken cancellationToken)
    {
        var existingBackups = ListBackups(configPath);
        if (existingBackups.Count > 0 &&
            await FilesAreIdenticalAsync(configPath, existingBackups[0], cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var directory = GetBackupSubdirectory(configPath);
        Directory.CreateDirectory(directory);

        var extension = Path.GetExtension(configPath);
        var backupPath = Path.Combine(
            directory,
            $"{BackupFilePrefix}{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}{extension}");
        File.Copy(configPath, backupPath, overwrite: true);
    }

    private static async Task<bool> FilesAreIdenticalAsync(
        string pathA,
        string pathB,
        CancellationToken cancellationToken)
    {
        var bytesA = await File.ReadAllBytesAsync(pathA, cancellationToken).ConfigureAwait(false);
        var bytesB = await File.ReadAllBytesAsync(pathB, cancellationToken).ConfigureAwait(false);
        return bytesA.AsSpan().SequenceEqual(bytesB);
    }

    private string Quarantine(string configPath)
    {
        var directory = GetBackupSubdirectory(configPath);
        Directory.CreateDirectory(directory);

        var extension = Path.GetExtension(configPath);
        var quarantinePath = Path.Combine(
            directory,
            $"{QuarantineFilePrefix}{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}{extension}");
        File.Move(configPath, quarantinePath, overwrite: true);
        return quarantinePath;
    }

    private string GetBackupSubdirectory(string configPath)
    {
        var fullPath = Path.GetFullPath(configPath);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fullPath)))[..12].ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(fullPath);
        return Path.Combine(_paths.BackupDirectory, "compiler-configs", $"{baseName}-{hash}");
    }
}
