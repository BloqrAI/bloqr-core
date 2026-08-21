namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// A rendering-library-agnostic description of a table, for display via <see cref="IConsoleRenderer.RenderTable"/>.
/// </summary>
public sealed class ConsoleTable
{
    /// <summary>
    /// Gets or sets the optional title displayed above the table.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets the table's column definitions, in display order.
    /// </summary>
    public List<ConsoleTableColumn> Columns { get; } = [];

    /// <summary>
    /// Gets the table's rows.
    /// </summary>
    public List<ConsoleTableRow> Rows { get; } = [];

    /// <summary>
    /// Adds a column to the table.
    /// </summary>
    /// <param name="header">The column header text.</param>
    /// <param name="alignment">The column's text alignment.</param>
    /// <returns>This table, for chaining.</returns>
    public ConsoleTable AddColumn(string header, TextAlignment alignment = TextAlignment.Left)
    {
        Columns.Add(new ConsoleTableColumn(header, alignment));
        return this;
    }

    /// <summary>
    /// Adds a row of cell values to the table.
    /// </summary>
    /// <param name="cells">The cell values, in column order.</param>
    /// <returns>This table, for chaining.</returns>
    public ConsoleTable AddRow(params string[] cells)
    {
        Rows.Add(new ConsoleTableRow(cells));
        return this;
    }
}
