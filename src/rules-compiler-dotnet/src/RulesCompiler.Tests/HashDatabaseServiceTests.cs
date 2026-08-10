namespace RulesCompiler.Tests;

public sealed class HashDatabaseServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly HashDatabaseService _service;

    public HashDatabaseServiceTests()
    {
        _tempDirectory = Directory.CreateTempSubdirectory("hash-database-tests-").FullName;
        _service = new HashDatabaseService(new Mock<ILogger<HashDatabaseService>>().Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WithMissingFile_ReturnsEmptyDictionary()
    {
        var path = Path.Combine(_tempDirectory, ".hashes.json");

        var entries = await _service.LoadAsync(path);

        Assert.Empty(entries);
    }

    [Fact]
    public async Task RecordAsync_ThenLoadAsync_RoundTripsTheEntry()
    {
        var path = Path.Combine(_tempDirectory, ".hashes.json");
        var entry = new HashDatabaseEntry
        {
            Hash = new string('a', 96),
            SizeBytes = 1234,
            ComputedAt = DateTimeOffset.UtcNow,
            ItemType = "output_file",
        };

        await _service.RecordAsync(path, "output.txt", entry);
        var entries = await _service.LoadAsync(path);

        Assert.True(entries.ContainsKey("output.txt"));
        Assert.Equal(entry.Hash, entries["output.txt"].Hash);
        Assert.Equal(entry.SizeBytes, entries["output.txt"].SizeBytes);
        Assert.Equal(entry.ItemType, entries["output.txt"].ItemType);
    }

    [Fact]
    public async Task RecordAsync_CreatesTheDatabaseDirectory()
    {
        var path = Path.Combine(_tempDirectory, "nested", "dir", ".hashes.json");
        var entry = new HashDatabaseEntry { Hash = new string('b', 96) };

        await _service.RecordAsync(path, "output.txt", entry);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task RecordAsync_CalledTwice_PreservesOtherEntries()
    {
        var path = Path.Combine(_tempDirectory, ".hashes.json");
        await _service.RecordAsync(path, "a.txt", new HashDatabaseEntry { Hash = new string('a', 96) });
        await _service.RecordAsync(path, "b.txt", new HashDatabaseEntry { Hash = new string('b', 96) });

        var entries = await _service.LoadAsync(path);

        Assert.Equal(2, entries.Count);
        Assert.True(entries.ContainsKey("a.txt"));
        Assert.True(entries.ContainsKey("b.txt"));
    }

    [Fact]
    public async Task RecordAsync_CalledAgainForSameKey_OverwritesTheEntry()
    {
        var path = Path.Combine(_tempDirectory, ".hashes.json");
        await _service.RecordAsync(path, "output.txt", new HashDatabaseEntry { Hash = new string('a', 96) });
        await _service.RecordAsync(path, "output.txt", new HashDatabaseEntry { Hash = new string('c', 96) });

        var entries = await _service.LoadAsync(path);

        Assert.Single(entries);
        Assert.Equal(new string('c', 96), entries["output.txt"].Hash);
    }
}
