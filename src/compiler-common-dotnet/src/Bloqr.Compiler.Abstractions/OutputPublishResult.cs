namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// The outcome of publishing a freshly compiled output file to its configured durable
/// destination via <see cref="IOutputPublisher"/>.
/// </summary>
public class OutputPublishResult
{
    /// <summary>
    /// Gets or sets whether publishing succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the final path the output now lives at, when <see cref="Success"/> is true.
    /// </summary>
    public string? FinalPath { get; set; }

    /// <summary>
    /// Gets or sets the path the previous file was archived to, if archiving occurred.
    /// </summary>
    public string? ArchivedPath { get; set; }

    /// <summary>
    /// Gets or sets an error message describing why publishing failed
    /// (e.g., the <c>"error"</c> conflict strategy found an existing file).
    /// </summary>
    public string? ErrorMessage { get; set; }
}
