namespace Bloqr.Dashboard.Tests;

public sealed class CompilerConfigSchemaValidatorTests
{
    private readonly CompilerConfigSchemaValidator _validator = new();

    [Fact]
    public void Validate_WithMinimalValidConfig_Succeeds()
    {
        var config = new CompilerConfiguration
        {
            Name = "Test List",
            Sources = [new FilterSource { Source = "https://example.com/list.txt" }],
        };

        var result = _validator.Validate(config);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithMissingName_Fails()
    {
        var config = new CompilerConfiguration
        {
            Sources = [new FilterSource { Source = "https://example.com/list.txt" }],
        };

        var result = _validator.Validate(config);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithNoSources_Fails()
    {
        var config = new CompilerConfiguration { Name = "Test List" };

        var result = _validator.Validate(config);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithFullOutputHashVerificationAndArchivingBlocks_Succeeds()
    {
        var config = new CompilerConfiguration
        {
            Name = "Test List",
            Sources = [new FilterSource { Source = "local.txt" }],
            Output = new OutputSettings { Path = "output.txt", ConflictStrategy = "rename" },
            HashVerification = new HashVerificationSettings
            {
                Mode = "warning",
                RequireHashesForRemote = false,
                FailOnMismatch = false,
                HashDatabasePath = ".hashes.json",
            },
            Archiving = new ArchivingSettings { Enabled = true, Mode = "automatic", RetentionDays = 90 },
        };

        var result = _validator.Validate(config);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInvalidConflictStrategy_Fails()
    {
        var config = new CompilerConfiguration
        {
            Name = "Test List",
            Sources = [new FilterSource { Source = "local.txt" }],
            Output = new OutputSettings { Path = "output.txt", ConflictStrategy = "delete" },
        };

        var result = _validator.Validate(config);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_SourceWithDefaultInclusionsAndExclusionsSources_Succeeds()
    {
        // Regression test: FilterSource always serializes inclusions_sources/exclusions_sources
        // (as [] when unset, since they're non-nullable List<string> properties) - the schema
        // must declare these per-source properties or every source the wizard emits would fail
        // schema validation under additionalProperties: false.
        var config = new CompilerConfiguration
        {
            Name = "Test List",
            Sources = [new FilterSource { Source = "local.txt" }],
        };

        var result = _validator.Validate(config);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_SourceWithInclusionsAndExclusionsSourcesPopulated_Succeeds()
    {
        var config = new CompilerConfiguration
        {
            Name = "Test List",
            Sources =
            [
                new FilterSource
                {
                    Source = "local.txt",
                    InclusionsSources = ["patterns/include.txt"],
                    ExclusionsSources = ["patterns/exclude.txt"],
                },
            ],
        };

        var result = _validator.Validate(config);

        result.IsValid.Should().BeTrue();
    }
}
