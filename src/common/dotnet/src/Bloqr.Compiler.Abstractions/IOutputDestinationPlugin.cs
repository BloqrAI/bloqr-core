namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Interface for plugins that provide custom output destinations.
/// </summary>
public interface IOutputDestinationPlugin : IPlugin
{
    /// <summary>
    /// Gets the scheme this destination handles (e.g., "s3", "azure", "ftp").
    /// </summary>
    string Scheme { get; }

    /// <summary>
    /// Writes compiled output to the destination.
    /// </summary>
    /// <param name="content">The compiled output content.</param>
    /// <param name="destination">The destination URI.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> WriteAsync(
        string content,
        Uri destination,
        CancellationToken cancellationToken = default);
}