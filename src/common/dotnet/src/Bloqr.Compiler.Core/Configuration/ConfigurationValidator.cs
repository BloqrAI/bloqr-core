namespace Bloqr.Compiler.Core.Configuration;

/// <summary>
/// Validates compiler configuration before compilation.
/// </summary>
/// <remarks>
/// <see cref="ValidationError"/> and <see cref="ValidationResult"/> live in
/// <c>Bloqr.Compiler.Abstractions</c> (not nested here) so that interfaces
/// like <c>IRulesCompilerService</c> can reference them without depending
/// on this concrete implementation.
/// </remarks>
public static class ConfigurationValidator
{
    private static readonly string[] ValidConflictStrategies = ["rename", "overwrite", "error"];
    private static readonly string[] ValidHashVerificationModes = ["strict", "warning", "disabled"];
    private static readonly string[] ValidArchivingModes = ["automatic", "interactive", "disabled"];

    /// <summary>
    /// Validates a compiler configuration.
    /// </summary>
    /// <param name="config">The configuration to validate.</param>
    /// <returns>A validation result containing any errors or warnings.</returns>
    public static ValidationResult Validate(CompilerConfiguration config)
    {
        var result = new ValidationResult();

        // Structural JSON Schema validation first, so shape/type/enum violations the schema
        // catches (but these hand-written business rules below don't) get schema-accurate messages
        // rather than surfacing as a confusing downstream failure.
        CompilerConfigJsonSchemaValidator.Validate(config, result);

        // Validate required fields
        if (string.IsNullOrWhiteSpace(config.Name))
        {
            result.AddError("name", "Name is required");
        }

        if (config.Sources.Count == 0)
        {
            result.AddError("sources", "At least one source is required");
        }

        ValidateOutput(config.Output, result);
        ValidateHashVerification(config.HashVerification, result);
        ValidateArchiving(config.Archiving, result);

        // Validate global transformations
        ValidateTransformations(config.Transformations, "transformations", result);

        // Validate each source
        for (int i = 0; i < config.Sources.Count; i++)
        {
            ValidateSource(config.Sources[i], $"sources[{i}]", result);
        }

        // Validate inclusion/exclusion patterns
        ValidatePatterns(config.Inclusions, "inclusions", result);
        ValidatePatterns(config.Exclusions, "exclusions", result);
        ValidateSourceFiles(config.InclusionsSources, "inclusions_sources", result);
        ValidateSourceFiles(config.ExclusionsSources, "exclusions_sources", result);

        return result;
    }

    private static void ValidateOutput(OutputSettings? output, ValidationResult result)
    {
        if (output is null)
        {
            return;
        }

        if (!ValidConflictStrategies.Contains(output.ConflictStrategy, StringComparer.OrdinalIgnoreCase))
        {
            result.AddError("output.conflictStrategy",
                $"Invalid conflict strategy '{output.ConflictStrategy}'. Valid strategies are: {string.Join(", ", ValidConflictStrategies)}");
        }
    }

    private static void ValidateHashVerification(HashVerificationSettings? hashVerification, ValidationResult result)
    {
        if (hashVerification is null)
        {
            return;
        }

        if (!ValidHashVerificationModes.Contains(hashVerification.Mode, StringComparer.OrdinalIgnoreCase))
        {
            result.AddError("hashVerification.mode",
                $"Invalid hash verification mode '{hashVerification.Mode}'. Valid modes are: {string.Join(", ", ValidHashVerificationModes)}");
        }

        if (!string.Equals(hashVerification.Mode, "disabled", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(hashVerification.HashDatabasePath))
        {
            result.AddWarning("hashVerification.hashDatabasePath",
                "Hash verification is enabled but no hashDatabasePath was specified; hashes cannot be recorded or checked.");
        }
    }

    private static void ValidateArchiving(ArchivingSettings? archiving, ValidationResult result)
    {
        if (archiving is null)
        {
            return;
        }

        if (!ValidArchivingModes.Contains(archiving.Mode, StringComparer.OrdinalIgnoreCase))
        {
            result.AddError("archiving.mode",
                $"Invalid archiving mode '{archiving.Mode}'. Valid modes are: {string.Join(", ", ValidArchivingModes)}");
        }

        if (archiving.RetentionDays < 1)
        {
            result.AddError("archiving.retentionDays", "Retention days must be at least 1");
        }
    }

    private static void ValidateSource(FilterSource source, string path, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(source.Source))
        {
            result.AddError($"{path}.source", "Source URL or path is required");
        }

        // Validate source type
        if (!string.IsNullOrEmpty(source.Type) && !SourceTypeHelper.IsValid(source.Type))
        {
            result.AddError($"{path}.type",
                $"Invalid source type '{source.Type}'. Valid types are: {string.Join(", ", SourceTypeHelper.AllSourceTypes)}");
        }

        // Validate source-specific transformations
        ValidateTransformations(source.Transformations, $"{path}.transformations", result);

        // Validate patterns
        ValidatePatterns(source.Inclusions, $"{path}.inclusions", result);
        ValidatePatterns(source.Exclusions, $"{path}.exclusions", result);
        ValidateSourceFiles(source.InclusionsSources, $"{path}.inclusions_sources", result);
        ValidateSourceFiles(source.ExclusionsSources, $"{path}.exclusions_sources", result);
    }

    private static void ValidateTransformations(List<string> transformations, string path, ValidationResult result)
    {
        var invalidTransformations = TransformationHelper.GetInvalidTransformations(transformations);
        foreach (var invalid in invalidTransformations)
        {
            result.AddError(path,
                $"Invalid transformation '{invalid}'. Valid transformations are: {string.Join(", ", TransformationHelper.AllTransformations)}");
        }
    }

    private static void ValidatePatterns(List<string> patterns, string path, ValidationResult result)
    {
        for (int i = 0; i < patterns.Count; i++)
        {
            var pattern = patterns[i];

            // Check for regex patterns
            if (pattern.StartsWith("/") && pattern.EndsWith("/") && pattern.Length > 2)
            {
                // Try to validate regex syntax
                try
                {
                    var regexPattern = pattern[1..^1]; // Remove leading and trailing slashes
                    _ = new System.Text.RegularExpressions.Regex(regexPattern);
                }
                catch (System.Text.RegularExpressions.RegexParseException ex)
                {
                    result.AddWarning($"{path}[{i}]", $"Invalid regex pattern: {ex.Message}");
                }
            }
        }
    }

    private static void ValidateSourceFiles(List<string> sources, string path, ValidationResult result)
    {
        for (int i = 0; i < sources.Count; i++)
        {
            var source = sources[i];

            // Skip URL sources - they'll be validated at runtime
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                continue;
            }

            // For local files, check if they exist
            if (!string.IsNullOrEmpty(source) && !File.Exists(source))
            {
                result.AddWarning($"{path}[{i}]", $"Source file not found: {source}");
            }
        }
    }
}
