namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// A single row of cell values within a <see cref="ConsoleTable"/>.
/// </summary>
/// <param name="Cells">The cell values, in column order.</param>
public sealed record ConsoleTableRow(IReadOnlyList<string> Cells);
