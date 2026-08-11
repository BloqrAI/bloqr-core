namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Event arguments for when a hash is verified successfully.
/// </summary>
public class HashVerifiedEventArgs : CompilationEventArgs
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
    /// Gets the duration of hash computation.
    /// </summary>
    public TimeSpan ComputationDuration { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HashVerifiedEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    /// <param name="itemIdentifier">The path or identifier for the item.</param>
    /// <param name="itemType">The type of item.</param>
    /// <param name="expectedHash">The expected hash.</param>
    /// <param name="actualHash">The actual hash.</param>
    /// <param name="sizeBytes">The size in bytes.</param>
    /// <param name="computationDuration">The duration of hash computation.</param>
    public HashVerifiedEventArgs(
        CompilerOptions options,
        string itemIdentifier,
        string itemType,
        string expectedHash,
        string actualHash,
        long sizeBytes,
        TimeSpan computationDuration)
        : base(options)
    {
        ItemIdentifier = itemIdentifier ?? throw new ArgumentNullException(nameof(itemIdentifier));
        ItemType = itemType ?? throw new ArgumentNullException(nameof(itemType));
        ExpectedHash = expectedHash ?? throw new ArgumentNullException(nameof(expectedHash));
        ActualHash = actualHash ?? throw new ArgumentNullException(nameof(actualHash));
        SizeBytes = sizeBytes;
        ComputationDuration = computationDuration;
    }
}
