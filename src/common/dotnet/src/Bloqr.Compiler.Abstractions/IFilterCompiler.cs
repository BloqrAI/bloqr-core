namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Interface for compiling filter rules using @adguard/hostlist-compiler.
/// </summary>
public interface IFilterCompiler
{
    /// <summary>
    /// Compiles filter rules using the provided configuration.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="outputPath">Optional output file path. If null, uses default.</param>
    /// <param name="format">Optional format override for the configuration.</param>
    /// <param name="verbose">Whether to enable verbose logging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The compilation result.</returns>
    Task<CompilerResult> CompileAsync(
        string configPath,
        string? outputPath = null,
        ConfigurationFormat? format = null,
        bool verbose = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compiles filter rules using the provided options.
    /// </summary>
    /// <param name="options">Compiler options including config path, output path, and verbose mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The compilation result.</returns>
    Task<CompilerResult> CompileAsync(
        CompilerOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets version information for all compiler components.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Version information.</returns>
    Task<VersionInfo> GetVersionInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if hostlist-compiler is available.
    /// </summary>
    /// <returns>True if available, false otherwise.</returns>
    Task<bool> IsCompilerAvailableAsync();
}
