namespace Bloqr.Dashboard.Console.Services;

/// <summary>
/// Config consumption, round-trip editing, and versioning for existing compiler configs (#269) -
/// the reverse direction of <see cref="CompilerConfigWizardMenuService"/> (#268), which only
/// generates new ones. Shares its field-by-field prompting with the wizard via
/// <see cref="CompilerConfigFormMenuServiceBase"/>, pre-filled from the config being edited.
/// </summary>
public sealed class CompilerConfigEditorMenuService : CompilerConfigFormMenuServiceBase
{
    private readonly IConfigurationReader _configurationReader;
    private readonly ICompilerConfigGuard _configGuard;
    private readonly ICompilerConfigSchemaValidator _schemaValidator;
    private readonly ICompilerConfigVersionHistoryService _versionHistory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilerConfigEditorMenuService"/> class.
    /// </summary>
    public CompilerConfigEditorMenuService(
        IConsoleRenderer renderer,
        IConsolePrompter prompter,
        IConfigurationReader configurationReader,
        ICompilerConfigGuard configGuard,
        ICompilerConfigSchemaValidator schemaValidator,
        ICompilerConfigVersionHistoryService versionHistory,
        IDashboardPaths paths,
        ILogger<CompilerConfigEditorMenuService> logger)
        : base(renderer, prompter, paths, logger)
    {
        _configurationReader = configurationReader;
        _configGuard = configGuard;
        _schemaValidator = schemaValidator;
        _versionHistory = versionHistory;
    }

    /// <inheritdoc />
    public override string Title => "Compiler Config Editor";

    /// <inheritdoc />
    protected override Dictionary<string, Func<Task>> GetMenuActions() => new()
    {
        ["View a compiler config"] = ViewConfigAsync,
        ["Edit a compiler config"] = EditConfigAsync,
        ["Transform a compiler config's format"] = TransformFormatAsync,
        ["Show version history (git)"] = ShowVersionHistoryAsync,
    };

    private async Task ViewConfigAsync()
    {
        var configPath = PromptRequired("Path to compiler config file to view");
        if (!File.Exists(configPath))
        {
            Renderer.WriteStyled($"{configPath} does not exist.", TextStyle.Error);
            return;
        }

        var view = Prompter.Select("View as", new[] { "Friendly summary", "Raw file content" });
        if (view == "Raw file content")
        {
            var raw = await File.ReadAllTextAsync(configPath).ConfigureAwait(false);
            Renderer.RenderPanel(raw, Path.GetFileName(configPath));
            return;
        }

        try
        {
            var config = await _configurationReader.ReadConfigurationAsync(configPath).ConfigureAwait(false);
            Renderer.RenderRule(configPath);
            ShowSummary(config);
        }
        catch (Exception ex)
        {
            Renderer.WriteStyled($"Failed to parse {configPath}: {ex.Message}", TextStyle.Error);
        }
    }

