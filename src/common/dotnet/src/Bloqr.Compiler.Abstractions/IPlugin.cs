namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Base interface for BloqrCompiler plugins.
/// Plugins extend the compiler with custom functionality.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Gets the unique identifier for this plugin.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the display name for this plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the plugin version.
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// Gets a brief description of what this plugin does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Initializes the plugin. Called once during application startup.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether this plugin is enabled.
    /// </summary>
    bool IsEnabled { get; }
}