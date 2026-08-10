namespace Bloqr.Dashboard.Console.Services;

/// <summary>
/// Interactive attribute-by-attribute compiler-config generator (#268): walks the user through
/// every field the epic specifies, then validates the result against both
/// <c>Bloqr.Compiler.Core</c>'s business-rule validator and the JSON Schema before writing it as
/// JSON or heavily-commented JSONC - the single source of truth for a compiler config, per the
/// epic's explicit requirement. Prompting/flow logic lives here; the reusable, testable pieces
/// (slugify, version format, source-type inference, JSONC rendering, schema validation) live in
/// <c>Bloqr.Dashboard.Core</c> so they don't require a console to exercise.
/// </summary>
public sealed class CompilerConfigWizardMenuService : MenuServiceBase
{
    private static readonly string[] CommonLicenses =
        ["MIT", "Apache-2.0", "GPL-3.0", "BSD-3-Clause", "ISC", "Unlicense", "Other (specify)"];

    private static readonly string[] ConflictStrategies = ["rename", "overwrite", "error"];
    private static readonly string[] HashVerificationModes = ["warning", "strict", "disabled"];
    private static readonly string[] OutputFormats = ["JSON", "JSONC"];

    private readonly ICompilerConfigSchemaValidator _schemaValidator;
    private readonly IDashboardConfigurationStore _configStore;
    private readonly IDashboardPaths _paths;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilerConfigWizardMenuService"/> class.
    /// </summary>
    public CompilerConfigWizardMenuService(
        IConsoleRenderer renderer,
        IConsolePrompter prompter,
        ICompilerConfigSchemaValidator schemaValidator,
        IDashboardConfigurationStore configStore,
        IDashboardPaths paths,
        ILogger<CompilerConfigWizardMenuService> logger)
        : base(renderer, prompter, logger)
    {
        _schemaValidator = schemaValidator;
        _configStore = configStore;
        _paths = paths;
    }

    /// <inheritdoc />
    public override string Title => "Compiler Config Wizard";

    /// <inheritdoc />
    protected override Dictionary<string, Func<Task>> GetMenuActions() => new()
    {
        ["Generate a new compiler config"] = RunWizardAsync,
    };

