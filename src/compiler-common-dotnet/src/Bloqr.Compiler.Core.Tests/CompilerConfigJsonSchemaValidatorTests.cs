namespace Bloqr.Compiler.Core.Tests;

public class CompilerConfigJsonSchemaValidatorTests
{
    [Fact]
    public void Validate_AddsNoErrors_ForASchemaValidConfiguration()
    {
        var config = new CompilerConfiguration
        {
            Name = "Test",
            Version = "1.2.3",
            Sources = [new FilterSource { Source = "test.txt", Type = "adblock" }]
        };

        var result = new ValidationResult();
        CompilerConfigJsonSchemaValidator.Validate(config, result);

        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_AddsError_WhenVersionIsNotStrictSemver()
    {
        // The schema enforces a strict "int.int.int" semver pattern (the epic's own requirement -
        // no "v1" or "v2.4.beta"), but ConfigurationValidator's hand-written business rules never
        // check version format at all. This is exactly the kind of gap schema validation closes.
        var config = new CompilerConfiguration
        {
            Name = "Test",
            Version = "v1",
            Sources = [new FilterSource { Source = "test.txt" }]
        };

        var result = new ValidationResult();
        CompilerConfigJsonSchemaValidator.Validate(config, result);

        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Validate_AddsError_WhenNameIsMissing()
    {
        var config = new CompilerConfiguration
        {
            Name = "",
            Sources = [new FilterSource { Source = "test.txt" }]
        };

        var result = new ValidationResult();
        CompilerConfigJsonSchemaValidator.Validate(config, result);

        Assert.NotEmpty(result.Errors);
    }
}
