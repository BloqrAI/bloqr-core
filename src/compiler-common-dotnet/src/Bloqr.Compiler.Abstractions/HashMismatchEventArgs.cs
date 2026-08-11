namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Event arguments for when a hash verification fails.
/// </summary>
public class HashMismatchEventArgs : CompilationEventArgs
{
    /// <summary>
    /// Gets the path or identifier for the item.
    /// </summary>
    public string ItemIdentifier { get; }

    /// <summary>
    /// Gets the type of item (e.g., "input_file", "output_file", "downloaded_source").
    /// </summary>
    public string ItemType { get; }

    /// <summary>
    /// Gets the expected hash.
    /// </summary>
    public string ExpectedHash { get; }

    /// <summary>
    /// Gets the actual hash.
    /// </summary>
    public string ActualHash { get; }

    /// <summary>
    /// Gets the size of the item in bytes.
    /// </summary>
    public long SizeBytes { get; }

    /// <summary>
    /// Gets or sets whether to abort compilation.
    /// </summary>
    public bool Abort { get; set; }

    /// <summary>
    /// Gets or sets the reason for aborting (if abort is true).
    /// </summary>
    public string? AbortReason { get; set; }

    /// <summary>
    /// Gets or sets whether the handler allowed continuation despite mismatch.
    /// </summary>
    public bool AllowContinuation { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HashMismatchEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    /// <param name="itemIdentifier">The path or identifier for the item.</param>
    /// <param name="itemType">The type of item.</param>
    /// <param name="expectedHash">The expected hash.</param>
    /// <param name="actualHash">The actual hash.</param>
    /// <param name="sizeBytes">The size in bytes.</param>
    public HashMismatchEventArgs(
        CompilerOptions options,
        string itemIdentifier,
        string itemType,
        string expectedHash,
        string actualHash,
        long sizeBytes)
        : base(options)
    {
        ItemIdentifier = itemIdentifier ?? throw new ArgumentNullException(nameof(itemIdentifier));
        ItemType = itemType ?? throw new ArgumentNullException(nameof(itemType));
        ExpectedHash = expectedHash ?? throw new ArgumentNullException(nameof(expectedHash));
        ActualHash = actualHash ?? throw new ArgumentNullException(nameof(actualHash));
        SizeBytes = sizeBytes;
        Abort = true;
        AbortReason = $"Hash mismatch for {itemIdentifier}: expected {expectedHash[..Math.Min(16, expectedHash.Length)]}..., got {actualHash[..Math.Min(16, actualHash.Length)]}...";
        AllowContinuation = false;
    }
}
