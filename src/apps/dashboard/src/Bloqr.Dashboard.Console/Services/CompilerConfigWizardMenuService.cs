namespace Bloqr.Dashboard.Console.Services;

/// <summary>
/// Interactive attribute-by-attribute compiler-config generator (#268): walks the user through
/// every field the epic specifies, then validates the result against both
/// <c>Bloqr.Compiler.Core</c>'s business-rule validator and the JSON Schema before writing it as
/// JSON or heavily-commented JSONC - the single source of truth for a compiler config, per the
/// epic's explicit requirement. The reusable, testable pieces (slugify, version format,
/// source-type inference, JSONC rendering, schema validation) live in
/// <c>Bloqr.Dashboard.Core</c>; the prompting flow shared with the editor (#269) lives in
/// <see cref="CompilerConfigFormMenuServiceBase"/>.
/// </summary>
public sealed class CompilerConfigWizardMenuService : CompilerConfigFormMenuServiceBase
{
    private readonly ICompilerConfigSchemaValidator _schemaValidator;
    private readonly IDashboardConfigurationStore _configStore;

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
        : base(renderer, prompter, paths, logger)
    {
        _schemaValidator = schemaValidator;
        _configStore = configStore;
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
        config.DefaultEngine = PromptDefaultEngine();
        config.Sources = PromptSources();
        config.Transformations = PromptTransformations("global");

        (config.Inclusions, config.InclusionsSources) = (
            PromptInlinePatterns("global inclusion"),
            PromptPatternFiles("inclusion"));
        (config.Exclusions, config.ExclusionsSources) = (
            PromptInlinePatterns("global exclusion"),
            PromptPatternFiles("exclusion"));

        Renderer.RenderRule("Summary");
        ShowSummary(config);

        if (!ValidateAndReport(config, _schemaValidator))
        {
            Renderer.WriteLine("Nothing was saved.");
            return;
        }

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
}
