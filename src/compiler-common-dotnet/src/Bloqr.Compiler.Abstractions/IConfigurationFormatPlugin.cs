namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Interface for plugins that provide custom configuration formats.
/// </summary>
public interface IConfigurationFormatPlugin : IPlugin
{
    /// <summary>
    /// Gets the file extensions this plugin handles (e.g., ".xml", ".ini").
    /// </summary>
    IReadOnlyCollection<string> SupportedExtensions { get; }

    /// <summary>
    /// Parses configuration from the specified content.
    /// </summary>
    /// <param name="content">The configuration file content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed configuration.</returns>
    Task<CompilerConfiguration> ParseAsync(
        string content,
        CancellationToken cancellationToken = default);
}