    private async Task RunWizardAsync()
    {
        Renderer.RenderRule("New Compiler Config");

        var name = PromptRequired("Filter list name");
        var description = Prompter.Prompt("Description (optional)", string.Empty);
        var homepage = Prompter.Prompt("Homepage URL (optional)", string.Empty);

        var config = new CompilerConfiguration
        {
            Name = name,
            Description = NullIfBlank(description),
            Homepage = NullIfBlank(homepage),
            License = PromptLicense(),
            Version = PromptVersion(),
            Output = PromptOutputSettings(name),
        };

        if (Prompter.Confirm("Enable hash verification against a .hashes.json sidecar? (#273)", false))
        {
            config.HashVerification = PromptHashVerification(config.Output.Path);
        }

        config.Archiving = PromptArchiving();
        config.Sources = PromptSources();
        config.Transformations = PromptTransformations("global");

        (config.Inclusions, config.InclusionsSources) = (
            PromptInlinePatterns("global inclusion"),
            PromptPatternFiles("inclusion"));
        (config.Exclusions, config.ExclusionsSources) = (
            PromptInlinePatterns("global exclusion"),
            PromptPatternFiles("exclusion"));

        ShowSummary(config);

        var businessValidation = ConfigurationValidator.Validate(config);
        var schemaValidation = _schemaValidator.Validate(config);

        if (!businessValidation.IsValid || !schemaValidation.IsValid)
        {
            Renderer.WriteStyled("Generated configuration failed validation - nothing was saved:", TextStyle.Error);
            foreach (var error in businessValidation.Errors)
            {
                Renderer.WriteStyled($"  [rule] {error.Field}: {error.Message}", TextStyle.Error);
            }

            foreach (var error in schemaValidation.Errors)
            {
                Renderer.WriteStyled($"  [schema] {error}", TextStyle.Error);
            }

            return;
        }

        Renderer.WriteStyled("Configuration is valid.", TextStyle.Success);

        var format = Prompter.Select("Save as", OutputFormats);
        var extension = format == "JSONC" ? "jsonc" : "json";
        var defaultSavePath = $"{CompilerConfigWizardHelpers.Slugify(name)}-compiler-config.{extension}";
        var savePath = Prompter.Prompt("Save this compiler config as", defaultSavePath);

        if (File.Exists(savePath) && !Prompter.Confirm($"{savePath} already exists. Overwrite?", false))
        {
            Renderer.WriteLine("Cancelled - nothing was saved.");
            return;
        }

        var content = format == "JSONC" ? CompilerConfigJsoncWriter.Write(config) : ToPrettyJson(config);
        var directory = Path.GetDirectoryName(Path.GetFullPath(savePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(savePath, content).ConfigureAwait(false);
        Renderer.WriteStyled($"Wrote {savePath}.", TextStyle.Success);

        await OfferProfileRegistrationAsync(savePath).ConfigureAwait(false);
    }

    private OutputSettings PromptOutputSettings(string name)
    {
        var defaultFileName = CompilerConfigWizardHelpers.DefaultOutputFileName(name);
        var path = Prompter.Prompt("Compiled output file path", defaultFileName);
        var strategy = Prompter.Select("Conflict strategy if that file already exists", ConflictStrategies);
        return new OutputSettings { Path = path, ConflictStrategy = strategy };
    }

    private HashVerificationSettings PromptHashVerification(string? outputPath)
    {
        var mode = Prompter.Select("Hash verification mode", HashVerificationModes);
        var requireHashesForRemote = Prompter.Confirm("Require an existing hash for every remote (URL) source?", false);
        var failOnMismatch = Prompter.Confirm("Fail compilation on any hash mismatch, regardless of mode?", false);

        var defaultDatabasePath = string.IsNullOrWhiteSpace(outputPath)
            ? ".hashes.json"
            : Path.Combine(Path.GetDirectoryName(outputPath) ?? ".", ".hashes.json");
        var databasePath = Prompter.Prompt("Hash database (.hashes.json) path", defaultDatabasePath);

        return new HashVerificationSettings
        {
            Mode = mode,
            RequireHashesForRemote = requireHashesForRemote,
            FailOnMismatch = failOnMismatch,
            HashDatabasePath = databasePath,
        };
    }

    private ArchivingSettings PromptArchiving()
    {
        // Default enabled/automatic per the epic's explicit spec for this setting.
        var enabled = Prompter.Confirm("Enable archiving of replaced output files?", true);
        if (!enabled)
        {
            return new ArchivingSettings { Enabled = false };
        }

        var retentionText = Prompter.Prompt("Archive retention (days)", "90");
        var retentionDays = int.TryParse(retentionText, out var parsed) && parsed >= 1 ? parsed : 90;
        return new ArchivingSettings { Enabled = true, Mode = "automatic", RetentionDays = retentionDays };
    }

    private List<FilterSource> PromptSources()
    {
        var sources = new List<FilterSource>();
        Renderer.WriteLine("Add filter sources (local file paths or URLs). At least one is required.");

        do
        {
            var sourcePath = PromptRequired("Source path or URL");
            var name = Prompter.Prompt("Source name", CompilerConfigWizardHelpers.DefaultSourceName(sourcePath));
            var type = PromptSourceType(sourcePath);

            var source = new FilterSource { Source = sourcePath, Name = name, Type = type };

            if (Prompter.Confirm("Apply per-source transformations?", false))
            {
                source.Transformations = PromptTransformations("this source");
            }

            source.Inclusions = PromptInlinePatterns("this source's inclusion");
            source.Exclusions = PromptInlinePatterns("this source's exclusion");

            sources.Add(source);
            Renderer.WriteStyled($"Added source '{name}'.", TextStyle.Success);
            Renderer.WriteLine();
        }
        while (Prompter.Confirm("Add another source?", false));

        return sources;
    }

    private string PromptSourceType(string sourcePath)
    {
        var isRemote = Uri.TryCreate(sourcePath, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https");

        if (isRemote)
        {
            // Content isn't available to inspect without fetching it, so ask directly instead
            // of guessing.
            return Prompter.Select("Source type (can't be inferred for a remote URL)", SourceTypeHelper.AllSourceTypes);
        }

        var inferred = CompilerConfigWizardHelpers.InferLocalSourceType(sourcePath);
        return Prompter.Confirm($"Inferred type '{inferred}' from the file's content - correct?", true)
            ? inferred
            : Prompter.Select("Source type", SourceTypeHelper.AllSourceTypes);
    }

    private List<string> PromptTransformations(string scope)
    {
        var useRecommended = Prompter.Confirm(
            $"Use the recommended transformation set for {scope} " +
            $"({string.Join(", ", TransformationHelper.RecommendedTransformations)})?",
            true);

        return useRecommended
            ? TransformationHelper.RecommendedTransformations.ToList()
            : Prompter.MultiSelect("Select transformations", TransformationHelper.AllTransformations).ToList();
    }

    private List<string> PromptInlinePatterns(string kind)
    {
        var patterns = new List<string>();
        if (!Prompter.Confirm($"Add {kind} patterns?", false))
        {
            return patterns;
        }

        Renderer.WriteLine(
            "Enter patterns one at a time - a plain string, a wildcard like \"*.example.com\", " +
            "or a \"/regex/\". Leave blank to finish.");

        while (true)
        {
            var pattern = Prompter.Prompt("Pattern (blank to finish)", string.Empty);
            if (string.IsNullOrWhiteSpace(pattern))
            {
                break;
            }

            patterns.Add(pattern);
        }

        return patterns;
    }

    private List<string> PromptPatternFiles(string kind)
    {
        var files = new List<string>();
        if (!Prompter.Confirm(
            $"Reference {kind} pattern files from the Dashboard's pattern-files directory " +
            $"({_paths.PatternFilesDirectory})?",
            false))
        {
            return files;
        }

        Directory.CreateDirectory(_paths.PatternFilesDirectory);

        do
        {
            var fileName = ChoosePatternFile();
            files.Add(Path.Combine(_paths.PatternFilesDirectory, fileName));
        }
        while (Prompter.Confirm("Reference another pattern file?", false));

        return files;
    }

    private string ChoosePatternFile()
    {
        var existing = Directory.GetFiles(_paths.PatternFilesDirectory, "*.txt")
            .Select(Path.GetFileName)
            .Cast<string>()
            .ToList();

        if (existing.Count > 0 && Prompter.Confirm("Pick an existing pattern file instead of creating a new one?", true))
        {
            return Prompter.Select("Pattern file", existing);
        }

        // Strip any directory component (and thus any rooted path or ../ traversal) from the
        // user-typed name so the file can never land outside the dedicated pattern-files
        // directory - a plain filename is what the epic's "non-user-configurable directory"
        // requirement actually depends on.
        var fileName = Path.GetFileName(PromptRequired("New pattern file name (e.g. \"ads.txt\")"));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"pattern-{Guid.NewGuid():N}";
        }

        if (!fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".txt";
        }

        var lines = new List<string>();
        Renderer.WriteLine("Enter one pattern per line. Leave blank to finish.");
        while (true)
        {
            var line = Prompter.Prompt("Pattern (blank to finish)", string.Empty);
            if (string.IsNullOrWhiteSpace(line))
            {
                break;
            }

            lines.Add(line);
        }

        var fullPath = Path.Combine(_paths.PatternFilesDirectory, fileName);
        File.WriteAllLines(fullPath, lines);
        Renderer.WriteStyled($"Wrote {lines.Count} pattern(s) to {fullPath}.", TextStyle.Success);

        return fileName;
    }

    private string PromptLicense()
    {
        var choice = Prompter.Select("License", CommonLicenses);
        if (choice != "Other (specify)")
        {
            return choice;
        }

        return PromptRequired("License identifier");
    }

    private string PromptVersion()
    {
        while (true)
        {
            var version = Prompter.Prompt("Version (MAJOR.MINOR.PATCH)", "1.0.0");
            if (CompilerConfigWizardHelpers.IsValidVersion(version))
            {
                return version;
            }

            Renderer.WriteStyled(
                "Version must be strict MAJOR.MINOR.PATCH (e.g. \"1.0.0\") - no \"v\" prefix or prerelease suffix.",
                TextStyle.Warning);
        }
    }

    private string PromptRequired(string prompt)
    {
        while (true)
        {
            var value = Prompter.Prompt(prompt);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            Renderer.WriteStyled("This field is required.", TextStyle.Warning);
        }
    }

    private async Task OfferProfileRegistrationAsync(string configPath)
    {
        var load = await _configStore.LoadAsync(allowInteractiveRecovery: true).ConfigureAwait(false);
        if (load.Configuration.Profiles.Count == 0)
        {
            return;
        }

        if (!Prompter.Confirm("Add this config to an existing Dashboard profile?", false))
        {
            return;
        }

        var profileName = Prompter.Select("Profile", load.Configuration.Profiles.Keys);
        var profile = load.Configuration.Profiles[profileName];
        if (!profile.CompilerConfigs.Contains(configPath))
        {
            profile.CompilerConfigs.Add(configPath);
        }

        await _configStore.SaveAsync(load.Configuration).ConfigureAwait(false);
        Renderer.WriteStyled($"Added to profile '{profileName}'.", TextStyle.Success);
    }

    private void ShowSummary(CompilerConfiguration config)
    {
        Renderer.RenderRule("Summary");

        var table = new ConsoleTable { Title = "Compiler Config" };
        table.AddColumn("Field");
        table.AddColumn("Value");
        table.AddRow("Name", config.Name);
        table.AddRow("Description", config.Description ?? "(none)");
        table.AddRow("License", config.License ?? "(none)");
        table.AddRow("Version", config.Version ?? "(none)");
        table.AddRow("Output path", config.Output?.Path ?? "(none)");
        table.AddRow("Conflict strategy", config.Output?.ConflictStrategy ?? "(none)");
        table.AddRow("Hash verification", config.HashVerification is null ? "disabled" : config.HashVerification.Mode);
        table.AddRow("Archiving", config.Archiving?.Enabled == true ? $"enabled ({config.Archiving.RetentionDays}d)" : "disabled");
        table.AddRow("Sources", config.Sources.Count.ToString());
        table.AddRow("Global transformations", string.Join(", ", config.Transformations));
        Renderer.RenderTable(table);

        if (config.Sources.Count > 0)
        {
            var sourcesTable = new ConsoleTable { Title = "Sources" };
            sourcesTable.AddColumn("Name");
            sourcesTable.AddColumn("Source");
            sourcesTable.AddColumn("Type");
            foreach (var source in config.Sources)
            {
                sourcesTable.AddRow(source.Name ?? "", source.Source, source.Type);
            }

            Renderer.RenderTable(sourcesTable);
        }
    }

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string ToPrettyJson(CompilerConfiguration configuration) =>
        JsonSerializer.Serialize(
            configuration,
            new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
}
