namespace RulesCompiler.Tests;

public class ConfigurationReaderTests
{
    private readonly Mock<ILogger<ConfigurationReader>> _loggerMock;
    private readonly ConfigurationReader _reader;

    public ConfigurationReaderTests()
    {
        _loggerMock = new Mock<ILogger<ConfigurationReader>>();
        _reader = new ConfigurationReader(_loggerMock.Object);
    }

    [Theory]
    [InlineData("config.json", ConfigurationFormat.Json)]
    [InlineData("config.jsonc", ConfigurationFormat.Json)]
    [InlineData("config.yaml", ConfigurationFormat.Yaml)]
    [InlineData("config.yml", ConfigurationFormat.Yaml)]
    [InlineData("config.toml", ConfigurationFormat.Toml)]
    [InlineData("path/to/config.JSON", ConfigurationFormat.Json)]
    [InlineData("path/to/config.JSONC", ConfigurationFormat.Json)]
    [InlineData("path/to/config.YAML", ConfigurationFormat.Yaml)]
    public void DetectFormat_ReturnsCorrectFormat(string filePath, ConfigurationFormat expected)
    {
        var result = _reader.DetectFormat(filePath);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("config.txt")]
    [InlineData("config")]
    [InlineData("config.xml")]
    public void DetectFormat_ThrowsForUnsupportedExtensions(string filePath)
    {
        Assert.Throws<ArgumentException>(() => _reader.DetectFormat(filePath));
    }

