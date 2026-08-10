namespace Bloqr.Compiler.Core.Configuration;

/// <summary>
/// Reads compiler configuration from JSON, YAML, or TOML files.
/// </summary>
/// <remarks>
/// <para>
/// This reader supports all @adguard/hostlist-compiler configuration options
/// across JSON, YAML, and TOML formats.
/// </para>
/// <para>
/// Property naming conventions:
/// <list type="bullet">
///   <item><description>JSON: snake_case (e.g., inclusions_sources)</description></item>
///   <item><description>YAML: snake_case (e.g., inclusions_sources)</description></item>
///   <item><description>TOML: snake_case (e.g., inclusions_sources)</description></item>
/// </list>
/// </para>
/// </remarks>
public class ConfigurationReader : IConfigurationReader
{
    private readonly ILogger<ConfigurationReader> _logger;
    private readonly IDeserializer _yamlDeserializer;
    private readonly ISerializer _yamlSerializer;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationReader"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ConfigurationReader(ILogger<ConfigurationReader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // YAML uses snake_case naming convention to match hostlist-compiler
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
            .Build();

        // JSON options with snake_case for compatibility with hostlist-compiler. ReadCommentHandling/
        // AllowTrailingCommas only affect deserialization, never serialization, so it's safe to set
        // them here even though this same instance is also used for writing (ToJson) - they let this
        // reader round-trip the heavily-commented .jsonc files the Dashboard's compiler-config wizard
        // (#268) and DetectFormat below both treat as JSON.
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }

    /// <inheritdoc/>
    public async Task<CompilerConfiguration> ReadConfigurationAsync(
        string configPath,
        ConfigurationFormat? format = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configPath))
            throw new ArgumentException("Config path cannot be null or empty.", nameof(configPath));

        if (!File.Exists(configPath))
            throw new FileNotFoundException($"Configuration file not found: {configPath}", configPath);

        var detectedFormat = format ?? DetectFormat(configPath);
        _logger.LogDebug("Reading configuration from {Path} with format {Format}", configPath, detectedFormat);

        var content = await File.ReadAllTextAsync(configPath, cancellationToken);

        var config = detectedFormat switch
        {
            ConfigurationFormat.Json => ParseJson(content),
            ConfigurationFormat.Yaml => ParseYaml(content),
            ConfigurationFormat.Toml => ParseToml(content),
            _ => throw new ArgumentException($"Unsupported format: {detectedFormat}")
        };

        _logger.LogDebug("Loaded configuration '{Name}' with {SourceCount} sources and {TransformCount} transformations",
            config.Name, config.Sources.Count, config.Transformations.Count);

        return config;
    }

    /// <inheritdoc/>
    public ConfigurationFormat DetectFormat(string filePath)
    {
        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();

        return extension switch
        {
            ".json" or ".jsonc" => ConfigurationFormat.Json,
            ".yaml" or ".yml" => ConfigurationFormat.Yaml,
            ".toml" => ConfigurationFormat.Toml,
            _ => throw new ArgumentException($"Unknown configuration file extension: {extension}")
        };
    }

    /// <inheritdoc/>
    public string ToJson(CompilerConfiguration configuration)
    {
        // Use snake_case for output to match hostlist-compiler expectations
        return JsonSerializer.Serialize(configuration, _jsonOptions);
    }

    /// <summary>
    /// Converts configuration to YAML format.
    /// </summary>
    /// <param name="configuration">The configuration to convert.</param>
    /// <returns>YAML string representation of the configuration.</returns>
    public string ToYaml(CompilerConfiguration configuration)
    {
        return _yamlSerializer.Serialize(configuration);
    }

    /// <summary>
    /// Validates the configuration and returns any validation errors.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <returns>A validation result containing any errors or warnings.</returns>
    public ValidationResult ValidateConfiguration(CompilerConfiguration configuration)
    {
        return ConfigurationValidator.Validate(configuration);
    }

    private CompilerConfiguration ParseJson(string content)
    {
        var config = JsonSerializer.Deserialize<CompilerConfiguration>(content, _jsonOptions);
        return config ?? throw new InvalidOperationException("Failed to parse JSON configuration");
    }

    private CompilerConfiguration ParseYaml(string content)
    {
        var config = _yamlDeserializer.Deserialize<CompilerConfiguration>(content);
        return config ?? throw new InvalidOperationException("Failed to parse YAML configuration");
    }

    private CompilerConfiguration ParseToml(string content)
    {
        // Parse TOML to a TomlTable first for flexible property mapping
        var tomlTable = Toml.ToModel(content);
        return ParseTomlTable(tomlTable);
    }

    private CompilerConfiguration ParseTomlTable(TomlTable table)
    {
        var config = new CompilerConfiguration();

        // Map root-level properties
        if (table.TryGetValue("name", out var name))
            config.Name = name?.ToString() ?? string.Empty;

        if (table.TryGetValue("description", out var desc))
            config.Description = desc?.ToString();

        if (table.TryGetValue("homepage", out var home))
            config.Homepage = home?.ToString();

        if (table.TryGetValue("license", out var license))
            config.License = license?.ToString();

        if (table.TryGetValue("version", out var version))
            config.Version = version?.ToString();

        // Map array properties
        config.Transformations = GetStringList(table, "transformations");
        config.Inclusions = GetStringList(table, "inclusions");
        config.InclusionsSources = GetStringList(table, "inclusions_sources");
        config.Exclusions = GetStringList(table, "exclusions");
        config.ExclusionsSources = GetStringList(table, "exclusions_sources");

        // Map sources array
        if (table.TryGetValue("sources", out var sourcesObj) && sourcesObj is TomlTableArray sources)
        {
            foreach (var sourceTable in sources)
            {
                config.Sources.Add(ParseFilterSource(sourceTable));
            }
        }

        return config;
    }

    private FilterSource ParseFilterSource(TomlTable table)
    {
        var source = new FilterSource();

        if (table.TryGetValue("name", out var name))
            source.Name = name?.ToString();

        if (table.TryGetValue("source", out var src))
            source.Source = src?.ToString() ?? string.Empty;

        if (table.TryGetValue("type", out var type))
            source.Type = type?.ToString() ?? "adblock";

        source.Transformations = GetStringList(table, "transformations");
        source.Inclusions = GetStringList(table, "inclusions");
        source.InclusionsSources = GetStringList(table, "inclusions_sources");
        source.Exclusions = GetStringList(table, "exclusions");
        source.ExclusionsSources = GetStringList(table, "exclusions_sources");

        return source;
    }

    private static List<string> GetStringList(TomlTable table, string key)
    {
        if (table.TryGetValue(key, out var value) && value is TomlArray array)
        {
            return array.Select(item => item?.ToString() ?? string.Empty).ToList();
        }
        return [];
    }
}
