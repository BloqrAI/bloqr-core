namespace Bloqr.Dashboard.Tests;

/// <summary>
/// Covers <see cref="CompileResultRenderer"/> (#441) - the compile menu's dual-artifact summary,
/// shared logic behind <c>CompileMenuService</c>'s "Compile using active profile"/"Compile using a
/// specific config file" actions.
/// </summary>
public sealed class CompileResultRendererTests
{
    [Fact]
    public void Render_SuccessfulSingleEngineResult_ShowsOnlyThePrimaryArtifact()
    {
        var result = new CompilerResult
        {
            Success = true,
            ConfigName = "My List",
            RuleCount = 42,
            OutputPath = "out.txt",
            OutputHash = "abc123",
            ElapsedMs = 250,
        };

        var lines = CompileResultRenderer.Render(result);

        lines.Should().Contain(l => l.Contains("42 rules -> out.txt", StringComparison.Ordinal));
        lines.Should().NotContain(l => l.Contains("Browser artifact", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_MixedEngineResult_ShowsBothArtifactsWithPathRuleCountAndHash()
    {
        var result = new CompilerResult
        {
            Success = true,
            ConfigName = "Mixed List",
            RuleCount = 100,
            OutputPath = "out.txt",
            OutputHash = "dns-hash",
            BrowserOutputPath = "out.browser.txt",
            BrowserOutputHash = "browser-hash",
            BrowserRuleCount = 30,
            ElapsedMs = 500,
        };

        var lines = CompileResultRenderer.Render(result);

        lines.Should().Contain(l => l.Contains("100 rules -> out.txt", StringComparison.Ordinal));
        lines.Should().Contain(l => l.Contains("dns-hash", StringComparison.Ordinal));
        lines.Should().Contain(l => l.Contains("30 rules -> out.browser.txt", StringComparison.Ordinal));
        lines.Should().Contain(l => l.Contains("browser-hash", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_FailedResult_ShowsErrorMessageAndNoArtifacts()
    {
        var result = new CompilerResult { Success = false, ErrorMessage = "boom" };

        var lines = CompileResultRenderer.Render(result);

        lines.Should().ContainSingle();
        lines[0].Should().Contain("boom");
    }
}
