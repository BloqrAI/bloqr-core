namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Options for the compilation process.
/// </summary>
public class CompilerOptions
{
    /// <summary>
    /// Gets or sets the path to the configuration file.
    /// </summary>
    public string? ConfigPath { get; set; }

    /// <summary>
    /// Gets or sets the output file path.
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Gets or sets whether to copy output to the rules directory.
    /// </summary>
    public bool CopyToRules { get; set; }

    /// <summary>
    /// Gets or sets the rules directory path.
    /// </summary>
    public string? RulesDirectory { get; set; }

    /// <summary>
    /// Gets or sets the configuration file format override.
    /// </summary>
    public ConfigurationFormat? Format { get; set; }

    /// <summary>
    /// Gets or sets whether to enable verbose logging.
    /// </summary>
    /// <remarks>
    /// When enabled, the hostlist-compiler will output additional
    /// debugging information during compilation.
    /// </remarks>
    public bool Verbose { get; set; }

    /// <summary>
    /// Gets or sets whether to validate the configuration before compilation.
    /// </summary>
    /// <remarks>
    /// When enabled, the configuration will be validated for errors
    /// and warnings before compilation starts.
    /// </remarks>
    public bool ValidateConfig { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to fail on configuration validation warnings.
    /// </summary>
    public bool FailOnWarnings { get; set; }

    /// <summary>
    /// Gets or sets the chunking options for parallel compilation.
    /// </summary>
    /// <remarks>
    /// When chunking is enabled, sources are split into multiple chunks
    /// and compiled in parallel for improved performance on large filter lists.
    /// </remarks>
    public ChunkingOptions? Chunking { get; set; }

    /// <summary>
    /// Creates default compiler options.
    /// </summary>
    public static CompilerOptions Default => new()
    {
        ValidateConfig = true,
        Verbose = false,
        CopyToRules = false,
        FailOnWarnings = false,
        Chunking = ChunkingOptions.Default
    };

    /// <summary>
    /// Creates compiler options optimized for large filter lists with parallel chunking enabled.
    /// </summary>
    public static CompilerOptions ForLargeLists => new()
    {
        ValidateConfig = true,
        Verbose = false,
        CopyToRules = false,
        FailOnWarnings = false,
        Chunking = ChunkingOptions.ForLargeLists
    };
}
