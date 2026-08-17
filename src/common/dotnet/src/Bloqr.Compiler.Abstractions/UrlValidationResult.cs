namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Result of validating a remote filter source URL (security checks, HTTPS enforcement, optional
/// in-flight hash verification) via <see cref="IBloqrValidatorService"/>. Mirrors
/// <c>bloqr-validator-core</c>'s <c>UrlValidationResult</c> JSON shape exactly.
/// </summary>
public sealed class UrlValidationResult
{
    /// <summary>
    /// Gets whether the URL passed validation.
    /// </summary>
    [JsonPropertyName("is_valid")]
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the validation messages (errors and warnings).
    /// </summary>
    [JsonPropertyName("messages")]
    public List<string> Messages { get; init; } = [];

    /// <summary>
    /// Gets the SHA-384 content hash, if the content was downloaded to compute it.
    /// </summary>
    [JsonPropertyName("content_hash")]
    public string? ContentHash { get; init; }

    /// <summary>
    /// Gets the content size in bytes, if known.
    /// </summary>
    [JsonPropertyName("content_size")]
    public long? ContentSize { get; init; }
}
