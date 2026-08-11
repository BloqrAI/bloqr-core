namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Interface for managing RulesCompiler plugins.
/// </summary>
public interface IPluginManager
{
    /// <summary>
    /// Gets all registered plugins.
    /// </summary>
    IReadOnlyCollection<IPlugin> Plugins { get; }

    /// <summary>
    /// Gets all plugins of a specific type.
    /// </summary>
    /// <typeparam name="T">The plugin type.</typeparam>
    /// <returns>Collection of matching plugins.</returns>
    IEnumerable<T> GetPlugins<T>() where T : IPlugin;

    /// <summary>
    /// Gets a specific plugin by its identifier.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <returns>The plugin if found; otherwise, null.</returns>
    IPlugin? GetPlugin(string pluginId);

    /// <summary>
    /// Registers a plugin.
    /// </summary>
    /// <param name="plugin">The plugin to register.</param>
    void Register(IPlugin plugin);

    /// <summary>
    /// Registers multiple plugins.
    /// </summary>
    /// <param name="plugins">The plugins to register.</param>
    void RegisterRange(IEnumerable<IPlugin> plugins);

    /// <summary>
    /// Unregisters a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <returns>True if the plugin was unregistered; otherwise, false.</returns>
    bool Unregister(string pluginId);

    /// <summary>
    /// Enables a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <returns>True if the plugin was enabled; otherwise, false.</returns>
    bool Enable(string pluginId);

    /// <summary>
    /// Disables a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <returns>True if the plugin was disabled; otherwise, false.</returns>
    bool Disable(string pluginId);

    /// <summary>
    /// Initializes all registered plugins.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InitializeAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads plugins from an assembly.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly containing plugins.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of loaded plugins.</returns>
    Task<IEnumerable<IPlugin>> LoadFromAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads plugins from a directory.
    /// </summary>
    /// <param name="directoryPath">Path to the directory containing plugin assemblies.</param>
    /// <param name="searchPattern">Search pattern for assemblies (default: "*.dll").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of loaded plugins.</returns>
    Task<IEnumerable<IPlugin>> LoadFromDirectoryAsync(
        string directoryPath,
        string searchPattern = "*.dll",
        CancellationToken cancellationToken = default);
}