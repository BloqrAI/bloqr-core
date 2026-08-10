namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Represents the <c>hashVerification</c> block of a compiler configuration: the
/// per-run policy for checking compiled artifacts against a <c>.hashes.json</c> sidecar.
/// </summary>
public class HashVerificationSettings
{
    /// <summary>
    /// Gets or sets the verification mode.
    /// </summary>
    /// <remarks>
    /// One of <c>"strict"</c> (a hash mismatch always fails compilation), <c>"warning"</c>
    /// (a mismatch is logged and raised as an event but compilation continues unless a
    /// handler escalates it), or <c>"disabled"</c> (no verification against the sidecar,
    /// though hashes are still computed and recorded to bootstrap trust for later runs).
    /// </remarks>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "warning";

    /// <summary>
    /// Gets or sets whether remote (URL) sources must already have a recorded hash in
    /// the sidecar database.
    /// </summary>
    [JsonPropertyName("requireHashesForRemote")]
    public bool RequireHashesForRemote { get; set; }

    /// <summary>
    /// Gets or sets whether a hash mismatch fails compilation regardless of <see cref="Mode"/>.
    /// </summary>
    [JsonPropertyName("failOnMismatch")]
    public bool FailOnMismatch { get; set; }

    /// <summary>
    /// Gets or sets the path to the <c>.hashes.json</c> sidecar database, resolved
    /// relative to the configuration file's directory.
    /// </summary>
    [JsonPropertyName("hashDatabasePath")]
    public string? HashDatabasePath { get; set; }
}
