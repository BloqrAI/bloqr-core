namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Provides helper methods for working with source types.
/// </summary>
public static class SourceTypeHelper
{
    /// <summary>
    /// Gets all valid source type names as strings (lowercase).
    /// </summary>
    public static readonly IReadOnlyList<string> AllSourceTypes =
    [
        "adblock",
        "hosts"
    ];

    /// <summary>
    /// The default source type used when not specified.
    /// </summary>
    public const string DefaultSourceType = "adblock";

    /// <summary>
    /// Validates that a source type name is recognized.
    /// </summary>
    /// <param name="sourceType">The source type name to validate.</param>
    /// <returns>True if the source type is valid; otherwise, false.</returns>
    public static bool IsValid(string sourceType)
    {
        return AllSourceTypes.Contains(sourceType?.ToLowerInvariant() ?? string.Empty);
    }

    /// <summary>
    /// Parses a source type string to the enum value.
    /// </summary>
    /// <param name="sourceType">The source type name.</param>
    /// <param name="result">The parsed source type enum value.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string sourceType, out SourceType result)
    {
        return Enum.TryParse(sourceType, ignoreCase: true, out result);
    }
}