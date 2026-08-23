namespace Bloqr.Compiler.Dotnet.Tests;

public class CommandLineArgumentHelperTests
{
    [Fact]
    public void SplitBareBooleanFlags_BareFlagFollowedByKeyValuePair_DoesNotSwallowTheNextFlag()
    {
        // Regression test for #426: a bare boolean flag immediately followed by another
        // "--key value" pair used to corrupt IConfiguration's command-line parsing, because
        // CommandLineConfigurationProvider always treats the token after "--key" as its value.
        var (flags, remaining) = CommandLineArgumentHelper.SplitBareBooleanFlags(
            ["--verbose", "--config", "x.json"]);

        Assert.True(flags.ContainsKey("verbose"));
        Assert.Equal(["--config", "x.json"], remaining);
    }

    [Fact]
    public void SplitBareBooleanFlags_BenchmarkFlagGroup_LeavesValueFlagsIntact()
    {
        var (flags, remaining) = CommandLineArgumentHelper.SplitBareBooleanFlags(
            ["--benchmark", "--benchmark-size", "small", "--benchmark-json"]);

        Assert.True(flags.ContainsKey("benchmark"));
        Assert.True(flags.ContainsKey("benchmark-json"));
        Assert.Equal(["--benchmark-size", "small"], remaining);
    }

    [Fact]
    public void SplitBareBooleanFlags_MultipleBareFlagsInARow_AllStripped()
    {
        var (flags, remaining) = CommandLineArgumentHelper.SplitBareBooleanFlags(
            ["--copy", "--verbose", "--config", "x.json", "--output", "out.txt"]);

        Assert.True(flags.ContainsKey("copy"));
        Assert.True(flags.ContainsKey("verbose"));
        Assert.Equal(["--config", "x.json", "--output", "out.txt"], remaining);
    }

    [Fact]
    public void SplitBareBooleanFlags_NoBareFlags_ReturnsArgsUnchanged()
    {
        var (flags, remaining) = CommandLineArgumentHelper.SplitBareBooleanFlags(
            ["--config", "x.json", "--output", "out.txt"]);

        Assert.Empty(flags);
        Assert.Equal(["--config", "x.json", "--output", "out.txt"], remaining);
    }

    [Fact]
    public void SplitBareBooleanFlags_IsCaseInsensitive()
    {
        var (flags, remaining) = CommandLineArgumentHelper.SplitBareBooleanFlags(["--VERBOSE"]);

        Assert.True(flags.ContainsKey("verbose"));
        Assert.Empty(remaining);
    }

    [Fact]
    public void SplitBareBooleanFlags_ValueOnlyEqualsSyntax_IsLeftForAddCommandLine()
    {
        // "--verbose=true" is already unambiguous to CommandLineConfigurationProvider on its
        // own, so the pre-scan should leave it alone rather than trying to match it.
        var (flags, remaining) = CommandLineArgumentHelper.SplitBareBooleanFlags(["--verbose=true"]);

        Assert.Empty(flags);
        Assert.Equal(["--verbose=true"], remaining);
    }
}