    private async Task EditConfigAsync()
    {
        var configPath = PromptRequired("Path to compiler config file to edit");
        if (!File.Exists(configPath))
        {
            Renderer.WriteStyled($"{configPath} does not exist.", TextStyle.Error);
            return;
        }

        var current = await LoadForEditAsync(configPath).ConfigureAwait(false);
        if (current is null)
        {
            return;
        }

        Renderer.RenderRule($"Editing {configPath}");
        ShowSummary(current);

        var name = Prompter.Prompt("Filter list name", current.Name);
        var description = Prompter.Prompt("Description (optional)", current.Description ?? string.Empty);
        var homepage = Prompter.Prompt("Homepage URL (optional)", current.Homepage ?? string.Empty);

        var edited = new CompilerConfiguration
        {
            Name = name,
            Description = NullIfBlank(description),
            Homepage = NullIfBlank(homepage),
            License = PromptLicense(current.License),
            Version = PromptVersion(current.Version),
            Output = PromptOutputSettings(name, current.Output),
        };

        if (Prompter.Confirm("Enable hash verification against a .hashes.json sidecar?", current.HashVerification is not null))
        {
            edited.HashVerification = PromptHashVerification(edited.Output.Path, current.HashVerification);
        }

        edited.Archiving = PromptArchiving(current.Archiving);
        edited.Sources = PromptSourcesForEdit(current.Sources);
        edited.Transformations = PromptTransformations("global", current.Transformations);

        (edited.Inclusions, edited.InclusionsSources) = (
            PromptInlinePatterns("global inclusion", current.Inclusions),
            PromptPatternFiles("inclusion", current.InclusionsSources));
        (edited.Exclusions, edited.ExclusionsSources) = (
            PromptInlinePatterns("global exclusion", current.Exclusions),
            PromptPatternFiles("exclusion", current.ExclusionsSources));

        Renderer.RenderRule("Updated Summary");
        ShowSummary(edited);

        if (!ValidateAndReport(edited, _schemaValidator))
        {
            Renderer.WriteLine("Nothing was saved.");
            return;
        }

        await SaveEditedConfigAsync(configPath, edited).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads the config to edit, honoring the "optional backup-on-edit" toggle: when accepted,
    /// loading goes through <see cref="ICompilerConfigGuard"/> (which backs up the file as a
    /// side effect of a successful load); when declined, the file is read directly with no backup.
    /// </summary>
    private async Task<CompilerConfiguration?> LoadForEditAsync(string configPath)
    {
        if (Prompter.Confirm("Back up the current version before editing?", true))
        {
            var guardResult = await _configGuard.LoadAsync(configPath).ConfigureAwait(false);
            if (!guardResult.Success || guardResult.Configuration is null)
            {
                Renderer.WriteStyled($"Failed to load {configPath}: {guardResult.Message}", TextStyle.Error);
                return null;
            }

            return guardResult.Configuration;
        }

        try
        {
            return await _configurationReader.ReadConfigurationAsync(configPath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Renderer.WriteStyled($"Failed to read {configPath}: {ex.Message}", TextStyle.Error);
            return null;
        }
    }

    private async Task SaveEditedConfigAsync(string configPath, CompilerConfiguration edited)
    {
        var currentIsJsonc = Path.GetExtension(configPath).Equals(".jsonc", StringComparison.OrdinalIgnoreCase);
        var formats = currentIsJsonc ? new[] { "JSONC", "JSON" } : new[] { "JSON", "JSONC" };
        var format = Prompter.Select("Save as", formats);

        var extension = format == "JSONC" ? "jsonc" : "json";
        var defaultSavePath = Path.ChangeExtension(configPath, extension);
        var savePath = Prompter.Prompt("Save this compiler config as", defaultSavePath);

        if (!string.Equals(Path.GetFullPath(savePath), Path.GetFullPath(configPath), StringComparison.OrdinalIgnoreCase) &&
            File.Exists(savePath) &&
            !Prompter.Confirm($"{savePath} already exists. Overwrite?", false))
        {
            Renderer.WriteLine("Cancelled - nothing was saved.");
            return;
        }

        var content = format == "JSONC" ? CompilerConfigJsoncWriter.Write(edited) : ToPrettyJson(edited);
        await File.WriteAllTextAsync(savePath, content).ConfigureAwait(false);
        Renderer.WriteStyled($"Wrote {savePath}.", TextStyle.Success);
    }

    private async Task TransformFormatAsync()
    {
        var configPath = PromptRequired("Path to compiler config file to transform");
        if (!File.Exists(configPath))
        {
            Renderer.WriteStyled($"{configPath} does not exist.", TextStyle.Error);
            return;
        }

        CompilerConfiguration config;
        try
        {
            config = await _configurationReader.ReadConfigurationAsync(configPath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Renderer.WriteStyled($"Failed to read {configPath}: {ex.Message}", TextStyle.Error);
            return;
        }

        var format = Prompter.Select("Transform to", OutputFormats);
        var extension = format == "JSONC" ? "jsonc" : "json";
        var defaultTargetPath = Path.ChangeExtension(configPath, extension);
        var targetPath = Prompter.Prompt("Save transformed config as", defaultTargetPath);

        if (File.Exists(targetPath) && !Prompter.Confirm($"{targetPath} already exists. Overwrite?", false))
        {
            Renderer.WriteLine("Cancelled.");
            return;
        }

        var content = format == "JSONC" ? CompilerConfigJsoncWriter.Write(config) : ToPrettyJson(config);
        await File.WriteAllTextAsync(targetPath, content).ConfigureAwait(false);
        Renderer.WriteStyled($"Wrote {targetPath}.", TextStyle.Success);
    }

    private async Task ShowVersionHistoryAsync()
    {
        var configPath = PromptRequired("Path to compiler config file");
        if (!File.Exists(configPath))
        {
            Renderer.WriteStyled($"{configPath} does not exist.", TextStyle.Error);
            return;
        }

        if (!await _versionHistory.IsUnderVersionControlAsync(configPath).ConfigureAwait(false))
        {
            Renderer.WriteStyled(
                $"{configPath} has no git history (not tracked in a git repository, or git isn't installed).",
                TextStyle.Warning);
            return;
        }

        var history = await _versionHistory.GetHistoryAsync(configPath).ConfigureAwait(false);

        var table = new ConsoleTable { Title = $"History for {configPath}" };
        table.AddColumn("Commit");
        table.AddColumn("Date");
        table.AddColumn("Author");
        table.AddColumn("Message");
        foreach (var revision in history)
        {
            table.AddRow(revision.ShortSha, revision.Date.ToString("u"), revision.Author, revision.Message);
        }

        Renderer.RenderTable(table);

        if (!Prompter.Confirm("Inspect or restore a specific revision?", false))
        {
            return;
        }

        var choices = history.Select(r => $"{r.ShortSha}  {r.Date:u}  {r.Message}").ToList();
        var chosenDisplay = Prompter.Select("Revision", choices);
        var chosenRevision = history[choices.IndexOf(chosenDisplay)];

        var action = Prompter.Select(
            "Action",
            new[] { "Show diff against current", "Show content at this revision", "Restore this revision" });

        switch (action)
        {
            case "Show diff against current":
                var diff = await _versionHistory.GetDiffAsync(configPath, chosenRevision.ShortSha).ConfigureAwait(false);
                Renderer.RenderPanel(
                    string.IsNullOrWhiteSpace(diff) ? "(no differences)" : diff,
                    $"Diff: {chosenRevision.ShortSha} -> working tree");
                break;

            case "Show content at this revision":
                var content = await _versionHistory.GetContentAtRevisionAsync(configPath, chosenRevision.ShortSha).ConfigureAwait(false);
                Renderer.RenderPanel(content, $"{configPath} @ {chosenRevision.ShortSha}");
                break;

            case "Restore this revision":
                await RestoreRevisionAsync(configPath, chosenRevision).ConfigureAwait(false);
                break;
        }
    }

    private async Task RestoreRevisionAsync(string configPath, CompilerConfigRevision revision)
    {
        if (!Prompter.Confirm($"Restore {configPath} to its content at {revision.ShortSha}?", false))
        {
            return;
        }

        if (Prompter.Confirm("Back up the current version first?", true))
        {
            // Best-effort: a currently-invalid file can't be backed up via the guard (it validates
            // before backing up), but that's fine - we're about to overwrite it with the
            // known-valid-at-the-time historical revision anyway.
            await _configGuard.LoadAsync(configPath).ConfigureAwait(false);
        }

        await _versionHistory.RestoreAsync(configPath, revision.ShortSha).ConfigureAwait(false);
        Renderer.WriteStyled($"Restored {configPath} to {revision.ShortSha}.", TextStyle.Success);
    }
}
