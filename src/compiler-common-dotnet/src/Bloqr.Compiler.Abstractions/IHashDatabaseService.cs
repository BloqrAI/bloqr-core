namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Reads and writes the <c>.hashes.json</c> sidecar database referenced by a compiler
/// configuration's <c>hashVerification.hashDatabasePath</c> - the primary trust mechanism
/// for detecting tampering between compilation runs, as opposed to a self-referential
/// hash embedded in the output file itself.
/// </summary>
public interface IHashDatabaseService
{
    /// <summary>
    /// Loads all recorded entries from the database at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the <c>.hashes.json</c> file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The recorded entries, keyed by item identifier. Empty if the file does not exist yet.</returns>
    Task<IReadOnlyDictionary<string, HashDatabaseEntry>> LoadAsync(
        string databasePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records (or overwrites) a single entry and persists the database.
    /// </summary>
    /// <param name="databasePath">Path to the <c>.hashes.json</c> file.</param>
    /// <param name="itemIdentifier">The key to record the entry under.</param>
    /// <param name="entry">The entry to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordAsync(
        string databasePath,
        string itemIdentifier,
        HashDatabaseEntry entry,
        CancellationToken cancellationToken = default);
}
