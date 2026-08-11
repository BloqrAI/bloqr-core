namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Event arguments for when a file lock is acquired.
/// </summary>
public class FileLockAcquiredEventArgs : CompilationEventArgs
{
    /// <summary>
    /// Gets the path to the locked file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the lock type (read or write).
    /// </summary>
    public FileLockType LockType { get; }

    /// <summary>
    /// Gets the lock identifier for tracking.
    /// </summary>
    public Guid LockId { get; }

    /// <summary>
    /// Gets the timestamp when the lock was acquired.
    /// </summary>
    public DateTimeOffset AcquiredAt { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLockAcquiredEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    /// <param name="filePath">The path to the locked file.</param>
    /// <param name="lockType">The type of lock.</param>
    /// <param name="lockId">The lock identifier.</param>
    public FileLockAcquiredEventArgs(
        CompilerOptions options,
        string filePath,
        FileLockType lockType,
        Guid lockId)
        : base(options)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        LockType = lockType;
        LockId = lockId;
        AcquiredAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Event arguments for when a file lock is released.
/// </summary>
public class FileLockReleasedEventArgs : CompilationEventArgs
{
    /// <summary>
    /// Gets the path to the unlocked file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the lock identifier.
    /// </summary>
    public Guid LockId { get; }

    /// <summary>
    /// Gets the duration the lock was held.
    /// </summary>
    public TimeSpan LockDuration { get; }

    /// <summary>
    /// Gets whether the file was modified while locked (for write locks).
    /// </summary>
    public bool WasModified { get; }

    /// <summary>
    /// Gets the content hash before the lock (for integrity verification).
    /// </summary>
    public string? HashBefore { get; }

    /// <summary>
    /// Gets the content hash after the lock was released.
    /// </summary>
    public string? HashAfter { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLockReleasedEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    /// <param name="filePath">The path to the unlocked file.</param>
    /// <param name="lockId">The lock identifier.</param>
    /// <param name="lockDuration">How long the lock was held.</param>
    /// <param name="wasModified">Whether the file was modified.</param>
    /// <param name="hashBefore">Content hash before lock.</param>
    /// <param name="hashAfter">Content hash after release.</param>
    public FileLockReleasedEventArgs(
        CompilerOptions options,
        string filePath,
        Guid lockId,
        TimeSpan lockDuration,
        bool wasModified = false,
        string? hashBefore = null,
        string? hashAfter = null)
        : base(options)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        LockId = lockId;
        LockDuration = lockDuration;
        WasModified = wasModified;
        HashBefore = hashBefore;
        HashAfter = hashAfter;
    }
}

/// <summary>
/// Event arguments for when a file lock fails.
/// </summary>
public class FileLockFailedEventArgs : CompilationEventArgs
{
    /// <summary>
    /// Gets the path to the file that couldn't be locked.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the lock type that was attempted.
    /// </summary>
    public FileLockType LockType { get; }

    /// <summary>
    /// Gets the reason for failure.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Gets or sets a value indicating whether to continue without the lock.
    /// </summary>
    public bool ContinueWithoutLock { get; set; }

    /// <summary>
    /// Gets the exception that caused the failure, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLockFailedEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="lockType">The type of lock attempted.</param>
    /// <param name="reason">The reason for failure.</param>
    /// <param name="exception">The exception, if any.</param>
    public FileLockFailedEventArgs(
        CompilerOptions options,
        string filePath,
        FileLockType lockType,
        string reason,
        Exception? exception = null)
        : base(options)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        LockType = lockType;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        Exception = exception;
    }
}

/// <summary>
/// Specifies the type of file lock.
/// </summary>
public enum FileLockType
{
    /// <summary>
    /// Read-only lock (shared access, prevents writes).
    /// </summary>
    Read,

    /// <summary>
    /// Write lock (exclusive access).
    /// </summary>
    Write
}
