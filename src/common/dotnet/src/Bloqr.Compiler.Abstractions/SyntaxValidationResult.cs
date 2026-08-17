namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Result of validating a local filter file's syntax (and, as a side effect inside
/// <c>bloqr-validator-core</c>, its at-rest hash) via <see cref="IRulesValidatorService"/>.
/// Mirrors <c>bloqr-validator-core</c>'s <c>SyntaxValidationResult</c> JSON shape exactly.
/// </summary>
public sealed class SyntaxValidationResult
{
    /// <summary>
    /// Gets whether the file's syntax is valid.
    /// </summary>
    [JsonPropertyName("is_valid")]
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the detected filter format (e.g. <c>"Adblock"</c>, <c>"Hosts"</c>, <c>"Unknown"</c>).
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of syntactically valid rules found.
    /// </summary>
    [JsonPropertyName("valid_rules")]
    public long ValidRules { get; init; }

    /// <summary>
    /// Gets the number of syntactically invalid rules found.
    /// </summary>
    [JsonPropertyName("invalid_rules")]
    public long InvalidRules { get; init; }

    /// <summary>
    /// Gets the validation messages (errors and warnings).
    /// </summary>
    [JsonPropertyName("messages")]
    public List<string> Messages { get; init; } = [];
}
