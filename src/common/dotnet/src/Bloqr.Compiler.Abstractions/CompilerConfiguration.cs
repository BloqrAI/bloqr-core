namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Represents the configuration for the @adguard/hostlist-compiler.
/// Supports JSON, YAML, and TOML configuration file formats.
/// </summary>
/// <remarks>
/// <para>
/// This configuration maps directly to the hostlist-compiler configuration schema.
/// See: https://github.com/AdguardTeam/HostlistCompiler
/// </para>
/// <para>
/// Pattern matching for inclusions/exclusions supports:
/// <list type="bullet">
///   <item><description>Plain string matching</description></item>
///   <item><description>Wildcards (e.g., "*.example.com")</description></item>
///   <item><description>Regular expressions (e.g., "/regex/", case-insensitive by default)</description></item>
///   <item><description>Comments prefixed with "!"</description></item>
/// </list>
/// </para>
/// </remarks>
public class CompilerConfiguration
{
    /// <summary>
    /// Gets or sets the name of the filter list.
    /// </summary>
    /// <remarks>This is a mandatory field.</remarks>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the filter list.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the homepage URL for the filter list.
    /// </summary>
    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }

    /// <summary>
    /// Gets or sets the license identifier for the filter list.
    /// </summary>
    [JsonPropertyName("license")]
    public string? License { get; set; }

    /// <summary>
    /// Gets or sets the version of the filter list.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets where the compiled output is published and how conflicts with an
    /// existing file at that path are handled.
    /// </summary>
    [JsonPropertyName("output")]
    public OutputSettings? Output { get; set; }

    /// <summary>
    /// Gets or sets the hash-verification policy for compiled artifacts.
    /// </summary>
    [JsonPropertyName("hashVerification")]
    public HashVerificationSettings? HashVerification { get; set; }

    /// <summary>
    /// Gets or sets the archiving policy applied when publishing overwrites an existing output file.
    /// </summary>
    [JsonPropertyName("archiving")]
    public ArchivingSettings? Archiving { get; set; }

    /// <summary>
    /// Gets or sets the list of source filter lists to compile.
    /// </summary>
    /// <remarks>This is a mandatory field. At least one source must be specified.</remarks>
    [JsonPropertyName("sources")]
    public List<FilterSource> Sources { get; set; } = [];

    /// <summary>
    /// Gets or sets the default compilation engine/grammar for sources that don't set
    /// <see cref="FilterSource.Engine"/> explicitly and whose content can't be
    /// confidently auto-detected.
    /// </summary>
    /// <remarks>
    /// Valid values are <c>"dns"</c> and <c>"browser"</c>. Defaults to <c>"dns"</c> -
    /// existing configurations are unaffected.
    /// </remarks>
    [JsonPropertyName("defaultEngine")]
    public string? DefaultEngine { get; set; }

    /// <summary>
    /// Gets or sets the list of transformations to apply to the final compiled output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Transformations are always applied in a fixed order regardless of configuration order:
    /// RemoveComments, Compress, RemoveModifiers, Validate, ValidateAllowIp, Deduplicate,
    /// InvertAllow, RemoveEmptyLines, TrimLines, InsertFinalNewLine, ConvertToAscii.
    /// </para>
    /// <para>See <see cref="Transformation"/> for available transformation values.</para>
    /// </remarks>
    [JsonPropertyName("transformations")]
    public List<string> Transformations { get; set; } = [];

    /// <summary>
    /// Gets or sets the global inclusion patterns.
    /// </summary>
    /// <remarks>
    /// Rules not matching any inclusion pattern will be excluded.
    /// Supports plain strings, wildcards, and regex patterns.
    /// </remarks>
    [JsonPropertyName("inclusions")]
    public List<string> Inclusions { get; set; } = [];

    /// <summary>
    /// Gets or sets file paths or URLs containing global inclusion patterns.
    /// </summary>
    /// <remarks>
    /// Each file should contain one pattern per line.
    /// Lines starting with "!" are treated as comments.
    /// </remarks>
    [JsonPropertyName("inclusions_sources")]
    public List<string> InclusionsSources { get; set; } = [];

    /// <summary>
    /// Gets or sets the global exclusion patterns.
    /// </summary>
    /// <remarks>
    /// Rules matching any exclusion pattern will be removed.
    /// Supports plain strings, wildcards, and regex patterns.
    /// </remarks>
    [JsonPropertyName("exclusions")]
    public List<string> Exclusions { get; set; } = [];

    /// <summary>
    /// Gets or sets file paths or URLs containing global exclusion patterns.
    /// </summary>
    /// <remarks>
    /// Each file should contain one pattern per line.
    /// Lines starting with "!" are treated as comments.
    /// </remarks>
    [JsonPropertyName("exclusions_sources")]
    public List<string> ExclusionsSources { get; set; } = [];
}