namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Provides helper methods for working with transformations.
/// </summary>
public static class TransformationHelper
{
    /// <summary>
    /// Gets all available transformation names as strings.
    /// </summary>
    public static readonly IReadOnlyList<string> AllTransformations =
    [
        nameof(Transformation.RemoveComments),
        nameof(Transformation.Compress),
        nameof(Transformation.RemoveModifiers),
        nameof(Transformation.Validate),
        nameof(Transformation.ValidateAllowIp),
        nameof(Transformation.Deduplicate),
        nameof(Transformation.InvertAllow),
        nameof(Transformation.RemoveEmptyLines),
        nameof(Transformation.TrimLines),
        nameof(Transformation.InsertFinalNewLine),
        nameof(Transformation.ConvertToAscii)
    ];

    /// <summary>
    /// Gets recommended transformations for a typical filter list compilation.
    /// </summary>
    /// <remarks>
    /// These transformations provide a good balance of validation, deduplication,
    /// and output formatting for most use cases.
    /// </remarks>
    public static readonly IReadOnlyList<string> RecommendedTransformations =
    [
        nameof(Transformation.Validate),
        nameof(Transformation.Deduplicate),
        nameof(Transformation.RemoveEmptyLines),
        nameof(Transformation.TrimLines),
        nameof(Transformation.InsertFinalNewLine)
    ];

    /// <summary>
    /// Gets minimal transformations for basic compilation.
    /// </summary>
    /// <remarks>
    /// Suitable when you want to preserve most of the original content
    /// while ensuring proper output formatting.
    /// </remarks>
    public static readonly IReadOnlyList<string> MinimalTransformations =
    [
        nameof(Transformation.Deduplicate),
        nameof(Transformation.InsertFinalNewLine)
    ];

    /// <summary>
    /// Gets transformations optimized for hosts file sources.
    /// </summary>
    /// <remarks>
    /// Use these transformations when compiling from /etc/hosts format sources.
    /// </remarks>
    public static readonly IReadOnlyList<string> HostsFileTransformations =
    [
        nameof(Transformation.Compress),
        nameof(Transformation.RemoveComments),
        nameof(Transformation.Deduplicate),
        nameof(Transformation.RemoveEmptyLines),
        nameof(Transformation.TrimLines),
        nameof(Transformation.InsertFinalNewLine)
    ];

    /// <summary>
    /// Validates that a transformation name is recognized.
    /// </summary>
    /// <param name="transformation">The transformation name to validate.</param>
    /// <returns>True if the transformation is valid; otherwise, false.</returns>
    public static bool IsValid(string transformation)
    {
        return AllTransformations.Contains(transformation, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates a list of transformation names.
    /// </summary>
    /// <param name="transformations">The transformation names to validate.</param>
    /// <returns>A list of invalid transformation names, or an empty list if all are valid.</returns>
    public static IReadOnlyList<string> GetInvalidTransformations(IEnumerable<string> transformations)
    {
        return transformations
            .Where(t => !IsValid(t))
            .ToList();
    }

    /// <summary>
    /// Parses a transformation name string to the enum value.
    /// </summary>
    /// <param name="transformation">The transformation name.</param>
    /// <param name="result">The parsed transformation enum value.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string transformation, out Transformation result)
    {
        return Enum.TryParse(transformation, ignoreCase: true, out result);
    }
}