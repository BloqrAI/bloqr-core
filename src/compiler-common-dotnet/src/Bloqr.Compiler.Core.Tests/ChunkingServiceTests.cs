namespace Bloqr.Compiler.Core.Tests;

public class ChunkingServiceTests
{
    private readonly Mock<ILogger<ChunkingService>> _loggerMock;
    private readonly Mock<IConfigurationReader> _configReaderMock;
    private readonly Mock<CommandHelper> _commandHelperMock;
    private readonly Mock<ICompilationEventDispatcher> _eventDispatcherMock;
    private readonly ChunkingService _service;

    public ChunkingServiceTests()
    {
        _loggerMock = new Mock<ILogger<ChunkingService>>();
        _configReaderMock = new Mock<IConfigurationReader>();
        _commandHelperMock = new Mock<CommandHelper>(Mock.Of<ILogger<CommandHelper>>());
        _eventDispatcherMock = new Mock<ICompilationEventDispatcher>();
        _service = new ChunkingService(
            _loggerMock.Object,
            _configReaderMock.Object,
            _commandHelperMock.Object,
            _eventDispatcherMock.Object);
    }

    #region ShouldEnableChunking Tests

    [Fact]
    public void ShouldEnableChunking_WithNoSources_ReturnsFalse()
    {
        var config = new CompilerConfiguration { Sources = [] };
        var options = new ChunkingOptions { Enabled = true };

        var result = _service.ShouldEnableChunking(config, options);

        Assert.False(result);
    }

    [Fact]
    public void ShouldEnableChunking_WithExplicitlyDisabled_ReturnsFalse()
    {
        var config = new CompilerConfiguration
        {
            Sources = [new FilterSource { Source = "http://example.com/list.txt" }]
        };
        var options = new ChunkingOptions { Enabled = false };

        var result = _service.ShouldEnableChunking(config, options);

        Assert.False(result);
    }

    [Fact]
    public void ShouldEnableChunking_WithExplicitlyEnabled_ReturnsTrue()
    {
        var config = new CompilerConfiguration
        {
            Sources = [new FilterSource { Source = "http://example.com/list.txt" }]
        };
        var options = new ChunkingOptions { Enabled = true };

        var result = _service.ShouldEnableChunking(config, options);

        Assert.True(result);
    }

    [Fact]
    public void ShouldEnableChunking_WithMultipleSources_AndNotExplicitlyDisabled_ReturnsTrue()
    {
        var config = new CompilerConfiguration
        {
            Sources =
            [
                new FilterSource { Source = "http://example.com/list1.txt" },
                new FilterSource { Source = "http://example.com/list2.txt" }
            ]
        };
        // When options is null or Enabled is not explicitly set to false,
        // chunking should be enabled for multiple sources with Source strategy
        var result = _service.ShouldEnableChunking(config, null);

        Assert.True(result);
    }

    [Fact]
    public void ShouldEnableChunking_WithSingleSourceAndNotExplicitlyEnabled_ReturnsFalse()
    {
        var config = new CompilerConfiguration
        {
            Sources = [new FilterSource { Source = "http://example.com/list.txt" }]
        };
        var options = new ChunkingOptions { Strategy = ChunkingStrategy.Source };

        var result = _service.ShouldEnableChunking(config, options);

        Assert.False(result);
    }

    #endregion

    #region SplitIntoChunks Tests

    [Fact]
    public void SplitIntoChunks_WithNoSources_ReturnsEmptyList()
    {
        var config = new CompilerConfiguration { Name = "Test", Sources = [] };
        var options = new ChunkingOptions { MaxParallel = 4 };

        var chunks = _service.SplitIntoChunks(config, options);

        Assert.Empty(chunks);
    }

    [Fact]
    public void SplitIntoChunks_WithFourSourcesAndTwoParallel_CreatesTwoChunks()
    {
        var config = new CompilerConfiguration
        {
            Name = "Test",
            Sources =
            [
                new FilterSource { Source = "http://example.com/list1.txt" },
                new FilterSource { Source = "http://example.com/list2.txt" },
                new FilterSource { Source = "http://example.com/list3.txt" },
                new FilterSource { Source = "http://example.com/list4.txt" }
            ]
        };
        var options = new ChunkingOptions { MaxParallel = 2, Strategy = ChunkingStrategy.Source };

        var chunks = _service.SplitIntoChunks(config, options);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(2, chunks[0].Config.Sources.Count);
        Assert.Equal(2, chunks[1].Config.Sources.Count);
    }

