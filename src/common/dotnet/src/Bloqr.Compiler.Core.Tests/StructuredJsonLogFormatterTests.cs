using System.Text.Json;
using Serilog;

namespace Bloqr.Compiler.Core.Tests;

public sealed class StructuredJsonLogFormatterTests : IDisposable
{
    private readonly string _tempDirectory;

    public StructuredJsonLogFormatterTests()
    {
        _tempDirectory = Directory.CreateTempSubdirectory("structured-json-formatter-tests-").FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Format_WritesTimestampLevelMessageTemplateAndRenderedMessage()
    {
        var line = WriteSingleLogLine(logger => logger.Information("Hello {Name}", "world"));

        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;

        Assert.False(string.IsNullOrEmpty(root.GetProperty("timestamp").GetString()));
        Assert.Equal("Information", root.GetProperty("level").GetString());
        Assert.Equal("Hello {Name}", root.GetProperty("messageTemplate").GetString());
        Assert.Equal("Hello \"world\"", root.GetProperty("message").GetString());
    }

    [Fact]
    public void Format_WithException_IncludesExceptionField()
    {
        var line = WriteSingleLogLine(
            logger => logger.Error(new InvalidOperationException("boom"), "Something broke"));

        using var document = JsonDocument.Parse(line);
        var exceptionText = document.RootElement.GetProperty("exception").GetString() ?? string.Empty;

        Assert.Contains("InvalidOperationException", exceptionText);
        Assert.Contains("boom", exceptionText);
    }

    [Fact]
    public void Format_WithApplicationEnrichment_IncludesApplicationField()
    {
        var logPath = Path.Combine(_tempDirectory, "app.jsonl");
        using (var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.WithProperty("Application", "bloqr-compiler-dotnet")
            .WriteTo.File(new StructuredJsonLogFormatter(), logPath)
            .CreateLogger())
        {
            logger.Information("Started");
        }

        var line = File.ReadAllLines(logPath).First(l => !string.IsNullOrWhiteSpace(l));
        using var document = JsonDocument.Parse(line);

        Assert.Equal("bloqr-compiler-dotnet", document.RootElement.GetProperty("application").GetString());
    }

    private string WriteSingleLogLine(Action<Serilog.ILogger> log)
    {
        var logPath = Path.Combine(_tempDirectory, "test.jsonl");
        using (var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(new StructuredJsonLogFormatter(), logPath)
            .CreateLogger())
        {
            log(logger);
        }

        return File.ReadAllLines(logPath).First(l => !string.IsNullOrWhiteSpace(l));
    }
}
