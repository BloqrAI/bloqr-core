namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Options for plugin loading and management.
/// </summary>
public class PluginOptions
{
    /// <summary>
    /// Gets or sets the plugin directories to scan.
    /// </summary>
    public IList<string> PluginDirectories { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether to auto-load plugins at startup.
    /// </summary>
    public bool AutoLoadPlugins { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of plugin IDs to disable.
    /// </summary>
    public IList<string> DisabledPlugins { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether to fail on plugin load errors.
    /// </summary>
    public bool FailOnPluginLoadError { get; set; }

    /// <summary>
    /// Gets or sets the plugin search pattern.
    /// </summary>
    public string SearchPattern { get; set; } = "*.dll";
}