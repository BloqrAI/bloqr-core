namespace Bloqr.Dashboard.Tests;

public sealed class CompilerConfigJsoncWriterTests
{
    private static readonly JsonSerializerOptions JsoncTolerantOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };


    [Fact]
    public void Write_ProducesJsonc_ThatRoundTripsThroughAJsonDeserializer()
    {
        var configuration = new CompilerConfiguration
        {
            Name = "Test Filter List",
            Description = "A test list",
            Homepage = "https://example.com",
            License = "MIT",
            Version = "1.2.3",
            Output = new OutputSettings { Path = "output/test.txt", ConflictStrategy = "overwrite" },
            HashVerification = new HashVerificationSettings
            {
                Mode = "strict",
                RequireHashesForRemote = true,
                FailOnMismatch = true,
                HashDatabasePath = "output/.hashes.json",
            },
            Archiving = new ArchivingSettings { Enabled = true, Mode = "automatic", RetentionDays = 30 },
            Sources =
            [
                new FilterSource
                {
                    Name = "Local Source",
                    Source = "local.txt",
                    Type = "adblock",
                    Transformations = ["Deduplicate"],
                    Inclusions = ["*.example.com"],
                },
            ],
            Transformations = ["Validate", "Deduplicate"],
            Inclusions = ["*.ads.example.com"],
            InclusionsSources = ["patterns/include.txt"],
            Exclusions = ["*.safe.example.com"],
            ExclusionsSources = ["patterns/exclude.txt"],
        };

        var jsonc = CompilerConfigJsoncWriter.Write(configuration);
        var reparsed = JsonSerializer.Deserialize<CompilerConfiguration>(jsonc, JsoncTolerantOptions);

        reparsed.Should().NotBeNull();
        reparsed!.Name.Should().Be("Test Filter List");
        reparsed.Description.Should().Be("A test list");
        reparsed.Homepage.Should().Be("https://example.com");
        reparsed.License.Should().Be("MIT");
        reparsed.Version.Should().Be("1.2.3");

        reparsed.Output.Should().NotBeNull();
        reparsed.Output!.Path.Should().Be("output/test.txt");
        reparsed.Output.ConflictStrategy.Should().Be("overwrite");

        reparsed.HashVerification.Should().NotBeNull();
        reparsed.HashVerification!.Mode.Should().Be("strict");
        reparsed.HashVerification.RequireHashesForRemote.Should().BeTrue();
        reparsed.HashVerification.FailOnMismatch.Should().BeTrue();
        reparsed.HashVerification.HashDatabasePath.Should().Be("output/.hashes.json");

        reparsed.Archiving.Should().NotBeNull();
        reparsed.Archiving!.Enabled.Should().BeTrue();
        reparsed.Archiving.RetentionDays.Should().Be(30);

        reparsed.Sources.Should().HaveCount(1);
        reparsed.Sources[0].Name.Should().Be("Local Source");
        reparsed.Sources[0].Source.Should().Be("local.txt");
        reparsed.Sources[0].Transformations.Should().Equal("Deduplicate");
        reparsed.Sources[0].Inclusions.Should().Equal("*.example.com");

        reparsed.Transformations.Should().Equal("Validate", "Deduplicate");
        reparsed.Inclusions.Should().Equal("*.ads.example.com");
        reparsed.InclusionsSources.Should().Equal("patterns/include.txt");
        reparsed.Exclusions.Should().Equal("*.safe.example.com");
        reparsed.ExclusionsSources.Should().Equal("patterns/exclude.txt");
    }

    [Fact]
    public void Write_WithNoOptionalBlocksSet_OmitsThemEntirely()
    {
        var configuration = new CompilerConfiguration
        {
            Name = "Minimal List",
            Sources = [new FilterSource { Source = "local.txt" }],
        };

        var jsonc = CompilerConfigJsoncWriter.Write(configuration);

        jsonc.Should().NotContain("\"output\"");
        jsonc.Should().NotContain("\"hashVerification\"");
        jsonc.Should().NotContain("\"archiving\"");

        var reparsed = JsonSerializer.Deserialize<CompilerConfiguration>(jsonc, JsoncTolerantOptions);
        reparsed.Should().NotBeNull();
        reparsed!.Output.Should().BeNull();
        reparsed.HashVerification.Should().BeNull();
        reparsed.Archiving.Should().BeNull();
    }

    [Fact]
    public void Write_WithMixedEngineConfig_EmitsDefaultEngineAndPerSourceEngine_ThatRoundTrips()
    {
        var configuration = new CompilerConfiguration
        {
            Name = "Mixed Engine List",
            DefaultEngine = "dns",
            Sources =
            [
                new FilterSource { Name = "Dns Source", Source = "dns.txt", Type = "hosts", Engine = "dns" },
                new FilterSource { Name = "Browser Source", Source = "browser.txt", Type = "adblock", Engine = "browser" },
                new FilterSource { Name = "Auto Source", Source = "auto.txt", Type = "adblock" },
            ],
        };

        var jsonc = CompilerConfigJsoncWriter.Write(configuration);

        jsonc.Should().Contain("\"defaultEngine\": \"dns\"");
        var reparsed = JsonSerializer.Deserialize<CompilerConfiguration>(jsonc, JsoncTolerantOptions);

        reparsed.Should().NotBeNull();
        reparsed!.DefaultEngine.Should().Be("dns");
        reparsed.Sources.Should().HaveCount(3);
        reparsed.Sources[0].Engine.Should().Be("dns");
        reparsed.Sources[1].Engine.Should().Be("browser");
        reparsed.Sources[2].Engine.Should().BeNull();
    }

    [Fact]
    public void Write_WithNoEngineFieldsSet_OmitsThemEntirely()
    {
        var configuration = new CompilerConfiguration
        {
            Name = "No Engine Fields",
            Sources = [new FilterSource { Source = "local.txt" }],
        };

        var jsonc = CompilerConfigJsoncWriter.Write(configuration);

        jsonc.Should().NotContain("\"defaultEngine\"");
        jsonc.Should().NotContain("\"engine\"");
    }

    [Fact]
    public void Write_WithEmptySources_ProducesAnEmptyArray()
    {
        var configuration = new CompilerConfiguration { Name = "Empty" };

        var jsonc = CompilerConfigJsoncWriter.Write(configuration);
        var reparsed = JsonSerializer.Deserialize<CompilerConfiguration>(jsonc, JsoncTolerantOptions);

        reparsed!.Sources.Should().BeEmpty();
    }
}
