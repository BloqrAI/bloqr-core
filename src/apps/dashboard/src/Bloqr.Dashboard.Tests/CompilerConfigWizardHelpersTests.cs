namespace Bloqr.Dashboard.Tests;

public sealed class CompilerConfigWizardHelpersTests : IDisposable
{
    private readonly string _tempDirectory;

    public CompilerConfigWizardHelpersTests()
    {
        _tempDirectory = Directory.CreateTempSubdirectory("wizard-helpers-tests-").FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("AdGuard Base List", "adguard-base-list")]
    [InlineData("  Trim Me  ", "trim-me")]
    [InlineData("Multiple   Spaces", "multiple-spaces")]
    [InlineData("Special!@# Chars", "special-chars")]
    public void Slugify_ProducesExpectedSlug(string input, string expected)
    {
        CompilerConfigWizardHelpers.Slugify(input).Should().Be(expected);
    }

    [Fact]
    public void Slugify_WithNothingSurviving_FallsBackToDefault()
    {
        CompilerConfigWizardHelpers.Slugify("!!!").Should().Be("filter-list");
    }

    [Fact]
    public void DefaultOutputFileName_AppendsTxtExtension()
    {
        CompilerConfigWizardHelpers.DefaultOutputFileName("My List").Should().Be("my-list.txt");
    }

    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("0.0.1", true)]
    [InlineData("12.34.56", true)]
    [InlineData("v1.0.0", false)]
    [InlineData("1.0", false)]
    [InlineData("1.0.0-beta", false)]
    [InlineData("1.0.0+build", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsValidVersion_EnforcesStrictIntDotIntDotInt(string version, bool expected)
    {
        CompilerConfigWizardHelpers.IsValidVersion(version).Should().Be(expected);
    }

    [Fact]
    public void InferLocalSourceType_WithMissingFile_DefaultsToAdblock()
    {
        var path = Path.Combine(_tempDirectory, "does-not-exist.txt");

        CompilerConfigWizardHelpers.InferLocalSourceType(path).Should().Be("adblock");
    }

    [Fact]
    public void InferLocalSourceType_WithHostsStyleContent_ReturnsHosts()
    {
        var path = Path.Combine(_tempDirectory, "hosts.txt");
        File.WriteAllLines(path, ["0.0.0.0 ads.example.com", "127.0.0.1 tracker.example.com", "0.0.0.0 spam.example.com"]);

        CompilerConfigWizardHelpers.InferLocalSourceType(path).Should().Be("hosts");
    }

    [Fact]
    public void InferLocalSourceType_WithAdblockStyleContent_ReturnsAdblock()
    {
        var path = Path.Combine(_tempDirectory, "adblock.txt");
        File.WriteAllLines(path, ["||ads.example.com^", "##.ad-banner", "@@||allowed.example.com^"]);

        CompilerConfigWizardHelpers.InferLocalSourceType(path).Should().Be("adblock");
    }

    [Fact]
    public void InferLocalSourceType_SkipsCommentAndBlankLines()
    {
        var path = Path.Combine(_tempDirectory, "commented.txt");
        File.WriteAllLines(path, ["! comment", "# comment", "", "||ads.example.com^"]);

        CompilerConfigWizardHelpers.InferLocalSourceType(path).Should().Be("adblock");
    }

    [Fact]
    public void InferLocalSourceEngine_WithMissingFile_DefaultsToDns()
    {
        var path = Path.Combine(_tempDirectory, "does-not-exist.txt");

        CompilerConfigWizardHelpers.InferLocalSourceEngine(path).Should().Be("dns");
    }

    [Fact]
    public void InferLocalSourceEngine_WithHostsStyleContent_ReturnsDns()
    {
        var path = Path.Combine(_tempDirectory, "hosts.txt");
        File.WriteAllLines(path, ["0.0.0.0 ads.example.com", "127.0.0.1 tracker.example.com"]);

        CompilerConfigWizardHelpers.InferLocalSourceEngine(path).Should().Be("dns");
    }

    [Fact]
    public void InferLocalSourceEngine_WithBareDnsBlocklistLines_ReturnsDns()
    {
        var path = Path.Combine(_tempDirectory, "dns-adblock.txt");
        File.WriteAllLines(path, ["||ads.example.com^", "||tracker.example.com^", "@@||allowed.example.com^"]);

        CompilerConfigWizardHelpers.InferLocalSourceEngine(path).Should().Be("dns");
    }

    [Fact]
    public void InferLocalSourceEngine_WithCosmeticRules_ReturnsBrowser()
    {
        var path = Path.Combine(_tempDirectory, "cosmetic.txt");
        File.WriteAllLines(path, ["##.ad-banner", "example.com#@#.allowed-ad", "##div[id^=\"ad-\"]"]);

        CompilerConfigWizardHelpers.InferLocalSourceEngine(path).Should().Be("browser");
    }

    [Fact]
    public void InferLocalSourceEngine_WithBrowserOnlyModifiers_ReturnsBrowser()
    {
        var path = Path.Combine(_tempDirectory, "browser-modifiers.txt");
        File.WriteAllLines(path, ["||ads.example.com^$script,csp=default-src 'self'", "||ads.example.com^$elemhide"]);

        CompilerConfigWizardHelpers.InferLocalSourceEngine(path).Should().Be("browser");
    }

    [Fact]
    public void InferLocalSourceEngine_WithNoClassifiableLines_FallsBackToDns()
    {
        var path = Path.Combine(_tempDirectory, "unclassifiable.txt");
        File.WriteAllLines(path, ["! just a comment", ""]);

        CompilerConfigWizardHelpers.InferLocalSourceEngine(path).Should().Be("dns");
    }

    [Theory]
    [InlineData("https://example.com/filters/easylist.txt", "easylist")]
    [InlineData("https://example.com/", "example.com")]
    [InlineData("/local/path/my-rules.txt", "my-rules")]
    public void DefaultSourceName_DerivesFromPathOrUrl(string source, string expected)
    {
        CompilerConfigWizardHelpers.DefaultSourceName(source).Should().Be(expected);
    }
}
