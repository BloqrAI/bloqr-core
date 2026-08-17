namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Main orchestration interface for the rules compiler pipeline.
/// </summary>
public interface IBloqrCompilerService
{
    /// <summary>
    /// Executes the full compilation pipeline.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="outputPath">Optional output file path.</param>
    /// <param name="copyToRules">Whether to copy output to the rules directory.</param>
    /// <param name="rulesDirectory">Optional rules directory path.</param>
    /// <param name="format">Optional format override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The compilation result.</returns>
    Task<CompilerResult> RunAsync(
        string? configPath = null,
        string? outputPath = null,
        bool copyToRules = false,
        string? rulesDirectory = null,
        ConfigurationFormat? format = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the full compilation pipeline using compiler options.
    /// </summary>
    /// <param name="options">Compiler options including config path, output path, and other settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The compilation result.</returns>
    Task<CompilerResult> RunAsync(
        CompilerOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets version information for all components.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Version information.</returns>
    Task<VersionInfo> GetVersionInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the configuration file.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="format">Optional format override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed configuration.</returns>
    Task<CompilerConfiguration> ReadConfigurationAsync(
        string? configPath = null,
        ConfigurationFormat? format = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a configuration and returns any errors or warnings.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="format">Optional format override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with errors and warnings.</returns>
    Task<ValidationResult> ValidateConfigurationAsync(
        string? configPath = null,
        ConfigurationFormat? format = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a configuration object and returns any errors or warnings.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <returns>Validation result with errors and warnings.</returns>
    ValidationResult ValidateConfiguration(CompilerConfiguration configuration);
}
