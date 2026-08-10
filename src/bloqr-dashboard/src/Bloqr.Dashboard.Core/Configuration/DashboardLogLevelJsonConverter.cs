namespace Bloqr.Dashboard.Core.Configuration;

/// <summary>
/// Converts <see cref="DashboardLogLevel"/> to/from the exact string values documented in
/// <c>schemas/dashboard-config.schema.json</c>.
/// </summary>
public sealed class DashboardLogLevelJsonConverter : JsonConverter<DashboardLogLevel>
{
    /// <inheritdoc />
    public override DashboardLogLevel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value switch
        {
            "trace" => DashboardLogLevel.Trace,
            "debug" => DashboardLogLevel.Debug,
            "info" => DashboardLogLevel.Info,
            "warn" => DashboardLogLevel.Warn,
            "error" => DashboardLogLevel.Error,
            "silent" => DashboardLogLevel.Silent,
            _ => throw new JsonException($"Unknown log level value '{value}'."),
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DashboardLogLevel value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(ToSchemaValue(value));
    }

    /// <summary>
    /// Converts a <see cref="DashboardLogLevel"/> to its schema string value.
    /// </summary>
    /// <param name="value">The log level value.</param>
    /// <returns>The schema string value.</returns>
    public static string ToSchemaValue(DashboardLogLevel value) => value switch
    {
        DashboardLogLevel.Trace => "trace",
        DashboardLogLevel.Debug => "debug",
        DashboardLogLevel.Info => "info",
        DashboardLogLevel.Warn => "warn",
        DashboardLogLevel.Error => "error",
        DashboardLogLevel.Silent => "silent",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown log level."),
    };
}
