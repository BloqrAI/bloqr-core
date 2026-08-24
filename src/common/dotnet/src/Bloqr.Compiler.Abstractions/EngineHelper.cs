namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Provides helper values for working with compilation engine names, mirroring
/// <see cref="SourceTypeHelper"/>'s shape for the analogous <c>engine</c> concept introduced by
/// dual-engine compilation (#436).
/// </summary>
public static class EngineHelper
{
    /// <summary>
    /// Gets all valid, forceable engine names as strings (lowercase). Deliberately excludes
    /// <c>"auto"</c> here - that's the absence of a forced engine (<c>null</c>/omitted), not a
    /// distinct engine to force sources through. Callers that need an "auto" choice in a picker
    /// prepend it themselves (see <see cref="AllEngineChoices"/>).
    /// </summary>
    public static readonly IReadOnlyList<string> AllEngines = ["dns", "browser"];

    /// <summary>
    /// Gets the engine choices for a picker that also offers "auto" (per-source detection) -
    /// <see cref="DefaultEngine.Value"/> followed by <see cref="AllEngines"/>.
    /// </summary>
    public static readonly IReadOnlyList<string> AllEngineChoices = [DefaultEngine.Value, .. AllEngines];

    /// <summary>
    /// The sentinel value meaning "detect per-source" rather than a forced engine.
    /// </summary>
    public static class DefaultEngine
    {
        /// <summary>The literal value.</summary>
        public const string Value = "auto";
    }

    /// <summary>
    /// Validates that an engine name is recognized (case-insensitively), including <c>"auto"</c>.
    /// </summary>
    /// <param name="engine">The engine name to validate.</param>
    /// <returns><c>true</c> if the engine is valid; otherwise, <c>false</c>.</returns>
    public static bool IsValid(string? engine)
    {
        if (string.IsNullOrWhiteSpace(engine))
        {
            return true; // null/blank means "auto" - always valid.
        }

        return AllEngineChoices.Contains(engine, StringComparer.OrdinalIgnoreCase);
    }
}