    [Fact]
    public void SplitIntoChunks_PreservesConfigurationProperties()
    {
        var config = new CompilerConfiguration
        {
            Name = "Test Filter",
            Description = "Test description",
            Homepage = "https://example.com",
            License = "MIT",
            Version = "1.0.0",
            Sources = [new FilterSource { Source = "http://example.com/list.txt" }],
            Transformations = ["Deduplicate", "RemoveComments"],
            Inclusions = ["*.com"],
            Exclusions = ["ads.*"]
        };
        var options = new ChunkingOptions { MaxParallel = 4, Strategy = ChunkingStrategy.Source };

        var chunks = _service.SplitIntoChunks(config, options);

        var chunkConfig = chunks[0].Config;
        Assert.Contains("Test Filter", chunkConfig.Name);
        Assert.Equal(config.Description, chunkConfig.Description);
        Assert.Equal(config.Homepage, chunkConfig.Homepage);
        Assert.Equal(config.License, chunkConfig.License);
        Assert.Equal(config.Version, chunkConfig.Version);
        Assert.Equal(config.Transformations, chunkConfig.Transformations);
        Assert.Equal(config.Inclusions, chunkConfig.Inclusions);
        Assert.Equal(config.Exclusions, chunkConfig.Exclusions);
    }

    [Fact]
    public void SplitIntoChunks_SetsCorrectMetadata()
    {
        var config = new CompilerConfiguration
        {
            Name = "Test",
            Sources =
            [
                new FilterSource { Source = "http://example.com/list1.txt" },
                new FilterSource { Source = "http://example.com/list2.txt" },
                new FilterSource { Source = "http://example.com/list3.txt" }
            ]
        };
        var options = new ChunkingOptions { MaxParallel = 2, Strategy = ChunkingStrategy.Source };

        var chunks = _service.SplitIntoChunks(config, options);

        Assert.Equal(2, chunks.Count);

        Assert.Equal(0, chunks[0].Metadata.Index);
        Assert.Equal(2, chunks[0].Metadata.Total);
        Assert.Equal(2, chunks[0].Metadata.Sources.Count);

        Assert.Equal(1, chunks[1].Metadata.Index);
        Assert.Equal(2, chunks[1].Metadata.Total);
        Assert.Single(chunks[1].Metadata.Sources);
    }

    #endregion

    #region CompileChunksAsync Event Tests

