namespace Bloqr.Dashboard.Console.Services;

/// <summary>
/// <see cref="IMenuServiceFactory"/> implementation resolving registered top-level menu services
/// from the DI container in a fixed order, so <see cref="DashboardApplication"/> doesn't need a
/// hard-coded switch over every menu service type.
/// </summary>
public sealed class MenuServiceFactory : IMenuServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyList<Type> _topLevelMenuServiceTypes;

    /// <summary>
    /// Initializes a new instance of the <see cref="MenuServiceFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">The DI container to resolve menu services from.</param>
    /// <param name="topLevelMenuServiceTypes">
    /// The concrete <see cref="IMenuService"/> types to show in the main menu, in display order.
    /// </param>
    public MenuServiceFactory(IServiceProvider serviceProvider, IReadOnlyList<Type> topLevelMenuServiceTypes)
    {
        _serviceProvider = serviceProvider;
        _topLevelMenuServiceTypes = topLevelMenuServiceTypes;
    }

    /// <inheritdoc />
    public IReadOnlyList<IMenuService> GetMenuServices() =>
        _topLevelMenuServiceTypes
            .Select(type => (IMenuService)_serviceProvider.GetRequiredService(type))
            .ToList();

    /// <inheritdoc />
    public TMenuService GetMenuService<TMenuService>() where TMenuService : class, IMenuService =>
        _serviceProvider.GetRequiredService<TMenuService>();
}
