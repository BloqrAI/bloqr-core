namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Publishes a freshly compiled output file to its configured durable destination,
/// applying <see cref="OutputSettings.ConflictStrategy"/> and, when enabled,
/// <see cref="ArchivingSettings"/> along the way.
/// </summary>
public interface IOutputPublisher
{
    /// <summary>
    /// Publishes <paramref name="compiledFilePath"/> to <paramref name="output"/>'s configured path.
    /// </summary>
    /// <param name="compiledFilePath">The freshly compiled file to publish.</param>
    /// <param name="output">The output settings, including the destination path and conflict strategy.</param>
    /// <param name="archiving">
    /// The archiving settings to apply when the <c>"overwrite"</c> conflict strategy replaces an
    /// existing file, or <see langword="null"/> to never archive.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the publish operation.</returns>
    Task<OutputPublishResult> PublishAsync(
        string compiledFilePath,
        OutputSettings output,
        ArchivingSettings? archiving,
        CancellationToken cancellationToken = default);
}
