namespace Bloqr.Compiler.Core.Services;

/// <summary>
/// Default implementation of <see cref="IHashDatabaseService"/>. Stores the sidecar
/// as a flat JSON object mapping item identifier to <see cref="HashDatabaseEntry"/>.
/// </summary>
public class HashDatabaseService : IHashDatabaseService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly ILogger<HashDatabaseService> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="HashDatabaseService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public HashDatabaseService(ILogger<HashDatabaseService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, HashDatabaseEntry>> LoadAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        if (!File.Exists(databasePath))
        {
            return new Dictionary<string, HashDatabaseEntry>();
        }

        await using var stream = File.OpenRead(databasePath);
        var entries = await JsonSerializer.DeserializeAsync<Dictionary<string, HashDatabaseEntry>>(
            stream, SerializerOptions, cancellationToken);

        return entries ?? new Dictionary<string, HashDatabaseEntry>();
    }

    /// <inheritdoc/>
    public async Task RecordAsync(
        string databasePath,
        string itemIdentifier,
        HashDatabaseEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemIdentifier);
        ArgumentNullException.ThrowIfNull(entry);

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var entries = (await LoadAsync(databasePath, cancellationToken))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            entries[itemIdentifier] = entry;

            var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(databasePath);
            await JsonSerializer.SerializeAsync(stream, entries, SerializerOptions, cancellationToken);

            _logger.LogDebug(
                "Recorded hash for {ItemIdentifier} in {DatabasePath}",
                itemIdentifier, databasePath);
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
