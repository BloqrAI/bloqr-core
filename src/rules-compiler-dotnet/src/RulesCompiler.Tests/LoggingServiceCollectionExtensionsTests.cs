using Serilog.Events;

namespace RulesCompiler.Tests;

public sealed class LoggingServiceCollectionExtensionsTests : IDisposable
{
    private readonly string _tempDirectory;

    public LoggingServiceCollectionExtensionsTests()
    {
        _tempDirectory = Directory.CreateTempSubdirectory("rules-compiler-logging-tests-").FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateStructuredFileLogger_WithNullMinimumLevel_ReturnsNull()
    {
        var logger = LoggingServiceCollectionExtensions.CreateStructuredFileLogger(
            "test-app",
            _tempDirectory,
            minimumLevel: null);

        Assert.Null(logger);
    }

    [Fact]
    public void CreateStructuredFileLogger_WritesToExpectedFileNamingConvention()
    {
        var logger = LoggingServiceCollectionExtensions.CreateStructuredFileLogger(
            "test-app",
            _tempDirectory,
            LogEventLevel.Information);

        Assert.NotNull(logger);
        logger.Information("Hello from the test app");
        (logger as IDisposable)?.Dispose();

        var files = Directory.GetFiles(_tempDirectory, "test-app-*.jsonl");
        Assert.Single(files);

        var line = File.ReadAllLines(files[0]).First(l => !string.IsNullOrWhiteSpace(l));
        using var document = System.Text.Json.JsonDocument.Parse(line);
        Assert.Equal("test-app", document.RootElement.GetProperty("application").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateStructuredFileLogger_WithBlankApplicationName_Throws(string applicationName)
    {
        Assert.Throws<ArgumentException>(() =>
            LoggingServiceCollectionExtensions.CreateStructuredFileLogger(
                applicationName,
                _tempDirectory,
                LogEventLevel.Information));
    }
}
