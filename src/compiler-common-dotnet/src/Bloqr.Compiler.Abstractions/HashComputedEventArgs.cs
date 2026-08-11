namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Event arguments for when a hash is computed for an item.
/// </summary>
public class HashComputedEventArgs : CompilationEventArgs
{
    /// <summary>
    /// Gets the path or identifier for the item being hashed.
    /// </summary>
    public string ItemIdentifier { get; }

    /// <summary>
    /// Gets the type of item (e.g., "input_file", "output_file", "downloaded_source").
    /// </summary>
    public string ItemType { get; }

    /// <summary>
    /// Gets the computed SHA-384 hash (96 hex characters).
    /// </summary>
    public string Hash { get; }

    /// <summary>
    /// Gets the size of the item in bytes.
    /// </summary>
    public long SizeBytes { get; }

    /// <summary>
    /// Gets whether this is for verification purposes.
    /// </summary>
    public bool IsVerification { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HashComputedEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    /// <param name="itemIdentifier">The path or identifier for the item.</param>
    /// <param name="itemType">The type of item.</param>
    /// <param name="hash">The computed hash.</param>
    /// <param name="sizeBytes">The size in bytes.</param>
    /// <param name="isVerification">Whether this is for verification purposes.</param>
    public HashComputedEventArgs(
        CompilerOptions options,
        string itemIdentifier,
        string itemType,
        string hash,
        long sizeBytes,
        bool isVerification = false)
        : base(options)
    {
        ItemIdentifier = itemIdentifier ?? throw new ArgumentNullException(nameof(itemIdentifier));
        ItemType = itemType ?? throw new ArgumentNullException(nameof(itemType));
        Hash = hash ?? throw new ArgumentNullException(nameof(hash));
        SizeBytes = sizeBytes;
        IsVerification = isVerification;
    }
}
