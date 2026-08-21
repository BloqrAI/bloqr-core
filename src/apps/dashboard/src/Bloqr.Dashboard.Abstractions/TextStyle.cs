namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// A rendering-library-agnostic description of text styling (color and emphasis).
/// </summary>
public sealed class TextStyle
{
    /// <summary>
    /// Gets the foreground color for the styled text.
    /// </summary>
    public ConsoleColor? Foreground { get; init; }

    /// <summary>
    /// Gets a value indicating whether the text should be bold.
    /// </summary>
    public bool Bold { get; init; }

    /// <summary>
    /// Gets a value indicating whether the text should be italic.
    /// </summary>
    public bool Italic { get; init; }

    /// <summary>
    /// Gets a value indicating whether the text should be underlined.
    /// </summary>
    public bool Underline { get; init; }

    /// <summary>
    /// A pre-defined style for informational text.
    /// </summary>
    public static TextStyle Info { get; } = new() { Foreground = ConsoleColor.Blue };

    /// <summary>
    /// A pre-defined style for success text.
    /// </summary>
    public static TextStyle Success { get; } = new() { Foreground = ConsoleColor.Green };

    /// <summary>
    /// A pre-defined style for warning text.
    /// </summary>
    public static TextStyle Warning { get; } = new() { Foreground = ConsoleColor.Yellow };

    /// <summary>
    /// A pre-defined style for error text.
    /// </summary>
    public static TextStyle Error { get; } = new() { Foreground = ConsoleColor.Red, Bold = true };

    /// <summary>
    /// A pre-defined style for muted/secondary text.
    /// </summary>
    public static TextStyle Muted { get; } = new() { Foreground = ConsoleColor.Grey };
}
