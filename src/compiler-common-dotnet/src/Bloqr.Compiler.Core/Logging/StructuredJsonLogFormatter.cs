using Serilog.Events;
using Serilog.Formatting;

namespace Bloqr.Compiler.Core.Logging;

/// <summary>
/// Serilog text formatter emitting one JSON line per event matching
/// <c>schemas/log-entry.schema.json</c> exactly: <c>timestamp</c>, <c>level</c>,
/// <c>messageTemplate</c>, <c>message</c>, and, when present, <c>exception</c>,
/// <c>sourceContext</c>, <c>application</c>, and any extra structured <c>properties</c>.
/// Written by hand rather than relying on a built-in formatter (e.g. Serilog's compact JSON
/// formatter) so the on-disk shape is guaranteed to match the schema, byte-for-byte, across every
/// app in this repo that logs through <see cref="LoggingServiceCollectionExtensions.AddStructuredLogging"/>
/// — not just the Dashboard's own log viewer, but any other consumer parsing these files.
/// </summary>
public sealed class StructuredJsonLogFormatter : ITextFormatter
{
    private static readonly JsonSerializerOptions SerializerOptions = new();

    /// <inheritdoc />
    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(output);

        using var messageWriter = new StringWriter();
        logEvent.RenderMessage(messageWriter);

        var entry = new Dictionary<string, object?>
        {
            ["timestamp"] = logEvent.Timestamp.ToUniversalTime().ToString("O"),
            ["level"] = logEvent.Level.ToString(),
            ["messageTemplate"] = logEvent.MessageTemplate.Text,
            ["message"] = messageWriter.ToString(),
        };

        if (logEvent.Exception is not null)
        {
            entry["exception"] = logEvent.Exception.ToString();
        }

        if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
        {
            entry["sourceContext"] = RenderPropertyValue(sourceContext);
        }

        if (logEvent.Properties.TryGetValue("Application", out var application))
        {
            entry["application"] = RenderPropertyValue(application);
        }

        var extraProperties = logEvent.Properties
            .Where(p => p.Key is not ("SourceContext" or "Application"))
            .ToDictionary(p => p.Key, p => RenderPropertyValue(p.Value));

        if (extraProperties.Count > 0)
        {
            entry["properties"] = extraProperties;
        }

        output.Write(JsonSerializer.Serialize(entry, SerializerOptions));
        output.Write(Environment.NewLine);
    }

    private static string? RenderPropertyValue(LogEventPropertyValue value) =>
        value is ScalarValue { Value: { } scalar } ? scalar.ToString() : value.ToString();
}
