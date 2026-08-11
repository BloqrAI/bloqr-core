namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Event arguments for when configuration is loaded.
/// </summary>
public class ConfigurationLoadedEventArgs : CompilationEventArgs
{
    /// <summary>
    /// Gets the loaded configuration.
    /// </summary>
    public CompilerConfiguration Configuration { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationLoadedEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    /// <param name="configuration">The loaded configuration.</param>
    public ConfigurationLoadedEventArgs(CompilerOptions options, CompilerConfiguration configuration)
        : base(options)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }
}