namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Factory for resolving registered <see cref="IMenuService"/> instances by type, used by the
/// main application loop to build the top-level menu without hard-coding a switch over every
/// menu service.
/// </summary>
public interface IMenuServiceFactory
{
    /// <summary>
    /// Gets all registered top-level menu services, in registration order.
    /// </summary>
    /// <returns>The registered menu services.</returns>
    IReadOnlyList<IMenuService> GetMenuServices();

    /// <summary>
    /// Resolves a specific menu service by its implementation type.
    /// </summary>
    /// <typeparam name="TMenuService">The menu service type to resolve.</typeparam>
    /// <returns>The resolved menu service instance.</returns>
    TMenuService GetMenuService<TMenuService>() where TMenuService : class, IMenuService;
}
