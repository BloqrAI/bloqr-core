namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// A single column definition within a <see cref="ConsoleTable"/>.
/// </summary>
/// <param name="Header">The column header text.</param>
/// <param name="Alignment">The column's text alignment.</param>
public sealed record ConsoleTableColumn(string Header, TextAlignment Alignment = TextAlignment.Left);
