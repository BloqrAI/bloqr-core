namespace Bloqr.Dashboard.Console.Services;

/// <summary>
/// Menu for running and validating compilations. Delegates all actual compilation work to
/// <see cref="IRulesCompilerService"/> (registered by <c>RulesCompiler</c>'s
/// <c>AddRulesCompiler()</c>) — this menu is presentation only.
/// </summary>
public sealed class CompileMenuService : MenuServiceBase
{
    private readonly IRulesCompilerService _compilerService;
    private readonly IDashboardConfigurationStore _configStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompileMenuService"/> class.
    /// </summary>
    public CompileMenuService(
        IConsoleRenderer renderer,
        IConsolePrompter prompter,
        IRulesCompilerService compilerService,
        IDashboardConfigurationStore configStore,
        ILogger<CompileMenuService> logger)
        : base(renderer, prompter, logger)
    {
        _compilerService = compilerService;
        _configStore = configStore;
    }

    /// <inheritdoc />
    public override string Title => "Compile Rules";

    /// <inheritdoc />
    protected override Dictionary<string, Func<Task>> GetMenuActions() => new()
    {
        ["Compile using active profile"] = CompileActiveProfileAsync,
        ["Compile using a specific config file"] = CompileSpecificConfigAsync,
        ["Validate a config file"] = ValidateConfigAsync,
        ["Show available transformations"] = ShowTransformationsAsync,
    };

    private async Task CompileActiveProfileAsync()
    {
        var load = await _configStore.LoadAsync(allowInteractiveRecovery: true).ConfigureAwait(false);
        var configuration = load.Configuration;

        var activeProfileName = configuration.Settings.ActiveProfile;
        if (activeProfileName is null || !configuration.Profiles.TryGetValue(activeProfileName, out var profile))
        {
            Renderer.WriteStyled("No active profile is set. Use Profile Management to activate one first.", TextStyle.Warning);
            return;
        }

        var configPath = profile.CompilerConfigs.Count switch
        {
            0 => null,
            1 => profile.CompilerConfigs[0],
            _ => Prompter.Select($"Profile '{activeProfileName}' has multiple configs — pick one", profile.CompilerConfigs),
        };

        if (configPath is null)
        {
            Renderer.WriteStyled($"Profile '{activeProfileName}' has no compiler configs.", TextStyle.Warning);
            return;
        }

        await RunCompilationAsync(configPath).ConfigureAwait(false);
    }

    private async Task CompileSpecificConfigAsync()
    {
        var configPath = Prompter.Prompt("Path to compiler config file");
        await RunCompilationAsync(configPath).ConfigureAwait(false);
    }

    private async Task RunCompilationAsync(string configPath)
    {
        var result = await Renderer.StatusAsync(
            $"Compiling {configPath}...",
            () => _compilerService.RunAsync(configPath)).ConfigureAwait(false);

        DisplayResult(result);
    }

    private async Task ValidateConfigAsync()
    {
        var configPath = Prompter.Prompt("Path to compiler config file to validate");
        var result = await _compilerService.ValidateConfigurationAsync(configPath).ConfigureAwait(false);

        if (result.IsValid)
        {
            Renderer.WriteStyled("Configuration is valid.", TextStyle.Success);
        }
        else
        {
            Renderer.WriteStyled($"Configuration has {result.Errors.Count} error(s).", TextStyle.Error);
        }

        foreach (var error in result.Errors)
        {
            Renderer.WriteStyled($"  [error] {error.Field}: {error.Message}", TextStyle.Error);
        }

        foreach (var warning in result.Warnings)
        {
            Renderer.WriteStyled($"  [warn]  {warning.Field}: {warning.Message}", TextStyle.Warning);
        }
    }

    private Task ShowTransformationsAsync()
    {
        var table = new ConsoleTable { Title = "Available Transformations" };
        table.AddColumn("Name");
        table.AddColumn("Recommended?", TextAlignment.Center);

        foreach (var transformation in TransformationHelper.AllTransformations)
        {
            var recommended = TransformationHelper.RecommendedTransformations.Contains(transformation) ? "yes" : "";
            table.AddRow(transformation, recommended);
        }

        Renderer.RenderTable(table);
        return Task.CompletedTask;
    }

    private void DisplayResult(CompilerResult result)
    {
        if (result.Success)
        {
            Renderer.WriteStyled(
                $"Compiled '{result.ConfigName}': {result.RuleCount} rules -> {result.OutputPath} " +
                $"({result.ElapsedMs}ms)",
                TextStyle.Success);
        }
        else
        {
            Renderer.WriteStyled($"Compilation failed: {result.ErrorMessage}", TextStyle.Error);
        }

        if (result.CopiedToRules)
        {
            Renderer.WriteLine($"Copied to: {result.RulesDestination}");
        }
    }
}
