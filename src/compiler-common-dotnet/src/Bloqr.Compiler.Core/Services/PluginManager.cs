using Microsoft.Extensions.Options;

namespace Bloqr.Compiler.Core.Services;

/// <summary>
/// Default implementation of the plugin manager.
/// </summary>
public class PluginManager : IPluginManager
{
    private readonly ILogger<PluginManager> _logger;
    private readonly PluginOptions _options;
    private readonly ConcurrentDictionary<string, PluginEntry> _plugins = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginManager"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The plugin options.</param>
    public PluginManager(
        ILogger<PluginManager> logger,
        IOptions<PluginOptions>? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new PluginOptions();
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<IPlugin> Plugins =>
        _plugins.Values.Select(e => e.Plugin).ToList().AsReadOnly();

    /// <inheritdoc/>
    public IEnumerable<T> GetPlugins<T>() where T : IPlugin =>
        _plugins.Values
            .Where(e => e.Plugin is T && e.Plugin.IsEnabled)
            .Select(e => (T)e.Plugin)
            .OrderBy(p => p is IRuleTransformationPlugin rtp ? rtp.Order : 0);

    /// <inheritdoc/>
    public IPlugin? GetPlugin(string pluginId) =>
        _plugins.TryGetValue(pluginId, out var entry) ? entry.Plugin : null;

    /// <inheritdoc/>
    public void Register(IPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        var entry = new PluginEntry(plugin)
        {
            IsEnabled = !_options.DisabledPlugins.Contains(plugin.Id)
        };

        if (_plugins.TryAdd(plugin.Id, entry))
        {
            _logger.LogInformation("Registered plugin: {PluginId} ({PluginName} v{Version})",
                plugin.Id, plugin.Name, plugin.Version);
        }
        else
        {
            _logger.LogWarning("Plugin already registered: {PluginId}", plugin.Id);
        }
    }

    /// <inheritdoc/>
    public void RegisterRange(IEnumerable<IPlugin> plugins)
    {
        foreach (var plugin in plugins)
        {
            Register(plugin);
        }
    }

    /// <inheritdoc/>
    public bool Unregister(string pluginId)
    {
        if (_plugins.TryRemove(pluginId, out var entry))
        {
            _logger.LogInformation("Unregistered plugin: {PluginId}", pluginId);
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public bool Enable(string pluginId)
    {
        if (_plugins.TryGetValue(pluginId, out var entry))
        {
            entry.IsEnabled = true;
            _logger.LogInformation("Enabled plugin: {PluginId}", pluginId);
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public bool Disable(string pluginId)
    {
        if (_plugins.TryGetValue(pluginId, out var entry))
        {
            entry.IsEnabled = false;
            _logger.LogInformation("Disabled plugin: {PluginId}", pluginId);
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public async Task InitializeAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in _plugins.Values.Where(e => e.IsEnabled && !e.IsInitialized))
        {
            try
            {
                await entry.Plugin.InitializeAsync(cancellationToken);
                entry.IsInitialized = true;
                _logger.LogDebug("Initialized plugin: {PluginId}", entry.Plugin.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize plugin: {PluginId}", entry.Plugin.Id);
                if (_options.FailOnPluginLoadError)
                {
                    throw;
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IPlugin>> LoadFromAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        var plugins = new List<IPlugin>();

        try
        {
            _logger.LogDebug("Loading plugins from assembly: {Path}", assemblyPath);

            var assembly = Assembly.LoadFrom(assemblyPath);
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in pluginTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (Activator.CreateInstance(type) is IPlugin plugin)
                    {
                        Register(plugin);
                        plugins.Add(plugin);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to instantiate plugin type: {Type}", type.FullName);
                    if (_options.FailOnPluginLoadError)
                    {
                        throw;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to load plugins from assembly: {Path}", assemblyPath);
            if (_options.FailOnPluginLoadError)
            {
                throw;
            }
        }

        return await Task.FromResult(plugins);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IPlugin>> LoadFromDirectoryAsync(
        string directoryPath,
        string searchPattern = "*.dll",
        CancellationToken cancellationToken = default)
    {
        var plugins = new List<IPlugin>();

        if (!Directory.Exists(directoryPath))
        {
            _logger.LogWarning("Plugin directory does not exist: {Path}", directoryPath);
            return plugins;
        }

        var assemblies = Directory.GetFiles(directoryPath, searchPattern, SearchOption.AllDirectories);
        _logger.LogDebug("Found {Count} assemblies in plugin directory: {Path}", assemblies.Length, directoryPath);

        foreach (var assemblyPath in assemblies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var loadedPlugins = await LoadFromAssemblyAsync(assemblyPath, cancellationToken);
            plugins.AddRange(loadedPlugins);
        }

        return plugins;
    }

    private class PluginEntry
    {
        public IPlugin Plugin { get; }
        public bool IsEnabled { get; set; } = true;
        public bool IsInitialized { get; set; }

        public PluginEntry(IPlugin plugin)
        {
            Plugin = plugin;
        }
    }
}
