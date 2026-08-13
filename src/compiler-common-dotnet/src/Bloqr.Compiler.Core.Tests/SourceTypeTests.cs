namespace Bloqr.Compiler.Core.Tests;

public class SourceTypeTests
{
    [Theory]
    [InlineData("adblock")]
    [InlineData("hosts")]
    [InlineData("ADBLOCK")]
    [InlineData("HOSTS")]
    public void SourceTypeHelper_IsValid_ReturnsTrue_ForValidTypes(string sourceType)
    {
        Assert.True(SourceTypeHelper.IsValid(sourceType));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("other")]
    [InlineData("")]
    public void SourceTypeHelper_IsValid_ReturnsFalse_ForInvalidTypes(string sourceType)
    {
        Assert.False(SourceTypeHelper.IsValid(sourceType));
    }
}