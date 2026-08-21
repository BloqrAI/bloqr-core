namespace Bloqr.Dashboard.Core.Configuration;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for reading and writing the Dashboard's own
/// configuration model. Reading is JSONC-tolerant (comments and trailing commas are allowed)
/// with zero extra dependencies, via the built-in <see cref="System.Text.Json"/> options below.
/// </summary>
public static class DashboardJsonOptions
{
    /// <summary>
    /// Gets the shared, JSONC-tolerant options used for (de)serializing <see cref="DashboardConfiguration"/>.
    /// </summary>
    public static JsonSerializerOptions Instance { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
            // Optional string fields (e.g. defaultRulesDirectory) are typed as plain "string" —
            // not ["string","null"] — in the schema, so an unset value must be OMITTED, not
            // written as a JSON null, or schema validation rejects it.
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        options.Converters.Add(new DashboardThemeJsonConverter());
        options.Converters.Add(new DashboardLogLevelJsonConverter());

        return options;
    }
}
