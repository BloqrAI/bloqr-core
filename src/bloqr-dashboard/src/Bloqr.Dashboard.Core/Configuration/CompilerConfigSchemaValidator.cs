using Json.Schema;

namespace Bloqr.Dashboard.Core.Configuration;

/// <summary>
/// Default implementation of <see cref="ICompilerConfigSchemaValidator"/>, validating against the
/// embedded <c>compiler-config.schema.json</c> resource (kept in sync with
/// <c>schemas/compiler-config.schema.json</c> at the repo root), the same way
/// <see cref="DashboardConfigurationStore"/> validates the Dashboard's own config.
/// </summary>
public sealed class CompilerConfigSchemaValidator : ICompilerConfigSchemaValidator
{
    // See DashboardConfigurationStore's identical LazySchema field for why this must be a
    // process-wide singleton: JsonSchema.Net registers a parsed schema globally by its $id to
    // resolve $ref, and throws if the same $id is registered twice.
    private static readonly Lazy<JsonSchema> LazySchema = new(LoadEmbeddedSchema);

    // The schema types every optional field (output, hashVerification, archiving, description,
    // ...) as its bare JSON type, not `["object", "null"]` / `["string", "null"]` - so a literal
    // JSON null for an unset field would fail validation. Omitting null properties entirely
    // instead of writing them keeps a config with no optional fields set actually valid.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly JsonSchema _schema = LazySchema.Value;

    /// <inheritdoc />
    public ConfigurationValidationResult Validate(CompilerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var instance = JsonSerializer.SerializeToElement(configuration, SerializerOptions);
        var results = _schema.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.List });

        if (results.IsValid)
        {
            return ConfigurationValidationResult.Success;
        }

        var errors = (results.Details ?? [])
            .Where(d => !d.IsValid && d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
            .ToList();

        if (errors.Count == 0)
        {
            errors.Add("Configuration failed schema validation.");
        }

        return new ConfigurationValidationResult(false, errors);
    }

    private static JsonSchema LoadEmbeddedSchema()
    {
        var assembly = typeof(CompilerConfigSchemaValidator).Assembly;
        using var stream = assembly.GetManifestResourceStream("compiler-config.schema.json")
            ?? throw new InvalidOperationException(
                "Embedded resource 'compiler-config.schema.json' was not found in Bloqr.Dashboard.Core.");
        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }
}
