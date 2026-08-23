namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Represents the result of a compilation operation.
/// </summary>
public class CompilerResult
{
    /// <summary>
    /// Gets or sets whether the compilation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the name from the configuration.
    /// </summary>
    public string ConfigName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version from the configuration.
    /// </summary>
    public string? ConfigVersion { get; set; }

    /// <summary>
    /// Gets or sets the number of rules in the output.
    /// </summary>
    public int RuleCount { get; set; }

    /// <summary>
    /// Gets or sets the path to the output file.
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA-384 hash of the output file.
    /// </summary>
    public string OutputHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the output was copied to the rules directory.
    /// </summary>
    public bool CopiedToRules { get; set; }

    /// <summary>
    /// Gets or sets the destination path if copied to rules.
    /// </summary>
    public string? RulesDestination { get; set; }

    /// <summary>
    /// Gets or sets the elapsed time in milliseconds.
    /// </summary>
    public long ElapsedMs { get; set; }

    /// <summary>
    /// Gets or sets the start time of compilation.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time of compilation.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets or sets any error message if the compilation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the standard output from the compiler.
    /// </summary>
    public string StandardOutput { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the standard error from the compiler.
    /// </summary>
    public string StandardError { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the browser-syntax artifact, present only when the
    /// configuration mixed DNS and browser-syntax sources (producing two artifacts).
    /// </summary>
    public string? BrowserOutputPath { get; set; }

    /// <summary>
    /// Gets or sets the SHA-384 hash of the browser-syntax artifact, when present.
    /// </summary>
    public string? BrowserOutputHash { get; set; }

    /// <summary>
    /// Gets or sets the number of rules in the browser-syntax artifact, when present.
    /// </summary>
    public int? BrowserRuleCount { get; set; }
}
