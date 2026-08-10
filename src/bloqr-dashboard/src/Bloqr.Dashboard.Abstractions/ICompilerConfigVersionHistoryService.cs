namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Browses a compiler config file's version history via git, answering the epic's own open
/// question - "should it be done via git? If done via git, can version history be managed via
/// the Dashboard?" - rather than the Dashboard reimplementing version control itself. Read-only
/// history browsing plus restore-a-prior-version, all implemented by shelling out to a real
/// <c>git</c> binary.
/// </summary>
public interface ICompilerConfigVersionHistoryService
{
    /// <summary>
    /// Determines whether <paramref name="configPath"/> is tracked in a git repository.
    /// </summary>
    /// <param name="configPath">Path to the compiler-config file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if git is available and the file is inside a git work tree.</returns>
    Task<bool> IsUnderVersionControlAsync(string configPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the commit history for <paramref name="configPath"/>, newest first.
    /// </summary>
    /// <param name="configPath">Path to the compiler-config file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The revisions, newest first. Empty if the file has no history (e.g. never committed).</returns>
    Task<IReadOnlyList<CompilerConfigRevision>> GetHistoryAsync(
        string configPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the file's content as it was at a specific revision.
    /// </summary>
    /// <param name="configPath">Path to the compiler-config file.</param>
    /// <param name="revision">The commit hash to read from, as returned by <see cref="GetHistoryAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file's content at that revision.</returns>
    Task<string> GetContentAtRevisionAsync(
        string configPath,
        string revision,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a unified diff between a revision and the file's current working-tree content.
    /// </summary>
    /// <param name="configPath">Path to the compiler-config file.</param>
    /// <param name="revision">The commit hash to diff against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The unified diff text.</returns>
    Task<string> GetDiffAsync(string configPath, string revision, CancellationToken cancellationToken = default);

    /// <summary>
    /// Overwrites <paramref name="configPath"/> with its content at <paramref name="revision"/>.
    /// This writes file content only - it does not touch git's index, stage anything, or affect
    /// any other file, unlike a <c>git checkout</c>.
    /// </summary>
    /// <param name="configPath">Path to the compiler-config file.</param>
    /// <param name="revision">The commit hash to restore.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RestoreAsync(string configPath, string revision, CancellationToken cancellationToken = default);
}
