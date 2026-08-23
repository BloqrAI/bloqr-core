namespace Bloqr.Compiler.Core.Tests;

/// <summary>
/// Tests for <see cref="CommandHelper.GetBloqrCompilerCoreCommand"/>'s dual-engine
/// (<c>--engine</c>/<c>--browser-output</c>) argument passthrough (#436 — Wave 2 of epic
/// #432). Only covers the argument-building logic; a real <c>deno</c> on PATH is required
/// for a non-null return, so these skip when unavailable rather than asserting a specific
/// command path.
/// </summary>
public class CommandHelperTests
{
    private readonly CommandHelper _commandHelper = new(new Mock<ILogger<CommandHelper>>().Object);

    [Fact]
    public void GetBloqrCompilerCoreCommand_WithNoEngineOrBrowserOutput_OmitsBothFlags()
    {
        var result = _commandHelper.GetBloqrCompilerCoreCommand("config.json", "output.txt");
        if (result is null) return; // deno not on PATH in this environment

        Assert.DoesNotContain("--engine", result.Value.Args);
        Assert.DoesNotContain("--browser-output", result.Value.Args);
    }

    [Fact]
    public void GetBloqrCompilerCoreCommand_WithEngineAuto_OmitsTheEngineFlag()
    {
        // "auto" is the CLI's own default - passing it through explicitly would be a
        // no-op, so it's dropped like unset, keeping the command line identical to the
        // no-engine-specified case (the byte-identical-output guarantee's command-line
        // analogue).
        var result = _commandHelper.GetBloqrCompilerCoreCommand("config.json", "output.txt", engine: "auto");
        if (result is null) return;

        Assert.DoesNotContain("--engine", result.Value.Args);
    }

    [Theory]
    [InlineData("dns")]
    [InlineData("browser")]
    public void GetBloqrCompilerCoreCommand_WithExplicitEngine_PassesItThrough(string engine)
    {
        var result = _commandHelper.GetBloqrCompilerCoreCommand("config.json", "output.txt", engine: engine);
        if (result is null) return;

        Assert.Contains($"--engine \"{engine}\"", result.Value.Args);
    }

    [Fact]
    public void GetBloqrCompilerCoreCommand_WithBrowserOutputPath_PassesItThrough()
    {
        var result = _commandHelper.GetBloqrCompilerCoreCommand(
            "config.json", "output.txt", browserOutputPath: "output.browser.txt");
        if (result is null) return;

        Assert.Contains("--browser-output \"output.browser.txt\"", result.Value.Args);
    }

    [Fact]
    public void GetBloqrCompilerCoreCommand_WithEngineAndBrowserOutputAndVerbose_IncludesAllFlags()
    {
        var result = _commandHelper.GetBloqrCompilerCoreCommand(
            "config.json", "output.txt", verbose: true, engine: "browser", browserOutputPath: "output.browser.txt");
        if (result is null) return;

        Assert.Contains("--verbose", result.Value.Args);
        Assert.Contains("--engine \"browser\"", result.Value.Args);
        Assert.Contains("--browser-output \"output.browser.txt\"", result.Value.Args);
        Assert.Contains("--config \"config.json\"", result.Value.Args);
        Assert.Contains("--output \"output.txt\"", result.Value.Args);
    }
}
