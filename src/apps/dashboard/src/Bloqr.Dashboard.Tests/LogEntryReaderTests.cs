using Serilog;
using Serilog.Events;

namespace Bloqr.Dashboard.Tests;

public sealed class LogEntryReaderTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly DashboardPaths _paths;

    public LogEntryReaderTests()
    {
        _rootDirectory = Directory.CreateTempSubdirectory("bloqr-dashboard-tests-").FullName;
        _paths = DashboardPaths.ForDirectory(_rootDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ReadAsync_ParsesEntriesWrittenByTheSharedStructuredFormatter()
    {
        WriteLogEvents();

        var reader = new LogEntryReader(_paths);
        var entries = await reader.ReadAsync(new LogEntryFilter());

        entries.Should().HaveCount(3);
        entries.Should().Contain(e => e.Message == "First message" && e.Level == "Information");
        entries.Should().Contain(e => e.Message == "Second message" && e.Level == "Warning");
        entries.Should().Contain(e => e.Level == "Error" && e.Exception != null);
    }

    [Fact]
    public async Task ReadAsync_WithMinimumLevelFilter_ExcludesLowerSeverityEntries()
    {
        WriteLogEvents();

        var reader = new LogEntryReader(_paths);
        var entries = await reader.ReadAsync(new LogEntryFilter { MinimumLevel = "Warning" });

        entries.Should().HaveCount(2);
        entries.Should().NotContain(e => e.Level == "Information");
    }

    [Fact]
    public async Task ReadAsync_WithTail_ReturnsOnlyMostRecentEntries()
    {
        WriteLogEvents();

        var reader = new LogEntryReader(_paths);
        var entries = await reader.ReadAsync(new LogEntryFilter { Tail = 1 });

        entries.Should().HaveCount(1);
    }

    [Fact]
    public void ListLogFiles_WhenDirectoryMissing_ReturnsEmpty()
    {
        var reader = new LogEntryReader(_paths);

        reader.ListLogFiles().Should().BeEmpty();
    }

    private void WriteLogEvents()
    {
        Directory.CreateDirectory(_paths.LogDirectory);
        var logPath = Path.Combine(_paths.LogDirectory, "bloqr-dashboard-.jsonl");

        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(new StructuredJsonLogFormatter(), logPath, shared: true)
            .CreateLogger();

        logger.Information("First message");
        logger.Warning("Second message");
        logger.Error(new InvalidOperationException("boom"), "Third message");
    }
}
