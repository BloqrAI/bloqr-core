namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Represents the <c>archiving</c> block of a compiler configuration: whether a file
/// being overwritten during output publishing is preserved first, and for how long.
/// </summary>
public class ArchivingSettings
{
    /// <summary>
    /// Gets or sets whether archiving is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the archiving mode.
    /// </summary>
    /// <remarks>
    /// Only <c>"automatic"</c> is currently implemented (archive-then-overwrite with no
    /// prompting); <c>"interactive"</c> and <c>"disabled"</c> are accepted by the schema
    /// but not yet acted on by <c>IOutputPublisher</c>.
    /// </remarks>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "automatic";

    /// <summary>
    /// Gets or sets how many days an archived file is retained before pruning.
    /// </summary>
    [JsonPropertyName("retentionDays")]
    public int RetentionDays { get; set; } = 90;
}
