namespace Bloqr.Compiler.Core.Tests;

public class ModelsTests
{
    [Fact]
    public void CompilerConfiguration_HasDefaultValues()
    {
        // Act
        var config = new CompilerConfiguration();

        // Assert
        Assert.NotNull(config.Name);
        Assert.NotNull(config.Sources);
        Assert.NotNull(config.Transformations);
        Assert.NotNull(config.Inclusions);
        Assert.NotNull(config.Exclusions);
        Assert.NotNull(config.InclusionsSources);
        Assert.NotNull(config.ExclusionsSources);
        Assert.Empty(config.Sources);
        Assert.Empty(config.Transformations);
        Assert.Null(config.Description);
        Assert.Null(config.Version);
        Assert.Null(config.License);
        Assert.Null(config.Homepage);
    }

    [Fact]
    public void FilterSource_HasDefaultValues()
    {
        // Act
        var source = new FilterSource();

        // Assert
        Assert.Null(source.Name); // Name is now optional
        Assert.NotNull(source.Source);
        Assert.Equal("adblock", source.Type);
        Assert.NotNull(source.Inclusions);
        Assert.NotNull(source.Exclusions);
        Assert.NotNull(source.InclusionsSources);
        Assert.NotNull(source.ExclusionsSources);
        Assert.NotNull(source.Transformations);
        Assert.Empty(source.Transformations);
    }

    [Fact]
    public void CompilerResult_HasDefaultValues()
    {
        // Act
        var result = new CompilerResult();

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ConfigName);
        Assert.NotNull(result.OutputPath);
        Assert.NotNull(result.StandardOutput);
        Assert.NotNull(result.StandardError);
        Assert.Equal(0, result.RuleCount);
        Assert.Null(result.ConfigVersion);
    }

    [Fact]
    public void VersionInfo_HasDefaultValues()
    {
        // Act
        var info = new VersionInfo();

        // Assert
        Assert.NotNull(info.ModuleVersion);
        Assert.NotNull(info.DotNetVersion);
        Assert.NotNull(info.Platform);
    }

    [Fact]
    public void PlatformInfo_HasDefaultValues()
    {
        // Act
        var info = new PlatformInfo();

        // Assert
        Assert.NotNull(info.OSName);
        Assert.NotNull(info.OSVersion);
        Assert.NotNull(info.Architecture);
        Assert.False(info.IsWindows);
        Assert.False(info.IsLinux);
        Assert.False(info.IsMacOS);
    }

    [Theory]
    [InlineData(ConfigurationFormat.Json)]
    [InlineData(ConfigurationFormat.Yaml)]
    [InlineData(ConfigurationFormat.Toml)]
    public void ConfigurationFormat_HasExpectedValues(ConfigurationFormat format)
    {
        // Just verify the enum values exist and can be used
        Assert.True(Enum.IsDefined(typeof(ConfigurationFormat), format));
    }

    [Fact]
    public void CompilerOptions_HasDefaultValues()
    {
        // Act
        var options = CompilerOptions.Default;

        // Assert
        Assert.True(options.ValidateConfig);
        Assert.False(options.Verbose);
        Assert.False(options.CopyToRules);
        Assert.False(options.FailOnWarnings);
        Assert.Null(options.ConfigPath);
        Assert.Null(options.OutputPath);
    }
}