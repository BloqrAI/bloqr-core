namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Interface for a menu service that can be displayed to the user as part of the Dashboard's
/// menu-driven loop.
/// </summary>
public interface IMenuService
{
    /// <summary>
    /// Gets the display title for this menu.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Shows the menu and handles user interaction until the user navigates back or exits.
    /// </summary>
    Task ShowAsync();
}
