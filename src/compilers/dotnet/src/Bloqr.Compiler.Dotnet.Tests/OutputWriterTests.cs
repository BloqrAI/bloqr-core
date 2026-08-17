namespace Bloqr.Compiler.Dotnet.Tests;

public class OutputWriterTests
{
    private readonly Mock<ILogger<OutputWriter>> _loggerMock;
    private readonly OutputWriter _writer;

    public OutputWriterTests()
    {
        _loggerMock = new Mock<ILogger<OutputWriter>>();
        _writer = new OutputWriter(_loggerMock.Object);
    }

    [Fact]
    public async Task CountRulesAsync_CountsNonCommentLines()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();

        try
        {
            var content = @"! Comment line
# Another comment
||example.com^
||test.org^

@@||allowed.com^
! More comments
||blocked.net^";
            await File.WriteAllTextAsync(tempFile, content);

            // Act
            var count = await _writer.CountRulesAsync(tempFile);

            // Assert
            Assert.Equal(4, count); // 3 blocking rules + 1 allow rule
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CountRulesAsync_HandlesEmptyFile()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(tempFile, string.Empty);

            // Act
            var count = await _writer.CountRulesAsync(tempFile);

            // Assert
            Assert.Equal(0, count);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CountRulesAsync_HandlesAllComments()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();

        try
        {
            var content = @"! Comment 1
! Comment 2
# Comment 3

";
            await File.WriteAllTextAsync(tempFile, content);

            // Act
            var count = await _writer.CountRulesAsync(tempFile);

            // Assert
            Assert.Equal(0, count);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ComputeHashAsync_ReturnsConsistentHash()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();

        try
        {
            var content = "Test content for hashing";
            await File.WriteAllTextAsync(tempFile, content);

            // Act
            var hash1 = await _writer.ComputeHashAsync(tempFile);
            var hash2 = await _writer.ComputeHashAsync(tempFile);

            // Assert
            Assert.Equal(hash1, hash2);
            Assert.Equal(96, hash1.Length); // SHA-384 produces 96 hex characters
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ComputeHashAsync_ProducesDifferentHashesForDifferentContent()
    {
        // Arrange
        var tempFile1 = Path.GetTempFileName();
        var tempFile2 = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(tempFile1, "Content A");
            await File.WriteAllTextAsync(tempFile2, "Content B");

            // Act
            var hash1 = await _writer.ComputeHashAsync(tempFile1);
            var hash2 = await _writer.ComputeHashAsync(tempFile2);

            // Assert
            Assert.NotEqual(hash1, hash2);
        }
        finally
        {
            if (File.Exists(tempFile1))
                File.Delete(tempFile1);
            if (File.Exists(tempFile2))
                File.Delete(tempFile2);
        }
    }

    [Fact]
    public async Task CopyOutputAsync_CopiesFileSuccessfully()
    {
        // Arrange
        var sourceFile = Path.GetTempFileName();
        var destFile = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.txt");

        try
        {
            var content = "Test content to copy";
            await File.WriteAllTextAsync(sourceFile, content);

            // Act
            var result = await _writer.CopyOutputAsync(sourceFile, destFile);

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(destFile));
            Assert.Equal(content, await File.ReadAllTextAsync(destFile));
        }
        finally
        {
            if (File.Exists(sourceFile))
                File.Delete(sourceFile);
            if (File.Exists(destFile))
                File.Delete(destFile);
        }
    }

    [Fact]
    public async Task CopyOutputAsync_CreatesDestinationDirectory()
    {
        // Arrange
        var sourceFile = Path.GetTempFileName();
        var destDir = Path.Combine(Path.GetTempPath(), $"test-dir-{Guid.NewGuid()}");
        var destFile = Path.Combine(destDir, "output.txt");

        try
        {
            await File.WriteAllTextAsync(sourceFile, "Content");

            // Act
            var result = await _writer.CopyOutputAsync(sourceFile, destFile);

            // Assert
            Assert.True(result);
            Assert.True(Directory.Exists(destDir));
            Assert.True(File.Exists(destFile));
        }
        finally
        {
            if (File.Exists(sourceFile))
                File.Delete(sourceFile);
            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
        }
    }

    [Fact]
    public async Task CopyOutputAsync_ThrowsForMissingSource()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _writer.CopyOutputAsync("/nonexistent/file.txt", "/some/dest.txt"));
    }

    [Fact]
    public async Task CountRulesAsync_ThrowsForMissingFile()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _writer.CountRulesAsync("/nonexistent/file.txt"));
    }

    [Fact]
    public async Task ComputeHashAsync_ThrowsForMissingFile()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _writer.ComputeHashAsync("/nonexistent/file.txt"));
    }
}
