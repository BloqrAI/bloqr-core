namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Interface for reading compiler configuration from various file formats.
/// </summary>
public interface IConfigurationReader
{
    /// <summary>
    /// Reads the compiler configuration from the specified file.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="format">Optional format override. If null, format is detected from extension.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed compiler configuration.</returns>
    Task<CompilerConfiguration> ReadConfigurationAsync(
        string configPath,
        ConfigurationFormat? format = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects the configuration format from the file extension.
    /// </summary>
    /// <param name="filePath">Path to the configuration file.</param>
    /// <returns>The detected format.</returns>
    ConfigurationFormat DetectFormat(string filePath);

    /// <summary>
    /// Converts the configuration to JSON format for hostlist-compiler.
    /// </summary>
    /// <param name="configuration">The configuration to convert.</param>
    /// <returns>JSON string representation.</returns>
    string ToJson(CompilerConfiguration configuration);
}
