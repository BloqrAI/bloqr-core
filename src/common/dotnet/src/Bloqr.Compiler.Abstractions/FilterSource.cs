namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Represents a source filter list to compile.
/// </summary>
/// <remarks>
/// Each source can be a URL or local file path pointing to a filter list
/// in either adblock or hosts format.
/// </remarks>
public class FilterSource
{
    /// <summary>
    /// Gets or sets the name of the source (for identification purposes).
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the source URL or file path.
    /// </summary>
    /// <remarks>This is a mandatory field.</remarks>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type/format of the source.
    /// </summary>
    /// <remarks>
    /// Valid values are:
    /// <list type="bullet">
    ///   <item><description>"adblock" - AdGuard/uBlock Origin filter syntax (default)</description></item>
    ///   <item><description>"hosts" - /etc/hosts format</description></item>
    /// </list>
    /// </remarks>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "adblock";

    /// <summary>
    /// Gets or sets the source-specific transformations to apply.
    /// </summary>
    /// <remarks>
    /// These transformations are applied to this source before merging with other sources.
    /// See <see cref="Transformation"/> for available transformation values.
    /// </remarks>
    [JsonPropertyName("transformations")]
    public List<string> Transformations { get; set; } = [];

    /// <summary>
    /// Gets or sets the source-specific inclusion patterns.
    /// </summary>
    /// <remarks>
    /// Rules not matching any inclusion pattern will be excluded from this source.
    /// </remarks>
    [JsonPropertyName("inclusions")]
    public List<string> Inclusions { get; set; } = [];

    /// <summary>
    /// Gets or sets file paths or URLs containing source-specific inclusion patterns.
    /// </summary>
    [JsonPropertyName("inclusions_sources")]
    public List<string> InclusionsSources { get; set; } = [];

    /// <summary>
    /// Gets or sets the source-specific exclusion patterns.
    /// </summary>
    /// <remarks>
    /// Rules matching any exclusion pattern will be removed from this source.
    /// </remarks>
    [JsonPropertyName("exclusions")]
    public List<string> Exclusions { get; set; } = [];

    /// <summary>
    /// Gets or sets file paths or URLs containing source-specific exclusion patterns.
    /// </summary>
    [JsonPropertyName("exclusions_sources")]
    public List<string> ExclusionsSources { get; set; } = [];
}