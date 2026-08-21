using Json.Schema;

namespace Bloqr.Dashboard.Core.Configuration;

/// <summary>
/// Owns reading, writing, validating, backing up, and recovering the Dashboard's own
/// <c>.jsonc</c> configuration file. Schema-validates against the embedded
/// <c>dashboard-config.schema.json</c> resource (kept in sync with <c>schemas/dashboard-config.schema.json</c>
/// at the repo root) so the binary is schema-validation-capable even when run standalone,
/// away from a checkout of this repo.
/// </summary>
public sealed class DashboardConfigurationStore : IDashboardConfigurationStore
{
    private const string BackupFilePrefix = "dashboard-config.backup-";
    private const string QuarantineFilePrefix = "dashboard-config.corrupt-";

    // JsonSchema.Net registers a parsed schema globally by its $id (to resolve $ref), and throws
    // if the same $id is registered twice. A lazily-initialized, process-wide static keeps that
    // registration to exactly once regardless of how many DashboardConfigurationStore instances
    // are created (multiple instances are routine in tests, and not disallowed for consumers).
    private static readonly Lazy<JsonSchema> LazySchema = new(LoadEmbeddedSchema);

    private readonly IDashboardPaths _paths;
    private readonly ILogger<DashboardConfigurationStore> _logger;
    private readonly JsonSchema _schema;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardConfigurationStore"/> class.
    /// </summary>
    /// <param name="paths">Resolves the Dashboard's well-known filesystem locations.</param>
    /// <param name="logger">Logger for recovery and validation diagnostics.</param>
    public DashboardConfigurationStore(IDashboardPaths paths, ILogger<DashboardConfigurationStore> logger)
    {
        _paths = paths;
        _logger = logger;
        _schema = LazySchema.Value;
    }

    /// <inheritdoc />
    public string ConfigPath => _paths.ConfigFilePath;

