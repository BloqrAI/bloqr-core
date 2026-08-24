namespace Bloqr.Dashboard.Console.Services;

/// <summary>
/// Shared field-by-field prompting logic for compiler configs, used by both the generation
/// wizard (<see cref="CompilerConfigWizardMenuService"/>, #268) and the editor
/// (<see cref="CompilerConfigEditorMenuService"/>, #269) so the two don't duplicate the same
/// prompts. Every prompt method accepts the field's current value (when editing) so it can be
/// offered as the default instead of an empty one; called with no current value, a method starts
/// from scratch, which is exactly what the wizard needs.
/// </summary>
public abstract class CompilerConfigFormMenuServiceBase : MenuServiceBase
{
    private static readonly string[] CommonLicenses =
        ["MIT", "Apache-2.0", "GPL-3.0", "BSD-3-Clause", "ISC", "Unlicense", "Other (specify)"];

    private static readonly string[] ConflictStrategies = ["rename", "overwrite", "error"];
    private static readonly string[] HashVerificationModes = ["warning", "strict", "disabled"];

    /// <summary>
    /// The compiler config output formats the Dashboard can write.
    /// </summary>
    protected static readonly string[] OutputFormats = ["JSON", "JSONC"];

    /// <summary>
    /// Resolves the Dashboard's well-known filesystem locations.
    /// </summary>
    protected IDashboardPaths Paths { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilerConfigFormMenuServiceBase"/> class.
    /// </summary>
    protected CompilerConfigFormMenuServiceBase(
        IConsoleRenderer renderer,
        IConsolePrompter prompter,
        IDashboardPaths paths,
        ILogger logger)
        : base(renderer, prompter, logger)
    {
        Paths = paths;
    }

    /// <summary>
    /// Prompts for the compiled-output settings.
    /// </summary>
    protected OutputSettings PromptOutputSettings(string name, OutputSettings? current = null)
    {
        var defaultFileName = current?.Path ?? CompilerConfigWizardHelpers.DefaultOutputFileName(name);
        var path = Prompter.Prompt("Compiled output file path", defaultFileName);
        var strategy = Prompter.Select(
            "Conflict strategy if that file already exists",
            WithCurrentFirst(ConflictStrategies, current?.ConflictStrategy));
        return new OutputSettings { Path = path, ConflictStrategy = strategy };
    }

    /// <summary>
    /// Prompts for hash-verification settings.
    /// </summary>
    protected HashVerificationSettings PromptHashVerification(string? outputPath, HashVerificationSettings? current = null)
    {
        var mode = Prompter.Select("Hash verification mode", WithCurrentFirst(HashVerificationModes, current?.Mode));
        var requireHashesForRemote = Prompter.Confirm(
            "Require an existing hash for every remote (URL) source?", current?.RequireHashesForRemote ?? false);
        var failOnMismatch = Prompter.Confirm(
            "Fail compilation on any hash mismatch, regardless of mode?", current?.FailOnMismatch ?? false);

        var defaultDatabasePath = current?.HashDatabasePath ?? (string.IsNullOrWhiteSpace(outputPath)
            ? ".hashes.json"
            : Path.Combine(Path.GetDirectoryName(outputPath) ?? ".", ".hashes.json"));
        var databasePath = Prompter.Prompt("Hash database (.hashes.json) path", defaultDatabasePath);

        return new HashVerificationSettings
        {
            Mode = mode,
            RequireHashesForRemote = requireHashesForRemote,
            FailOnMismatch = failOnMismatch,
            HashDatabasePath = databasePath,
        };
    }

    /// <summary>
    /// Prompts for archiving settings.
    /// </summary>
    protected ArchivingSettings PromptArchiving(ArchivingSettings? current = null)
    {
        // Default enabled/automatic per the epic's explicit spec for this setting, when there's
        // no existing value to default to.
        var enabled = Prompter.Confirm("Enable archiving of replaced output files?", current?.Enabled ?? true);
        if (!enabled)
        {
            return new ArchivingSettings { Enabled = false };
        }

        var retentionText = Prompter.Prompt("Archive retention (days)", (current?.RetentionDays ?? 90).ToString());
        var retentionDays = int.TryParse(retentionText, out var parsed) && parsed >= 1 ? parsed : 90;
        return new ArchivingSettings { Enabled = true, Mode = "automatic", RetentionDays = retentionDays };
    }

    /// <summary>
    /// Prompts for filter sources from scratch.
    /// </summary>
    protected List<FilterSource> PromptSources()
    {
        var sources = new List<FilterSource>();
        Renderer.WriteLine("Add filter sources (local file paths or URLs). At least one is required.");

        do
        {
            var sourcePath = PromptRequired("Source path or URL");
            var name = Prompter.Prompt("Source name", CompilerConfigWizardHelpers.DefaultSourceName(sourcePath));
            var type = PromptSourceType(sourcePath);
            var engine = PromptSourceEngine(sourcePath);

            var source = new FilterSource { Source = sourcePath, Name = name, Type = type, Engine = engine };

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

    /// <summary>
    /// Offers to keep an existing source list as-is, or replace it entirely via
    /// <see cref="PromptSources"/>. Editing individual sources in place isn't supported - a
    /// replace-wholesale flow is the simplest honest option for a console wizard.
    /// </summary>
    protected List<FilterSource> PromptSourcesForEdit(List<FilterSource> current)
    {
        Renderer.WriteLine($"Currently has {current.Count} source(s).");
        return Prompter.Confirm("Replace all sources?", false) ? PromptSources() : current;
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

    /// <summary>
    /// Prompts for this source's compilation engine (#441), mirroring <see cref="PromptSourceType"/>'s
    /// infer-then-confirm UX for a local file (via <see cref="CompilerConfigWizardHelpers.InferLocalSourceEngine"/>,
    /// the .NET port of the TypeScript compiler's <c>EngineDetector</c>, #433) and its
    /// can't-infer fallback for a remote URL. Returns <see langword="null"/> when the user leaves
    /// it at "auto" - the same "unset means auto-detect against <c>defaultEngine</c>, then
    /// <c>dns</c>" semantics <see cref="FilterSource.Engine"/> already documents, so a config that
    /// never mixes engines round-trips with no <c>engine</c> field noise.
    /// </summary>
    private string? PromptSourceEngine(string sourcePath)
    {
        var isRemote = Uri.TryCreate(sourcePath, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https");

        string choice;
        if (isRemote)
        {
            // Content isn't available to inspect without fetching it, so ask directly instead
            // of guessing - same fallback PromptSourceType uses for a remote URL.
            choice = Prompter.Select("Engine (can't be inferred for a remote URL)", EngineHelper.AllEngineChoices);
        }
        else
        {
            var inferred = CompilerConfigWizardHelpers.InferLocalSourceEngine(sourcePath);
            choice = Prompter.Confirm($"Inferred engine '{inferred}' from the file's content - correct?", true)
                ? inferred
                : Prompter.Select("Engine", EngineHelper.AllEngineChoices);
        }

        return choice == EngineHelper.DefaultEngine.Value ? null : choice;
    }

    /// <summary>
    /// Prompts for a transformation list, offering the recommended set as a shortcut.
    /// </summary>
    protected List<string> PromptTransformations(string scope, List<string>? current = null)
    {
        if (current is { Count: > 0 })
        {
            Renderer.WriteLine($"Current {scope} transformations: {string.Join(", ", current)}");
            if (!Prompter.Confirm("Replace them?", false))
            {
                return current;
            }
        }

        var useRecommended = Prompter.Confirm(
            $"Use the recommended transformation set for {scope} " +
            $"({string.Join(", ", TransformationHelper.RecommendedTransformations)})?",
            true);

        return useRecommended
            ? TransformationHelper.RecommendedTransformations.ToList()
            : Prompter.MultiSelect("Select transformations", TransformationHelper.AllTransformations).ToList();
    }

    /// <summary>
    /// Prompts for inline patterns, one at a time until a blank entry.
    /// </summary>
    protected List<string> PromptInlinePatterns(string kind, List<string>? current = null)
    {
        if (current is { Count: > 0 })
        {
            Renderer.WriteLine($"Current {kind} patterns: {string.Join(", ", current)}");
            if (!Prompter.Confirm("Replace them?", false))
            {
                return current;
            }
        }

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

    /// <summary>
    /// Prompts for references to pattern files in the Dashboard's dedicated pattern-files directory.
    /// </summary>
    protected List<string> PromptPatternFiles(string kind, List<string>? current = null)
    {
        if (current is { Count: > 0 })
        {
            Renderer.WriteLine($"Current {kind} pattern files: {string.Join(", ", current)}");
            if (!Prompter.Confirm("Replace them?", false))
            {
                return current;
            }
        }

        var files = new List<string>();
        if (!Prompter.Confirm(
            $"Reference {kind} pattern files from the Dashboard's pattern-files directory " +
            $"({Paths.PatternFilesDirectory})?",
            false))
        {
            return files;
        }

        Directory.CreateDirectory(Paths.PatternFilesDirectory);

        do
        {
            var fileName = ChoosePatternFile();
            files.Add(Path.Combine(Paths.PatternFilesDirectory, fileName));
        }
        while (Prompter.Confirm("Reference another pattern file?", false));

        return files;
    }

    private string ChoosePatternFile()
    {
        var existing = Directory.GetFiles(Paths.PatternFilesDirectory, "*.txt")
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

        var fullPath = Path.Combine(Paths.PatternFilesDirectory, fileName);
        File.WriteAllLines(fullPath, lines);
        Renderer.WriteStyled($"Wrote {lines.Count} pattern(s) to {fullPath}.", TextStyle.Success);

        return fileName;
    }

    /// <summary>
    /// Prompts for the config-level default engine (#441): the fallback a source's engine
    /// resolves to when it has no explicit <see cref="FilterSource.Engine"/> of its own and
    /// content sniffing found no strong signal, per <see cref="CompilerConfiguration.DefaultEngine"/>'s
    /// documented resolution order. Returns <see langword="null"/> for "auto" (no config-level
    /// override - each source falls back to the compiler's own "dns" default), so a config that
    /// doesn't need this round-trips with no <c>defaultEngine</c> field.
    /// </summary>
    protected string? PromptDefaultEngine(string? current = null)
    {
        var choices = EngineHelper.AllEngineChoices;
        var defaultChoice = current is null ? EngineHelper.DefaultEngine.Value : current;
        var choice = Prompter.Select(
            "Default engine for sources with no explicit engine of their own",
            WithCurrentFirst([.. choices], defaultChoice));

        return choice == EngineHelper.DefaultEngine.Value ? null : choice;
    }

    /// <summary>
    /// Prompts for a license, offering common values (current value first, if it's one of them)
    /// plus a custom option.
    /// </summary>
    protected string PromptLicense(string? current = null)
    {
        var choice = Prompter.Select("License", WithCurrentFirst(CommonLicenses, current));
        if (choice != "Other (specify)")
        {
            return choice;
        }

        return PromptRequired("License identifier", current);
    }

    /// <summary>
    /// Prompts for a strict <c>MAJOR.MINOR.PATCH</c> version, re-prompting until valid.
    /// </summary>
    protected string PromptVersion(string? current = null)
    {
        while (true)
        {
            var version = Prompter.Prompt("Version (MAJOR.MINOR.PATCH)", current ?? "1.0.0");
            if (CompilerConfigWizardHelpers.IsValidVersion(version))
            {
                return version;
            }

            Renderer.WriteStyled(
                "Version must be strict MAJOR.MINOR.PATCH (e.g. \"1.0.0\") - no \"v\" prefix or prerelease suffix.",
                TextStyle.Warning);
        }
    }

    /// <summary>
    /// Prompts for a non-blank value, re-prompting until one is given.
    /// </summary>
    protected string PromptRequired(string prompt, string? defaultValue = null)
    {
        while (true)
        {
            var value = Prompter.Prompt(prompt, defaultValue);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            Renderer.WriteStyled("This field is required.", TextStyle.Warning);
        }
    }

    /// <summary>
    /// Renders a friendly, table-based summary of a compiler config.
    /// </summary>
    protected void ShowSummary(CompilerConfiguration config)
    {
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

    /// <summary>
    /// Validates a config against both the business-rule validator and the JSON Schema, rendering
    /// any errors.
    /// </summary>
    /// <returns><c>true</c> if the config is valid against both.</returns>
    protected bool ValidateAndReport(CompilerConfiguration config, ICompilerConfigSchemaValidator schemaValidator)
    {
        var businessValidation = ConfigurationValidator.Validate(config);
        var schemaValidation = schemaValidator.Validate(config);

        if (businessValidation.IsValid && schemaValidation.IsValid)
        {
            Renderer.WriteStyled("Configuration is valid.", TextStyle.Success);
            return true;
        }

        Renderer.WriteStyled("Configuration failed validation:", TextStyle.Error);
        foreach (var error in businessValidation.Errors)
        {
            Renderer.WriteStyled($"  [rule] {error.Field}: {error.Message}", TextStyle.Error);
        }

        foreach (var error in schemaValidation.Errors)
        {
            Renderer.WriteStyled($"  [schema] {error}", TextStyle.Error);
        }

        return false;
    }

    /// <summary>
    /// Renders a config as pretty-printed JSON (no schema-invalid literal nulls; matches the
    /// serialization behavior <see cref="CompilerConfigSchemaValidator"/> relies on).
    /// </summary>
    protected static string ToPrettyJson(CompilerConfiguration configuration) =>
        JsonSerializer.Serialize(
            configuration,
            new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });

    /// <summary>
    /// Converts a blank string to <see langword="null"/>, otherwise returns it unchanged.
    /// </summary>
    protected static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string[] WithCurrentFirst(string[] choices, string? current)
    {
        if (current is null || !choices.Contains(current, StringComparer.OrdinalIgnoreCase))
        {
            return choices;
        }

        return choices.OrderBy(c => string.Equals(c, current, StringComparison.OrdinalIgnoreCase) ? 0 : 1).ToArray();
    }
}
