namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Represents a file lock handle that can be disposed to release the lock.
/// </summary>
public interface IFileLockHandle : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the lock identifier.
    /// </summary>
    Guid LockId { get; }

    /// <summary>
    /// Gets the path to the locked file.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Gets the lock type.
    /// </summary>
    FileLockType LockType { get; }

    /// <summary>
    /// Gets the timestamp when the lock was acquired.
    /// </summary>
    DateTimeOffset AcquiredAt { get; }

    /// <summary>
    /// Gets whether the lock is still active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets the content hash computed when the lock was acquired.
    /// </summary>
    string? ContentHash { get; }
}

/// <summary>
/// Service for managing file locks on local source files.
/// Implements zero-trust file integrity verification.
/// </summary>
public interface IFileLockService
{
    /// <summary>
    /// Acquires a read lock on the specified file, preventing other processes from writing to it.
    /// </summary>
    /// <param name="filePath">The path to the file to lock.</param>
    /// <param name="computeHash">Whether to compute a content hash for integrity verification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A lock handle that releases the lock when disposed.</returns>
    /// <exception cref="IOException">Thrown when the file cannot be locked.</exception>
    Task<IFileLockHandle> AcquireReadLockAsync(
        string filePath,
        bool computeHash = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a write lock on the specified file, giving exclusive access.
    /// </summary>
    /// <param name="filePath">The path to the file to lock.</param>
    /// <param name="computeHash">Whether to compute a content hash for integrity verification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A lock handle that releases the lock when disposed.</returns>
    /// <exception cref="IOException">Thrown when the file cannot be locked.</exception>
    Task<IFileLockHandle> AcquireWriteLockAsync(
        string filePath,
        bool computeHash = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to acquire a read lock without throwing on failure.
    /// </summary>
    /// <param name="filePath">The path to the file to lock.</param>
    /// <param name="timeout">Maximum time to wait for the lock.</param>
    /// <param name="computeHash">Whether to compute a content hash.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A lock handle if successful, null if the lock couldn't be acquired.</returns>
    Task<IFileLockHandle?> TryAcquireReadLockAsync(
        string filePath,
        TimeSpan timeout,
        bool computeHash = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the integrity of a file by comparing its current hash with a previous hash.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="expectedHash">The expected content hash.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the file matches the expected hash, false otherwise.</returns>
    Task<bool> VerifyIntegrityAsync(
        string filePath,
        string expectedHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the SHA-256 hash of a file's contents.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The SHA-256 hash as a hex string.</returns>
    Task<string> ComputeHashAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the currently held locks.
    /// </summary>
    IReadOnlyCollection<IFileLockHandle> ActiveLocks { get; }

    /// <summary>
    /// Releases all currently held locks.
    /// </summary>
    Task ReleaseAllLocksAsync();
}