    [Fact]
    public async Task ReadConfigurationAsync_ThrowsForMissingFile()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _reader.ReadConfigurationAsync("/nonexistent/path/config.json"));
    }

    [Fact]
    public async Task ReadConfigurationAsync_ThrowsForNullPath()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _reader.ReadConfigurationAsync(null!));
    }

    [Fact]
    public async Task ReadConfigurationAsync_ThrowsForEmptyPath()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _reader.ReadConfigurationAsync(string.Empty));
    }

    [Fact]
    public async Task ReadConfigurationAsync_ParsesJsonConfig()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var newPath = Path.ChangeExtension(tempFile, ".json");
        File.Move(tempFile, newPath);

        try
        {
            var jsonContent = @"{
                ""name"": ""Test Rules"",
                ""version"": ""1.0.0"",
                ""description"": ""Test description"",
                ""homepage"": ""https://example.com"",
                ""license"": ""MIT"",
                ""sources"": [
                    {
                        ""name"": ""Local"",
                        ""source"": ""./rules.txt"",
                        ""type"": ""adblock""
                    }
                ],
                ""transformations"": [""Deduplicate"", ""Validate""]
            }";
            await File.WriteAllTextAsync(newPath, jsonContent);

            // Act
            var config = await _reader.ReadConfigurationAsync(newPath);

            // Assert
            Assert.Equal("Test Rules", config.Name);
            Assert.Equal("1.0.0", config.Version);
            Assert.Equal("Test description", config.Description);
            Assert.Single(config.Sources);
            Assert.Equal("Local", config.Sources[0].Name);
            Assert.Equal(2, config.Transformations.Count);
        }
        finally
        {
            if (File.Exists(newPath))
                File.Delete(newPath);
        }
    }

    [Fact]
    public async Task ReadConfigurationAsync_ParsesJsoncWithCommentsAndTrailingCommas()
    {
        // Regression test: the Dashboard's compiler-config wizard (#268) writes .jsonc files,
        // and DetectFormat previously had no mapping for that extension at all (threw
        // ArgumentException), while the JSON parser separately couldn't tolerate comments or
        // trailing commas even for a plain .json file. Both are needed for #269's round-trip
        // editing to actually work.
        var tempFile = Path.GetTempFileName();
        var newPath = Path.ChangeExtension(tempFile, ".jsonc");
        File.Move(tempFile, newPath);

        try
        {
            var jsoncContent = """
                {
                  // Filter list name (required).
                  "name": "Test Rules",
                  "sources": [
                    {
                      "source": "./rules.txt",
                      "type": "adblock",
                    },
                  ],
                }
                """;
            await File.WriteAllTextAsync(newPath, jsoncContent);

            var config = await _reader.ReadConfigurationAsync(newPath);

            Assert.Equal("Test Rules", config.Name);
            Assert.Single(config.Sources);
            Assert.Equal("./rules.txt", config.Sources[0].Source);
        }
        finally
        {
            if (File.Exists(newPath))
                File.Delete(newPath);
        }
    }

    [Fact]
    public async Task ReadConfigurationAsync_ParsesJsonWithAllNewProperties()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var newPath = Path.ChangeExtension(tempFile, ".json");
        File.Move(tempFile, newPath);

        try
        {
            var jsonContent = @"{
                ""name"": ""Full Feature Test"",
                ""version"": ""2.0.0"",
                ""sources"": [
                    {
                        ""name"": ""Source1"",
                        ""source"": ""./rules.txt"",
                        ""type"": ""adblock"",
                        ""transformations"": [""RemoveComments""],
                        ""inclusions"": [""*.example.com""],
                        ""exclusions"": [""*.google.com""],
                        ""inclusions_sources"": [""include-patterns.txt""],
                        ""exclusions_sources"": [""exclude-patterns.txt""]
                    }
                ],
                ""transformations"": [""Deduplicate"", ""Validate""],
                ""inclusions"": [""pattern1"", ""pattern2""],
                ""exclusions"": [""/regex/""],
                ""inclusions_sources"": [""global-includes.txt""],
                ""exclusions_sources"": [""global-excludes.txt""]
            }";
            await File.WriteAllTextAsync(newPath, jsonContent);

            // Act
            var config = await _reader.ReadConfigurationAsync(newPath);

            // Assert
            Assert.Equal("Full Feature Test", config.Name);
            Assert.Equal(2, config.Inclusions.Count);
            Assert.Single(config.Exclusions);
            Assert.Single(config.InclusionsSources);
            Assert.Single(config.ExclusionsSources);

            // Source-level properties
            var source = config.Sources[0];
            Assert.Single(source.Transformations);
            Assert.Equal("RemoveComments", source.Transformations[0]);
            Assert.Single(source.Inclusions);
            Assert.Single(source.Exclusions);
            Assert.Single(source.InclusionsSources);
            Assert.Single(source.ExclusionsSources);
        }
        finally
        {
            if (File.Exists(newPath))
                File.Delete(newPath);
        }
    }

    [Fact]
    public async Task ReadConfigurationAsync_ParsesYamlConfig()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var newPath = Path.ChangeExtension(tempFile, ".yaml");
        File.Move(tempFile, newPath);

        try
        {
            var yamlContent = @"
name: Test Rules YAML
version: 2.0.0
description: YAML test
homepage: https://yaml.example.com
license: GPLv3
sources:
  - name: Remote
    source: https://example.com/rules.txt
    type: adblock
transformations:
  - Deduplicate
  - Compress
";
            await File.WriteAllTextAsync(newPath, yamlContent);

            // Act
            var config = await _reader.ReadConfigurationAsync(newPath);

            // Assert
            Assert.Equal("Test Rules YAML", config.Name);
            Assert.Equal("2.0.0", config.Version);
            Assert.Single(config.Sources);
            Assert.Equal("Remote", config.Sources[0].Name);
            Assert.Equal(2, config.Transformations.Count);
        }
        finally
        {
            if (File.Exists(newPath))
                File.Delete(newPath);
        }
    }

    [Fact]
    public async Task ReadConfigurationAsync_ParsesYamlWithFormatOverride()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();

        try
        {
            var yamlContent = @"
name: Format Override Test
version: 3.0.0
sources: []
transformations: []
";
            await File.WriteAllTextAsync(tempFile, yamlContent);

            // Act - file has .tmp extension but we specify YAML format
            var config = await _reader.ReadConfigurationAsync(tempFile, ConfigurationFormat.Yaml);

            // Assert
            Assert.Equal("Format Override Test", config.Name);
            Assert.Equal("3.0.0", config.Version);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ToJson_SerializesConfiguration()
    {
        // Arrange
        var config = new CompilerConfiguration
        {
            Name = "Test",
            Version = "1.0.0",
            Sources =
            [
                new FilterSource { Name = "Source1", Source = "./test.txt", Type = "adblock" }
            ],
            Transformations = ["Deduplicate"]
        };

        // Act
        var json = _reader.ToJson(config);

        // Assert
        Assert.Contains("\"name\": \"Test\"", json);
        Assert.Contains("\"version\": \"1.0.0\"", json);
        Assert.Contains("\"sources\":", json);
    }
}
