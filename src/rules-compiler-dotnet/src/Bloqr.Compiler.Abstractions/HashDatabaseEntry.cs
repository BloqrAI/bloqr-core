namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// A single recorded entry in a <c>.hashes.json</c> sidecar database: the last known-good
/// SHA-384 hash for a given item identifier (typically a file path).
/// </summary>
public class HashDatabaseEntry
{
    /// <summary>
    /// Gets or sets the SHA-384 hash (96 hex characters), lowercase.
    /// </summary>
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the size of the item in bytes at the time the hash was computed.
    /// </summary>
    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets when this entry was last recorded.
    /// </summary>
    [JsonPropertyName("computedAt")]
    public DateTimeOffset ComputedAt { get; set; }

    /// <summary>
    /// Gets or sets the type of item this hash describes (e.g., "output_file",
    /// "copied_rules_file"), for audit-trail readability.
    /// </summary>
    [JsonPropertyName("itemType")]
    public string ItemType { get; set; } = string.Empty;
}
