namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Interface for writing compiled output to destination directories.
/// </summary>
public interface IOutputWriter
{
    /// <summary>
    /// Copies the compiled output to a destination directory.
    /// </summary>
    /// <param name="sourcePath">Path to the source file.</param>
    /// <param name="destinationPath">Path to the destination file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> CopyOutputAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the SHA-384 hash of a file.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Hex-encoded hash string.</returns>
    Task<string> ComputeHashAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the number of non-empty, non-comment lines in a file.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of rules.</returns>
    Task<int> CountRulesAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
