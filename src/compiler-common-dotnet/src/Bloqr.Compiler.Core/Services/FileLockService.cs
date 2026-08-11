using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Bloqr.Compiler.Core.Services;

/// <summary>
/// Implementation of a file lock handle.
/// </summary>
internal sealed class FileLockHandle : IFileLockHandle
{
    private readonly FileStream _fileStream;
    private readonly FileLockService _service;
    private readonly Stopwatch _stopwatch;
    private bool _disposed;

    public Guid LockId { get; }
    public string FilePath { get; }
    public FileLockType LockType { get; }
    public DateTimeOffset AcquiredAt { get; }
    public bool IsActive => !_disposed;
    public string? ContentHash { get; }

    internal FileLockHandle(
        FileStream fileStream,
        string filePath,
        FileLockType lockType,
        string? contentHash,
        FileLockService service)
    {
        _fileStream = fileStream ?? throw new ArgumentNullException(nameof(fileStream));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _stopwatch = Stopwatch.StartNew();

        LockId = Guid.NewGuid();
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        LockType = lockType;
        ContentHash = contentHash;
        AcquiredAt = DateTimeOffset.UtcNow;
    }

    internal TimeSpan GetDuration() => _stopwatch.Elapsed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stopwatch.Stop();
        _fileStream.Dispose();
        _service.OnLockReleased(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _stopwatch.Stop();
        await _fileStream.DisposeAsync();
        _service.OnLockReleased(this);
    }
}

/// <summary>
/// Default implementation of file locking service for local source files.
/// Provides zero-trust file integrity verification.
/// </summary>
public class FileLockService : IFileLockService
{
    private readonly ILogger<FileLockService> _logger;
    private readonly ConcurrentDictionary<Guid, FileLockHandle> _activeLocks = new();
    private readonly SemaphoreSlim _lockSemaphore = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLockService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public FileLockService(ILogger<FileLockService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<IFileLockHandle> ActiveLocks =>
        _activeLocks.Values.ToList().AsReadOnly();

    /// <inheritdoc/>
    public async Task<IFileLockHandle> AcquireReadLockAsync(
        string filePath,
        bool computeHash = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        _logger.LogDebug("Acquiring read lock on {FilePath}", fullPath);

        await _lockSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Open file with read access, shared read but no write
            var fileStream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read, // Allow other readers, block writers
                bufferSize: 4096,
                FileOptions.Asynchronous);

            string? hash = null;
            if (computeHash)
            {
                hash = await ComputeHashFromStreamAsync(fileStream, cancellationToken);
                fileStream.Seek(0, SeekOrigin.Begin);
            }

            var handle = new FileLockHandle(fileStream, fullPath, FileLockType.Read, hash, this);
            _activeLocks.TryAdd(handle.LockId, handle);

            _logger.LogInformation(
                "Read lock acquired on {FilePath} (LockId: {LockId}, Hash: {Hash})",
                fullPath, handle.LockId, hash?[..16] + "...");

            return handle;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to acquire read lock on {FilePath}", fullPath);
            throw;
        }
        finally
        {
            _lockSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IFileLockHandle> AcquireWriteLockAsync(
        string filePath,
        bool computeHash = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        _logger.LogDebug("Acquiring write lock on {FilePath}", fullPath);

        await _lockSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Compute hash first if needed (before exclusive access)
            string? hash = null;
            if (computeHash && File.Exists(fullPath))
            {
                hash = await ComputeHashAsync(fullPath, cancellationToken);
            }

            // Open file with write access, exclusive (no sharing)
            var fileStream = new FileStream(
                fullPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None, // Exclusive access
                bufferSize: 4096,
                FileOptions.Asynchronous);

            var handle = new FileLockHandle(fileStream, fullPath, FileLockType.Write, hash, this);
            _activeLocks.TryAdd(handle.LockId, handle);

            _logger.LogInformation(
                "Write lock acquired on {FilePath} (LockId: {LockId})",
                fullPath, handle.LockId);

            return handle;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to acquire write lock on {FilePath}", fullPath);
            throw;
        }
        finally
        {
            _lockSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IFileLockHandle?> TryAcquireReadLockAsync(
        string filePath,
        TimeSpan timeout,
        bool computeHash = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await AcquireReadLockAsync(fullPath, computeHash, cancellationToken);
            }
            catch (IOException) when (stopwatch.Elapsed < timeout)
            {
                // File is locked, wait and retry
                _logger.LogDebug("File {FilePath} is locked, retrying...", fullPath);
                await Task.Delay(100, cancellationToken);
            }
        }

        _logger.LogWarning("Timeout acquiring read lock on {FilePath}", fullPath);
        return null;
    }

    /// <inheritdoc/>
    public async Task<bool> VerifyIntegrityAsync(
        string filePath,
        string expectedHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHash);

        var currentHash = await ComputeHashAsync(filePath, cancellationToken);
        var matches = string.Equals(currentHash, expectedHash, StringComparison.OrdinalIgnoreCase);

        if (!matches)
        {
            _logger.LogWarning(
                "Integrity check failed for {FilePath}: expected {Expected}, got {Actual}",
                filePath, expectedHash[..16] + "...", currentHash[..16] + "...");
        }

        return matches;
    }

    /// <inheritdoc/>
    public async Task<string> ComputeHashAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return await ComputeHashFromStreamAsync(stream, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ReleaseAllLocksAsync()
    {
        _logger.LogInformation("Releasing all {Count} active locks", _activeLocks.Count);

        var locks = _activeLocks.Values.ToList();
        foreach (var lockHandle in locks)
        {
            try
            {
                await lockHandle.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing lock {LockId}", lockHandle.LockId);
            }
        }

        _activeLocks.Clear();
    }

    internal void OnLockReleased(FileLockHandle handle)
    {
        _activeLocks.TryRemove(handle.LockId, out _);
        _logger.LogDebug(
            "Lock released on {FilePath} (LockId: {LockId}, Duration: {Duration}ms)",
            handle.FilePath, handle.LockId, handle.GetDuration().TotalMilliseconds);
    }

    private static async Task<string> ComputeHashFromStreamAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
