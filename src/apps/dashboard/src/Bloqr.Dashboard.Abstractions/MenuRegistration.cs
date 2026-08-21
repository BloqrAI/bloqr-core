namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Describes a top-level menu entry: display text paired with the menu service it activates.
/// </summary>
/// <param name="DisplayText">The text shown for this entry in the top-level menu.</param>
/// <param name="MenuService">The menu service to show when this entry is selected.</param>
public sealed record MenuRegistration(string DisplayText, IMenuService MenuService);
