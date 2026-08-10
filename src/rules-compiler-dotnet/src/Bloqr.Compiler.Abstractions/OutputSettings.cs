namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Represents the <c>output</c> block of a compiler configuration: where the compiled
/// filter list is published and what happens when that destination already exists.
/// </summary>
public class OutputSettings
{
    /// <summary>
    /// Gets or sets the durable output file path the compiled result is published to,
    /// distinct from the ephemeral path a single compile run writes to first.
    /// </summary>
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    /// <summary>
    /// Gets or sets the strategy used when <see cref="Path"/> already has a file.
    /// </summary>
    /// <remarks>
    /// One of <c>"rename"</c> (write the new output alongside the existing file with a
    /// sequential <c>_1</c>, <c>_2</c>, ... suffix), <c>"overwrite"</c> (replace the
    /// existing file, archiving it first if <see cref="ArchivingSettings.Enabled"/> is
    /// set), or <c>"error"</c> (fail the compilation instead of touching the existing
    /// file). Matches the enum in <c>schemas/compiler-config.schema.json</c>.
    /// </remarks>
    [JsonPropertyName("conflictStrategy")]
    public string ConflictStrategy { get; set; } = "rename";
}