    [Fact]
    public async Task CompileChunksAsync_RaisesChunkStartedAndChunkCompletedForEachChunk()
    {
        // No hostlist-compiler/npx on PATH in this deterministic test environment ->
        // CompileSingleChunkAsync always throws, exercising the failure branch of the
        // newly-wired ChunkStarted/ChunkCompleted events without depending on real file I/O.
        _commandHelperMock.Setup(c => c.FindCommand(It.IsAny<string>())).Returns((string?)null);

        var chunks = new List<(CompilerConfiguration Config, ChunkMetadata Metadata)>
        {
            (new CompilerConfiguration { Name = "chunk-1" }, new ChunkMetadata { Index = 0, Total = 2 }),
            (new CompilerConfiguration { Name = "chunk-2" }, new ChunkMetadata { Index = 1, Total = 2 }),
        };
        var options = new CompilerOptions { ConfigPath = "config.json" };
        var chunkingOptions = new ChunkingOptions { MaxParallel = 2 };

        var result = await _service.CompileChunksAsync(chunks, options, chunkingOptions);

        _eventDispatcherMock.Verify(
            d => d.RaiseChunkStartedAsync(It.IsAny<ChunkStartedEventArgs>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _eventDispatcherMock.Verify(
            d => d.RaiseChunkCompletedAsync(
                It.Is<ChunkCompletedEventArgs>(a => !a.Success), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        // All chunks failed, so nothing reaches the merge step.
        _eventDispatcherMock.Verify(
            d => d.RaiseChunksMergingAsync(It.IsAny<ChunksMergingEventArgs>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.False(result.Success);
    }

    #endregion

    #region MergeChunks Tests

    [Fact]
    public void MergeChunks_RemovesDuplicateRules()
    {
        var chunkResults = new List<string[]>
        {
            new[] { "||example.com^", "||test.com^" },
            new[] { "||example.com^", "||other.com^" }  // Duplicate
        };

        var (rules, duplicatesRemoved) = _service.MergeChunks(chunkResults);

        Assert.Equal(3, rules.Length);
        Assert.Equal(1, duplicatesRemoved);
        Assert.Contains("||example.com^", rules);
        Assert.Contains("||test.com^", rules);
        Assert.Contains("||other.com^", rules);
    }

    [Fact]
    public void MergeChunks_PreservesComments()
    {
        var chunkResults = new List<string[]>
        {
            new[] { "! Comment 1", "||example.com^" },
            new[] { "! Comment 1", "||other.com^" }  // Same comment in different chunk
        };

        var (rules, duplicatesRemoved) = _service.MergeChunks(chunkResults);

        Assert.Equal(4, rules.Length);  // Both comments are preserved
        Assert.Equal(0, duplicatesRemoved);  // Comments don't count as duplicates
    }

    [Fact]
    public void MergeChunks_PreservesEmptyLines()
    {
        var chunkResults = new List<string[]>
        {
            new[] { "||example.com^", "", "||test.com^" },
            new[] { "||other.com^", "", "" }
        };

        var (rules, duplicatesRemoved) = _service.MergeChunks(chunkResults);

        Assert.Equal(6, rules.Length);  // All lines including empty ones
        Assert.Equal(0, duplicatesRemoved);
    }

    [Fact]
    public void MergeChunks_PreservesHashComments()
    {
        var chunkResults = new List<string[]>
        {
            new[] { "# Comment 1", "||example.com^" },
            new[] { "# Comment 2", "||other.com^" }
        };

        var (rules, duplicatesRemoved) = _service.MergeChunks(chunkResults);

        Assert.Equal(4, rules.Length);
        Assert.Contains("# Comment 1", rules);
        Assert.Contains("# Comment 2", rules);
    }

    [Fact]
    public void MergeChunks_PreservesOrder()
    {
        var chunkResults = new List<string[]>
        {
            new[] { "||first.com^", "||second.com^" },
            new[] { "||third.com^", "||fourth.com^" }
        };

        var (rules, _) = _service.MergeChunks(chunkResults);

        Assert.Equal("||first.com^", rules[0]);
        Assert.Equal("||second.com^", rules[1]);
        Assert.Equal("||third.com^", rules[2]);
        Assert.Equal("||fourth.com^", rules[3]);
    }

    #endregion

    #region EstimateSpeedup Tests

    [Fact]
    public void EstimateSpeedup_WhenDisabled_ReturnsOne()
    {
        var options = new ChunkingOptions { Enabled = false };

        var speedup = _service.EstimateSpeedup(100000, options);

        Assert.Equal(1.0, speedup);
    }

    [Fact]
    public void EstimateSpeedup_WithZeroRules_ReturnsOne()
    {
        var options = new ChunkingOptions { Enabled = true };

        var speedup = _service.EstimateSpeedup(0, options);

        Assert.Equal(1.0, speedup);
    }

    [Fact]
    public void EstimateSpeedup_WithManyRulesAndHighParallelism_ReturnsExpectedSpeedup()
    {
        var options = new ChunkingOptions
        {
            Enabled = true,
            ChunkSize = 100000,
            MaxParallel = 8
        };

        // 800,000 rules = 8 chunks = ~8x speedup (limited by MaxParallel)
        var speedup = _service.EstimateSpeedup(800000, options);

        Assert.Equal(8.0, speedup);
    }

    [Fact]
    public void EstimateSpeedup_LimitedByMaxParallel()
    {
        var options = new ChunkingOptions
        {
            Enabled = true,
            ChunkSize = 100000,
            MaxParallel = 4
        };

        // 1,000,000 rules = 10 chunks, but limited to 4 parallel
        var speedup = _service.EstimateSpeedup(1000000, options);

        Assert.Equal(4.0, speedup);
    }

    #endregion
}

public class ChunkingOptionsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var options = ChunkingOptions.Default;

        Assert.False(options.Enabled);
        Assert.Equal(100_000, options.ChunkSize);
        Assert.Equal(Environment.ProcessorCount, options.MaxParallel);
        Assert.Equal(ChunkingStrategy.Source, options.Strategy);
    }

    [Fact]
    public void ForLargeLists_EnablesChunking()
    {
        var options = ChunkingOptions.ForLargeLists;

        Assert.True(options.Enabled);
        Assert.True(options.MaxParallel >= 2);
    }
}

public class ChunkMetadataTests
{
    [Fact]
    public void ChunkedCompilationResult_EstimatedSpeedup_CalculatesCorrectly()
    {
        var result = new ChunkedCompilationResult
        {
            TotalElapsedMs = 1000,
            Chunks = new List<ChunkMetadata>
            {
                new ChunkMetadata { ElapsedMs = 800 },
                new ChunkMetadata { ElapsedMs = 900 },
                new ChunkMetadata { ElapsedMs = 850 },
                new ChunkMetadata { ElapsedMs = 750 }
            }
        };

        // Sum of individual chunks: 3300ms
        // Parallel time: 1000ms
        // Speedup: 3300 / 1000 = 3.3x
        Assert.Equal(3.3, result.EstimatedSpeedup, 1);
    }

    [Fact]
    public void ChunkedCompilationResult_EstimatedSpeedup_WithNoChunks_ReturnsOne()
    {
        var result = new ChunkedCompilationResult
        {
            TotalElapsedMs = 1000,
            Chunks = new List<ChunkMetadata>()
        };

        Assert.Equal(1.0, result.EstimatedSpeedup);
    }
}
