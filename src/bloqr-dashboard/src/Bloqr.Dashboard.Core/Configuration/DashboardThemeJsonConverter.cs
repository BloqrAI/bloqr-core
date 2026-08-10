namespace Bloqr.Dashboard.Core.Configuration;

/// <summary>
/// Converts <see cref="DashboardTheme"/> to/from the exact string values documented in
/// <c>schemas/dashboard-config.schema.json</c> (including the hyphenated "high-contrast", which
/// the built-in <see cref="JsonStringEnumConverter"/> naming policies cannot express).
/// </summary>
public sealed class DashboardThemeJsonConverter : JsonConverter<DashboardTheme>
{
    /// <inheritdoc />
    public override DashboardTheme Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value switch
        {
            "default" => DashboardTheme.Default,
            "dark" => DashboardTheme.Dark,
            "light" => DashboardTheme.Light,
            "high-contrast" => DashboardTheme.HighContrast,
            _ => throw new JsonException($"Unknown theme value '{value}'."),
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DashboardTheme value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(ToSchemaValue(value));
    }

    /// <summary>
    /// Converts a <see cref="DashboardTheme"/> to its schema string value.
    /// </summary>
    /// <param name="value">The theme value.</param>
    /// <returns>The schema string value.</returns>
    public static string ToSchemaValue(DashboardTheme value) => value switch
    {
        DashboardTheme.Default => "default",
        DashboardTheme.Dark => "dark",
        DashboardTheme.Light => "light",
        DashboardTheme.HighContrast => "high-contrast",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown theme."),
    };
}
