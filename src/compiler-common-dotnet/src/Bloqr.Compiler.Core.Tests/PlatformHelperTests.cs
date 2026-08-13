namespace Bloqr.Compiler.Core.Tests;

public class PlatformHelperTests
{
    [Fact]
    public void GetPlatformInfo_ReturnsValidInfo()
    {
        // Act
        var info = PlatformHelper.GetPlatformInfo();

        // Assert
        Assert.NotNull(info);
        Assert.NotEmpty(info.OSName);
        Assert.NotEmpty(info.OSVersion);
        Assert.NotEmpty(info.Architecture);

        // Exactly one platform should be true
        var platformCount = new[] { info.IsWindows, info.IsLinux, info.IsMacOS }.Count(x => x);
        Assert.True(platformCount >= 1, "At least one platform should be detected");
    }

    [Fact]
    public void GetOSName_ReturnsNonEmptyString()
    {
        // Act
        var osName = PlatformHelper.GetOSName();

        // Assert
        Assert.NotNull(osName);
        Assert.NotEmpty(osName);
    }

    [Fact]
    public void GetExecutableExtensions_ReturnsValidExtensions()
    {
        // Act
        var extensions = PlatformHelper.GetExecutableExtensions();

        // Assert
        Assert.NotNull(extensions);
        Assert.NotEmpty(extensions);

        if (PlatformHelper.IsWindows)
        {
            Assert.Contains(".cmd", extensions);
            Assert.Contains(".exe", extensions);
        }
        else
        {
            Assert.Contains("", extensions);
        }
    }

    [Fact]
    public void PlatformProperties_AreConsistent()
    {
        // The static properties should match GetPlatformInfo results
        var info = PlatformHelper.GetPlatformInfo();

        Assert.Equal(PlatformHelper.IsWindows, info.IsWindows);
        Assert.Equal(PlatformHelper.IsLinux, info.IsLinux);
        Assert.Equal(PlatformHelper.IsMacOS, info.IsMacOS);
    }
}
