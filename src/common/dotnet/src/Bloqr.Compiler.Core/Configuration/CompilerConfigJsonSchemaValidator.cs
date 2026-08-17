using Json.Schema;

namespace Bloqr.Compiler.Core.Configuration;

/// <summary>
/// Structural JSON Schema validation for <see cref="CompilerConfiguration"/> against the embedded
/// <c>compiler-config.schema.json</c> resource (kept in sync with
/// <c>schemas/compiler-config.schema.json</c> at the repo root). This catches shape/type/enum
/// violations the schema defines but <see cref="ConfigurationValidator"/>'s hand-written business
/// rules don't (e.g. an unknown extra property, or a field of the wrong JSON type) - schema-accurate
/// error messages instead of only ad hoc field checks. This is the single source of truth for
/// compiler-config schema evaluation; the Dashboard's own <c>ICompilerConfigSchemaValidator</c>
/// implementation delegates here rather than re-embedding and re-evaluating the same schema.
/// Named distinctly from that Dashboard type (rather than both being
/// <c>CompilerConfigSchemaValidator</c>) since both namespaces are globally used side by side in
/// the Dashboard projects and an identical name would be ambiguous there.
/// </summary>
public static class CompilerConfigJsonSchemaValidator
{
    // JsonSchema.Net registers a parsed schema globally by its $id to resolve $ref, and throws if
    // the same $id is registered twice - so this must be a process-wide singleton, same reasoning
    // as the Dashboard's identical LazySchema field.
    private static readonly Lazy<JsonSchema> LazySchema = new(LoadEmbeddedSchema);

    // The schema types every optional field (output, hashVerification, archiving, description, ...)
    // as its bare JSON type, not `["object", "null"]` / `["string", "null"]` - so a literal JSON
    // null for an unset field would fail validation. Omitting null properties entirely instead of
    // writing them keeps a config with no optional fields set actually valid.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Validates <paramref name="configuration"/> against the compiler-config JSON Schema, adding
    /// any violations to <paramref name="result"/> as errors.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <param name="result">The validation result to append schema errors to.</param>
    public static void Validate(CompilerConfiguration configuration, ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(result);

        var instance = JsonSerializer.SerializeToElement(configuration, SerializerOptions);
        var results = LazySchema.Value.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.List });

        if (results.IsValid)
        {
            return;
        }

        var violations = (results.Details ?? [])
            .Where(d => !d.IsValid && d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => (Location: d.InstanceLocation.ToString(), Message: e.Value)));

        var any = false;
        foreach (var (location, message) in violations)
        {
            result.AddError(string.IsNullOrEmpty(location) ? "schema" : location, message);
            any = true;
        }

        if (!any)
        {
            result.AddError("schema", "Configuration failed schema validation.");
        }
    }

    private static JsonSchema LoadEmbeddedSchema()
    {
        var assembly = typeof(CompilerConfigJsonSchemaValidator).Assembly;
        using var stream = assembly.GetManifestResourceStream("compiler-config.schema.json")
            ?? throw new InvalidOperationException(
                "Embedded resource 'compiler-config.schema.json' was not found in Bloqr.Compiler.Core.");
        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }
}
