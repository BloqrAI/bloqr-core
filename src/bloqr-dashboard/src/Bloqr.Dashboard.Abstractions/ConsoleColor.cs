namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// A rendering-library-agnostic set of named colors for console output.
/// </summary>
public enum ConsoleColor
{
    /// <summary>Default terminal foreground color.</summary>
    Default,

    /// <summary>Grey / muted text.</summary>
    Grey,

    /// <summary>Red, used for errors and destructive actions.</summary>
    Red,

    /// <summary>Green, used for success and confirmations.</summary>
    Green,

    /// <summary>Yellow, used for warnings.</summary>
    Yellow,

    /// <summary>Blue, used for informational text.</summary>
    Blue,

    /// <summary>Cyan, used for highlights and headings.</summary>
    Cyan,

    /// <summary>Magenta, used for secondary highlights.</summary>
    Magenta,

    /// <summary>White text.</summary>
    White,
}
