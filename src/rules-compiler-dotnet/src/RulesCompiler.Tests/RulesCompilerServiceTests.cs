namespace RulesCompiler.Tests;

public sealed class RulesCompilerServiceTests : IDisposable
{
    private const string OutputHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string DifferentHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private readonly string _tempDirectory;
    private readonly string _configPath;
    private readonly string _compiledPath;

    private readonly Mock<IConfigurationReader> _configurationReader = new();
    private readonly Mock<IFilterCompiler> _filterCompiler = new();
    private readonly Mock<IOutputWriter> _outputWriter = new();
    private readonly Mock<IOutputPublisher> _outputPublisher = new();
    private readonly Mock<IHashDatabaseService> _hashDatabaseService = new();
    private readonly Mock<ICompilationEventDispatcher> _eventDispatcher = new();
    private readonly RulesCompilerService _service;

    public RulesCompilerServiceTests()
    {
        _tempDirectory = Directory.CreateTempSubdirectory("rules-compiler-service-tests-").FullName;
        _configPath = Path.Combine(_tempDirectory, "config.json");
        _compiledPath = Path.Combine(_tempDirectory, "compiled.txt");
        File.WriteAllText(_configPath, "{}");
        File.WriteAllText(_compiledPath, "compiled");

        _outputWriter
            .Setup(w => w.ComputeHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OutputHash);
        _outputWriter
            .Setup(w => w.CountRulesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        _service = new RulesCompilerService(
            new Mock<ILogger<RulesCompilerService>>().Object,
            _configurationReader.Object,
            _filterCompiler.Object,
            _outputWriter.Object,
            _outputPublisher.Object,
            _hashDatabaseService.Object,
            _eventDispatcher.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_WithNoOutputOrHashVerificationConfigured_SucceedsWithoutPublishingOrVerifying()
    {
        SetUpConfiguration(new CompilerConfiguration { Name = "Test", Sources = [new FilterSource { Source = "x" }] });
        SetUpSuccessfulCompilation();

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.True(result.Success);
        Assert.Equal(_compiledPath, result.OutputPath);
        _outputPublisher.Verify(
            p => p.PublishAsync(It.IsAny<string>(), It.IsAny<OutputSettings>(), It.IsAny<ArchivingSettings?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _hashDatabaseService.Verify(
            h => h.RecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HashDatabaseEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenPublishFails_ReturnsFailureWithoutComputingRuleCount()
    {
        SetUpConfiguration(new CompilerConfiguration
        {
            Name = "Test",
            Sources = [new FilterSource { Source = "x" }],
            Output = new OutputSettings { Path = "out.txt", ConflictStrategy = "error" },
        });
        SetUpSuccessfulCompilation();
        _outputPublisher
            .Setup(p => p.PublishAsync(_compiledPath, It.IsAny<OutputSettings>(), It.IsAny<ArchivingSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutputPublishResult { Success = false, ErrorMessage = "already exists" });

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.False(result.Success);
        Assert.Equal("already exists", result.ErrorMessage);
        _outputWriter.Verify(w => w.CountRulesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenPublishSucceeds_UsesThePublishedPathForHashing()
    {
        var publishedPath = Path.Combine(_tempDirectory, "published.txt");
        File.WriteAllText(publishedPath, "published");

        SetUpConfiguration(new CompilerConfiguration
        {
            Name = "Test",
            Sources = [new FilterSource { Source = "x" }],
            Output = new OutputSettings { Path = "out.txt" },
        });
        SetUpSuccessfulCompilation();
        _outputPublisher
            .Setup(p => p.PublishAsync(_compiledPath, It.IsAny<OutputSettings>(), It.IsAny<ArchivingSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutputPublishResult { Success = true, FinalPath = publishedPath });

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.True(result.Success);
        Assert.Equal(publishedPath, result.OutputPath);
    }

    [Fact]
    public async Task RunAsync_WithNoExistingHashEntry_RecordsTheNewHash()
    {
        SetUpConfiguration(new CompilerConfiguration
        {
            Name = "Test",
            Sources = [new FilterSource { Source = "x" }],
            HashVerification = new HashVerificationSettings { Mode = "warning", HashDatabasePath = ".hashes.json" },
        });
        SetUpSuccessfulCompilation();
        _hashDatabaseService
            .Setup(h => h.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, HashDatabaseEntry>());

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.True(result.Success);
        _hashDatabaseService.Verify(
            h => h.RecordAsync(It.IsAny<string>(), _compiledPath, It.Is<HashDatabaseEntry>(e => e.Hash == OutputHash), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithStrictModeAndMismatchingHash_FailsCompilation()
    {
        SetUpConfiguration(new CompilerConfiguration
        {
            Name = "Test",
            Sources = [new FilterSource { Source = "x" }],
            HashVerification = new HashVerificationSettings { Mode = "strict", HashDatabasePath = ".hashes.json" },
        });
        SetUpSuccessfulCompilation();
        _hashDatabaseService
            .Setup(h => h.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, HashDatabaseEntry>
            {
                [_compiledPath] = new() { Hash = DifferentHash },
            });

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        _hashDatabaseService.Verify(
            h => h.RecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HashDatabaseEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_WithWarningModeAndMismatchingHash_ContinuesAndRecordsTheNewHash()
    {
        SetUpConfiguration(new CompilerConfiguration
        {
            Name = "Test",
            Sources = [new FilterSource { Source = "x" }],
            HashVerification = new HashVerificationSettings { Mode = "warning", HashDatabasePath = ".hashes.json" },
        });
        SetUpSuccessfulCompilation();
        _hashDatabaseService
            .Setup(h => h.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, HashDatabaseEntry>
            {
                [_compiledPath] = new() { Hash = DifferentHash },
            });

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.True(result.Success);
        _hashDatabaseService.Verify(
            h => h.RecordAsync(It.IsAny<string>(), _compiledPath, It.Is<HashDatabaseEntry>(e => e.Hash == OutputHash), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithMatchingHash_RaisesHashVerifiedAndDoesNotRewriteTheDatabase()
    {
        SetUpConfiguration(new CompilerConfiguration
        {
            Name = "Test",
            Sources = [new FilterSource { Source = "x" }],
            HashVerification = new HashVerificationSettings { Mode = "strict", HashDatabasePath = ".hashes.json" },
        });
        SetUpSuccessfulCompilation();
        _hashDatabaseService
            .Setup(h => h.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, HashDatabaseEntry>
            {
                [_compiledPath] = new() { Hash = OutputHash },
            });

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.True(result.Success);
        _eventDispatcher.Verify(
            d => d.RaiseHashVerifiedAsync(It.IsAny<HashVerifiedEventArgs>(), It.IsAny<CancellationToken>()), Times.Once);
        _hashDatabaseService.Verify(
            h => h.RecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HashDatabaseEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_WithHashVerificationModeDisabled_SkipsTheDatabaseEntirely()
    {
        SetUpConfiguration(new CompilerConfiguration
        {
            Name = "Test",
            Sources = [new FilterSource { Source = "x" }],
            HashVerification = new HashVerificationSettings { Mode = "disabled", HashDatabasePath = ".hashes.json" },
        });
        SetUpSuccessfulCompilation();

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.True(result.Success);
        _hashDatabaseService.Verify(
            h => h.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetUpConfiguration(CompilerConfiguration configuration)
    {
        _configurationReader
            .Setup(r => r.ReadConfigurationAsync(_configPath, It.IsAny<ConfigurationFormat?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);
    }

    private void SetUpSuccessfulCompilation()
    {
        _filterCompiler
            .Setup(c => c.CompileAsync(It.IsAny<CompilerOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompilerResult { Success = true, OutputPath = _compiledPath });
    }
}
