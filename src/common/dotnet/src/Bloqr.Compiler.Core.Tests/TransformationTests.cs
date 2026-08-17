namespace Bloqr.Compiler.Core.Tests;

public class TransformationTests
{
    [Theory]
    [InlineData("RemoveComments")]
    [InlineData("Compress")]
    [InlineData("RemoveModifiers")]
    [InlineData("Validate")]
    [InlineData("ValidateAllowIp")]
    [InlineData("Deduplicate")]
    [InlineData("InvertAllow")]
    [InlineData("RemoveEmptyLines")]
    [InlineData("TrimLines")]
    [InlineData("InsertFinalNewLine")]
    [InlineData("ConvertToAscii")]
    public void TransformationHelper_IsValid_ReturnsTrue_ForValidTransformations(string transformation)
    {
        Assert.True(TransformationHelper.IsValid(transformation));
    }

    [Theory]
    [InlineData("removecomments")] // case-insensitive
    [InlineData("DEDUPLICATE")]
    [InlineData("ValidateAllowIp")]
    public void TransformationHelper_IsValid_CaseInsensitive(string transformation)
    {
        Assert.True(TransformationHelper.IsValid(transformation));
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("NotATransformation")]
    [InlineData("")]
    public void TransformationHelper_IsValid_ReturnsFalse_ForInvalidTransformations(string transformation)
    {
        Assert.False(TransformationHelper.IsValid(transformation));
    }

    [Fact]
    public void TransformationHelper_AllTransformations_ContainsAllValues()
    {
        Assert.Equal(11, TransformationHelper.AllTransformations.Count);
    }

    [Fact]
    public void TransformationHelper_RecommendedTransformations_ContainsCommonDefaults()
    {
        Assert.Contains("Validate", TransformationHelper.RecommendedTransformations);
        Assert.Contains("Deduplicate", TransformationHelper.RecommendedTransformations);
        Assert.Contains("InsertFinalNewLine", TransformationHelper.RecommendedTransformations);
    }

    [Fact]
    public void TransformationHelper_GetInvalidTransformations_ReturnsInvalid()
    {
        // Arrange
        var transformations = new[] { "Validate", "Invalid", "Deduplicate", "BadOne" };

        // Act
        var invalid = TransformationHelper.GetInvalidTransformations(transformations);

        // Assert
        Assert.Equal(2, invalid.Count);
        Assert.Contains("Invalid", invalid);
        Assert.Contains("BadOne", invalid);
    }

    [Fact]
    public void TransformationHelper_TryParse_ReturnsTrue_ForValid()
    {
        Assert.True(TransformationHelper.TryParse("Deduplicate", out var result));
        Assert.Equal(Transformation.Deduplicate, result);
    }
}