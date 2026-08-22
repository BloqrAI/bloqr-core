namespace Bloqr.Compiler.Dotnet.Extensions;

/// <summary>
/// Extension methods for registering BloqrCompiler services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds BloqrCompiler services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBloqrCompiler(this IServiceCollection services)
    {
        return services.AddBloqrCompiler(_ => { });
    }

    /// <summary>
    /// Adds BloqrCompiler services to the service collection with custom options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure plugin options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBloqrCompiler(
        this IServiceCollection services,
        Action<PluginOptions> configureOptions)
    {
        // Configure options
        services.Configure(configureOptions);

        // Register helpers
        services.TryAddSingleton<CommandHelper>();

        // Register core services
        services.TryAddSingleton<IConfigurationReader, ConfigurationReader>();
        services.TryAddSingleton<IFilterCompiler, FilterCompiler>();
        services.TryAddSingleton<IOutputWriter, OutputWriter>();
        services.TryAddSingleton<IChunkingService, ChunkingService>();
        services.TryAddSingleton<IFileLockService, FileLockService>();
        services.TryAddSingleton<IHashDatabaseService, HashDatabaseService>();
        services.TryAddSingleton<IOutputPublisher, OutputPublisher>();
        services.TryAddSingleton<IBloqrValidatorService, BloqrValidatorService>();
        services.TryAddSingleton<IBloqrCompilerService, BloqrCompilerService>();
        services.TryAddSingleton<IBenchmarkService, BenchmarkService>();

        // Register extensibility services
        services.TryAddSingleton<IPluginManager, PluginManager>();
        services.TryAddSingleton<ICompilationEventDispatcher, CompilationEventDispatcher>();
        services.TryAddTransient<ICompilationPipelineBuilder, CompilationPipelineBuilder>();

        return services;
    }

    /// <summary>
    /// Adds a compilation event handler to the service collection.
    /// </summary>
    /// <typeparam name="THandler">The event handler type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCompilationEventHandler<THandler>(
        this IServiceCollection services)
        where THandler : class, ICompilationEventHandler
    {
        services.AddSingleton<ICompilationEventHandler, THandler>();
        return services;
    }

    /// <summary>
    /// Adds a compilation event handler instance to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="handler">The event handler instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCompilationEventHandler(
        this IServiceCollection services,
        ICompilationEventHandler handler)
    {
        services.AddSingleton(handler);
        return services;
    }

    /// <summary>
    /// Adds a plugin to the service collection.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBloqrCompilerPlugin<TPlugin>(
        this IServiceCollection services)
        where TPlugin : class, IPlugin
    {
        services.AddSingleton<IPlugin, TPlugin>();
        return services;
    }

    /// <summary>
    /// Adds a plugin instance to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="plugin">The plugin instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBloqrCompilerPlugin(
        this IServiceCollection services,
        IPlugin plugin)
    {
        services.AddSingleton(plugin);
        return services;
    }

    /// <summary>
    /// Adds a rule transformation plugin to the service collection.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRuleTransformationPlugin<TPlugin>(
        this IServiceCollection services)
        where TPlugin : class, IRuleTransformationPlugin
    {
        services.AddSingleton<IPlugin, TPlugin>();
        services.AddSingleton<IRuleTransformationPlugin, TPlugin>();
        return services;
    }

    /// <summary>
    /// Adds a rule validation plugin to the service collection.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRuleValidationPlugin<TPlugin>(
        this IServiceCollection services)
        where TPlugin : class, IRuleValidationPlugin
    {
        services.AddSingleton<IPlugin, TPlugin>();
        services.AddSingleton<IRuleValidationPlugin, TPlugin>();
        return services;
    }

    /// <summary>
    /// Adds a configuration format plugin to the service collection.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddConfigurationFormatPlugin<TPlugin>(
        this IServiceCollection services)
        where TPlugin : class, IConfigurationFormatPlugin
    {
        services.AddSingleton<IPlugin, TPlugin>();
        services.AddSingleton<IConfigurationFormatPlugin, TPlugin>();
        return services;
    }

    /// <summary>
    /// Adds an output destination plugin to the service collection.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOutputDestinationPlugin<TPlugin>(
        this IServiceCollection services)
        where TPlugin : class, IOutputDestinationPlugin
    {
        services.AddSingleton<IPlugin, TPlugin>();
        services.AddSingleton<IOutputDestinationPlugin, TPlugin>();
        return services;
    }

    /// <summary>
    /// Adds compilation middleware to the service collection.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCompilationMiddleware<TMiddleware>(
        this IServiceCollection services)
        where TMiddleware : class, ICompilationMiddleware
    {
        services.AddSingleton<ICompilationMiddleware, TMiddleware>();
        return services;
    }
}
