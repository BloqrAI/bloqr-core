namespace Bloqr.Compiler.Core.Tests;

public sealed class OutputPublisherTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly OutputPublisher _publisher;

    public OutputPublisherTests()
    {
        _tempDirectory = Directory.CreateTempSubdirectory("output-publisher-tests-").FullName;
        _publisher = new OutputPublisher(new Mock<ILogger<OutputPublisher>>().Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task PublishAsync_WithNoDestinationConflict_CopiesToDestination()
    {
        var compiled = WriteFile("compiled.txt", "v1");
        var destination = Path.Combine(_tempDirectory, "published", "output.txt");
        var output = new OutputSettings { Path = destination };

        var result = await _publisher.PublishAsync(compiled, output, archiving: null);

        Assert.True(result.Success);
        Assert.Equal(destination, result.FinalPath);
        Assert.Equal("v1", await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public async Task PublishAsync_WithEmptyPath_ReturnsCompiledPathUnchanged()
    {
        var compiled = WriteFile("compiled.txt", "v1");
        var output = new OutputSettings { Path = null };

        var result = await _publisher.PublishAsync(compiled, output, archiving: null);

        Assert.True(result.Success);
        Assert.Equal(compiled, result.FinalPath);
    }

    [Fact]
    public async Task PublishAsync_WithErrorStrategy_FailsWithoutTouchingExistingFile()
    {
        var compiled = WriteFile("compiled.txt", "new");
        var destination = WriteFile("existing.txt", "old");
        var output = new OutputSettings { Path = destination, ConflictStrategy = "error" };

        var result = await _publisher.PublishAsync(compiled, output, archiving: null);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal("old", await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public async Task PublishAsync_WithRenameStrategy_WritesASequentiallySuffixedFile()
    {
        var compiled = WriteFile("compiled.txt", "new");
        var destination = WriteFile("existing.txt", "old");
        var output = new OutputSettings { Path = destination, ConflictStrategy = "rename" };

        var result = await _publisher.PublishAsync(compiled, output, archiving: null);

        Assert.True(result.Success);
        Assert.Equal(Path.Combine(_tempDirectory, "existing_1.txt"), result.FinalPath);
        Assert.Equal("old", await File.ReadAllTextAsync(destination));
        Assert.Equal("new", await File.ReadAllTextAsync(result.FinalPath!));
    }

    [Fact]
    public async Task PublishAsync_WithRenameStrategy_SkipsAlreadyTakenSuffixes()
    {
        WriteFile("existing_1.txt", "taken");
        var compiled = WriteFile("compiled.txt", "new");
        var destination = WriteFile("existing.txt", "old");
        var output = new OutputSettings { Path = destination, ConflictStrategy = "rename" };

        var result = await _publisher.PublishAsync(compiled, output, archiving: null);

        Assert.Equal(Path.Combine(_tempDirectory, "existing_2.txt"), result.FinalPath);
    }

    [Fact]
    public async Task PublishAsync_WithOverwriteStrategyAndNoArchiving_ReplacesTheFile()
    {
        var compiled = WriteFile("compiled.txt", "new");
        var destination = WriteFile("existing.txt", "old");
        var output = new OutputSettings { Path = destination, ConflictStrategy = "overwrite" };

        var result = await _publisher.PublishAsync(compiled, output, archiving: null);

        Assert.True(result.Success);
        Assert.Equal(destination, result.FinalPath);
        Assert.Null(result.ArchivedPath);
        Assert.Equal("new", await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public async Task PublishAsync_WithOverwriteStrategyAndArchivingEnabled_ArchivesThePreviousFile()
    {
        var compiled = WriteFile("compiled.txt", "new");
        var destination = WriteFile("existing.txt", "old");
        var output = new OutputSettings { Path = destination, ConflictStrategy = "overwrite" };
        var archiving = new ArchivingSettings { Enabled = true, RetentionDays = 90 };

        var result = await _publisher.PublishAsync(compiled, output, archiving);

        Assert.True(result.Success);
        Assert.NotNull(result.ArchivedPath);
        Assert.True(File.Exists(result.ArchivedPath));
        Assert.Equal("old", await File.ReadAllTextAsync(result.ArchivedPath!));
        Assert.Equal("new", await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public async Task PublishAsync_WithArchivingEnabledButRetentionExpired_PrunesOldArchiveEntries()
    {
        var archiveDirectory = Path.Combine(_tempDirectory, "archive");
        Directory.CreateDirectory(archiveDirectory);
        var staleEntry = Path.Combine(archiveDirectory, "existing-20200101T000000000Z.txt");
        await File.WriteAllTextAsync(staleEntry, "stale");
        File.SetLastWriteTimeUtc(staleEntry, DateTime.UtcNow.AddDays(-365));

        var compiled = WriteFile("compiled.txt", "new");
        var destination = WriteFile("existing.txt", "old");
        var output = new OutputSettings { Path = destination, ConflictStrategy = "overwrite" };
        var archiving = new ArchivingSettings { Enabled = true, RetentionDays = 90 };

        await _publisher.PublishAsync(compiled, output, archiving);

        Assert.False(File.Exists(staleEntry));
    }

    private string WriteFile(string fileName, string content)
    {
        var path = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(path, content);
        return path;
    }
}
