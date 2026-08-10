namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Strategy interface for displaying collections of items in whatever format suits the item type
/// (table, list, cards, etc.), decoupling menu services from display formatting.
/// </summary>
/// <typeparam name="T">The type of items to display.</typeparam>
public interface IDisplayStrategy<in T>
{
    /// <summary>
    /// Displays a collection of items in summary form.
    /// </summary>
    /// <param name="items">The items to display.</param>
    void Display(IEnumerable<T> items);

    /// <summary>
    /// Displays a single item with detailed information.
    /// </summary>
    /// <param name="item">The item to display.</param>
    void DisplayDetails(T item);
}