    /// <inheritdoc />
    public async Task<ConfigurationLoadResult> LoadAsync(
        bool allowInteractiveRecovery,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ConfigPath))
        {
            var defaults = new DashboardConfiguration();
            await SaveAsync(defaults, cancellationToken).ConfigureAwait(false);
            return new ConfigurationLoadResult(defaults, WasRecovered: false, RecoveryDescription: null);
        }

        var json = await File.ReadAllTextAsync(ConfigPath, cancellationToken).ConfigureAwait(false);

        DashboardConfiguration? configuration;
        try
        {
            configuration = JsonSerializer.Deserialize<DashboardConfiguration>(json, DashboardJsonOptions.Instance);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Dashboard config at {ConfigPath} failed to parse", ConfigPath);
            return await RecoverAsync(allowInteractiveRecovery, $"parse error: {ex.Message}", cancellationToken)
                .ConfigureAwait(false);
        }

        if (configuration is null)
        {
            return await RecoverAsync(allowInteractiveRecovery, "config file was empty or null", cancellationToken)
                .ConfigureAwait(false);
        }

        var validation = Validate(configuration);
        if (!validation.IsValid)
        {
            _logger.LogError(
                "Dashboard config at {ConfigPath} failed schema validation: {Errors}",
                ConfigPath,
                string.Join("; ", validation.Errors));
            return await RecoverAsync(
                allowInteractiveRecovery,
                $"schema validation failed: {string.Join("; ", validation.Errors)}",
                cancellationToken).ConfigureAwait(false);
        }

        return new ConfigurationLoadResult(configuration, WasRecovered: false, RecoveryDescription: null);
    }

    /// <inheritdoc />
    public ConfigurationValidationResult Validate(DashboardConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var instance = JsonSerializer.SerializeToElement(configuration, DashboardJsonOptions.Instance);
        var results = _schema.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.List });

        if (results.IsValid)
        {
            return ConfigurationValidationResult.Success;
        }

        var errors = (results.Details ?? [])
            .Where(d => !d.IsValid && d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
            .ToList();

        if (errors.Count == 0)
        {
            errors.Add("Configuration failed schema validation.");
        }

        return new ConfigurationValidationResult(false, errors);
    }

    /// <inheritdoc />
    public async Task SaveAsync(DashboardConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var validation = Validate(configuration);
        if (!validation.IsValid)
        {
            throw new DashboardConfigurationException(
                $"Refusing to save an invalid Dashboard configuration: {string.Join("; ", validation.Errors)}");
        }

        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (configuration.Settings.Backup.Enabled && File.Exists(ConfigPath))
        {
            Directory.CreateDirectory(_paths.BackupDirectory);
            var backupPath = Path.Combine(
                _paths.BackupDirectory,
                $"{BackupFilePrefix}{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}.jsonc");
            File.Copy(ConfigPath, backupPath, overwrite: true);
            PruneBackups(configuration.Settings.Backup.MaxBackups);
        }

        var jsonc = JsoncWriter.Write(configuration);
        await File.WriteAllTextAsync(ConfigPath, jsonc, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListBackups()
    {
        if (!Directory.Exists(_paths.BackupDirectory))
        {
            return [];
        }

        return Directory.GetFiles(_paths.BackupDirectory, $"{BackupFilePrefix}*.jsonc")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<DashboardConfiguration> RestoreFromBackupAsync(
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);

        var json = await File.ReadAllTextAsync(backupPath, cancellationToken).ConfigureAwait(false);
        var configuration = JsonSerializer.Deserialize<DashboardConfiguration>(json, DashboardJsonOptions.Instance)
            ?? throw new DashboardConfigurationException($"Backup at {backupPath} is empty or invalid.");

        var validation = Validate(configuration);
        if (!validation.IsValid)
        {
            throw new DashboardConfigurationException(
                $"Backup at {backupPath} failed schema validation: {string.Join("; ", validation.Errors)}");
        }

        await SaveAsync(configuration, cancellationToken).ConfigureAwait(false);
        return configuration;
    }

    private async Task<ConfigurationLoadResult> RecoverAsync(
        bool allowInteractiveRecovery,
        string reason,
        CancellationToken cancellationToken)
    {
        if (!allowInteractiveRecovery)
        {
            throw new DashboardConfigurationException(
                $"Dashboard configuration at {ConfigPath} is invalid ({reason}) and non-interactive mode does not " +
                "auto-recover. Run the Dashboard interactively to repair it, or delete the file to regenerate defaults.");
        }

        var quarantinePath = Quarantine();

        foreach (var backupPath in ListBackups())
        {
            DashboardConfiguration? candidate;
            try
            {
                var json = await File.ReadAllTextAsync(backupPath, cancellationToken).ConfigureAwait(false);
                candidate = JsonSerializer.Deserialize<DashboardConfiguration>(json, DashboardJsonOptions.Instance);
            }
            catch (JsonException)
            {
                continue;
            }

            if (candidate is null || !Validate(candidate).IsValid)
            {
                continue;
            }

            await SaveAsync(candidate, cancellationToken).ConfigureAwait(false);
            return new ConfigurationLoadResult(
                candidate,
                WasRecovered: true,
                $"Quarantined the corrupt config to {quarantinePath} and restored the most recent valid backup ({backupPath}).");
        }

        var defaults = new DashboardConfiguration();
        await SaveAsync(defaults, cancellationToken).ConfigureAwait(false);
        return new ConfigurationLoadResult(
            defaults,
            WasRecovered: true,
            $"Quarantined the corrupt config to {quarantinePath}; no valid backup was found, so defaults were regenerated.");
    }

    private string Quarantine()
    {
        var directory = Path.GetDirectoryName(ConfigPath) ?? ".";
        var quarantinePath = Path.Combine(
            directory,
            $"{QuarantineFilePrefix}{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}.jsonc");

        if (File.Exists(ConfigPath))
        {
            File.Move(ConfigPath, quarantinePath, overwrite: true);
        }

        return quarantinePath;
    }

    private void PruneBackups(int maxBackups)
    {
        var backups = ListBackups();
        if (backups.Count <= maxBackups)
        {
            return;
        }

        foreach (var stale in backups.Skip(maxBackups))
        {
            File.Delete(stale);
        }
    }

    private static JsonSchema LoadEmbeddedSchema()
    {
        var assembly = typeof(DashboardConfigurationStore).Assembly;
        using var stream = assembly.GetManifestResourceStream("dashboard-config.schema.json")
            ?? throw new InvalidOperationException(
                "Embedded resource 'dashboard-config.schema.json' was not found in Bloqr.Dashboard.Core.");
        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }
}
