namespace Bloqr.Compiler.Dotnet.Tests;

/// <summary>
/// Tests for <see cref="BloqrCompilerService"/>'s dual-engine (DNS vs browser-syntax)
/// artifact publishing/hashing (#436 — Wave 2 of epic #432).
/// </summary>
public sealed class BloqrCompilerServiceDualEngineTests : IDisposable
{
    private static readonly string DnsHash = new('a', 96);
    private static readonly string BrowserHash = new('b', 96);

    private readonly string _tempDirectory;
    private readonly string _configPath;
    private readonly string _compiledPath;
    private readonly string _browserCompiledPath;

    private readonly Mock<IConfigurationReader> _configurationReader = new();
    private readonly Mock<IFilterCompiler> _filterCompiler = new();
    private readonly Mock<IOutputWriter> _outputWriter = new();
    private readonly Mock<IOutputPublisher> _outputPublisher = new();
    private readonly Mock<IHashDatabaseService> _hashDatabaseService = new();
    private readonly Mock<IBloqrValidatorService> _rulesValidatorService = new();
    private readonly Mock<ICompilationEventDispatcher> _eventDispatcher = new();
    private readonly BloqrCompilerService _service;

    public BloqrCompilerServiceDualEngineTests()
    {
        _tempDirectory = Directory.CreateTempSubdirectory("rules-compiler-dual-engine-tests-").FullName;
        _configPath = Path.Combine(_tempDirectory, "config.json");
        _compiledPath = Path.Combine(_tempDirectory, "compiled.txt");
        _browserCompiledPath = Path.Combine(_tempDirectory, "compiled.browser.txt");
        File.WriteAllText(_configPath, "{}");
        File.WriteAllText(_compiledPath, "dns rules");
        File.WriteAllText(_browserCompiledPath, "browser rules");

        // Wildcard default (covers the config-file hash computed for the audit trail before
        // compilation even starts) with specific overrides for the two artifact paths -
        // mirrors BloqrCompilerServiceTests' pattern.
        _outputWriter
            .Setup(w => w.ComputeHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DnsHash);
        _outputWriter
            .Setup(w => w.ComputeHashAsync(_compiledPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DnsHash);
        _outputWriter
            .Setup(w => w.ComputeHashAsync(_browserCompiledPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BrowserHash);
        _outputWriter
            .Setup(w => w.CountRulesAsync(_compiledPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _outputWriter
            .Setup(w => w.CountRulesAsync(_browserCompiledPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        _rulesValidatorService.Setup(v => v.IsAvailable).Returns(true);
        _rulesValidatorService
            .Setup(v => v.ValidateLocalFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyntaxValidationResult { IsValid = true, Format = "Adblock", ValidRules = 1, InvalidRules = 0 });

        _service = new BloqrCompilerService(
            new Mock<ILogger<BloqrCompilerService>>().Object,
            _configurationReader.Object,
            _filterCompiler.Object,
            _outputWriter.Object,
            _outputPublisher.Object,
            _hashDatabaseService.Object,
            _rulesValidatorService.Object,
            _eventDispatcher.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private void SetUpConfiguration(CompilerConfiguration configuration)
    {
        _configurationReader
            .Setup(r => r.ReadConfigurationAsync(_configPath, It.IsAny<ConfigurationFormat?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);
    }

    private void SetUpMixedEngineCompilation()
    {
        _filterCompiler
            .Setup(c => c.CompileAsync(It.IsAny<CompilerOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompilerResult
            {
                Success = true,
                OutputPath = _compiledPath,
                BrowserOutputPath = _browserCompiledPath,
            });
    }

    [Fact]
    public async Task RunAsync_MixedEngineConfig_HashesBothArtifactsIndependently()
    {
        SetUpConfiguration(new CompilerConfiguration
        {
            Name = "Mixed",
            Sources = [new FilterSource { Source = "x" }, new FilterSource { Source = "y", Engine = "browser" }],
        });
        SetUpMixedEngineCompilation();

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.True(result.Success);
        Assert.Equal(_compiledPath, result.OutputPath);
        Assert.Equal(DnsHash, result.OutputHash);
        Assert.Equal(3, result.RuleCount);
        Assert.Equal(_browserCompiledPath, result.BrowserOutputPath);
        Assert.Equal(BrowserHash, result.BrowserOutputHash);
        Assert.Equal(2, result.BrowserRuleCount);
    }

    [Fact]
    public async Task RunAsync_MixedEngineConfig_RaisesHashComputedForBothArtifactsWithDistinctItemTypes()
    {
        SetUpConfiguration(new CompilerConfiguration
        {
            Name = "Mixed",
            Sources = [new FilterSource { Source = "x" }, new FilterSource { Source = "y", Engine = "browser" }],
        });
        SetUpMixedEngineCompilation();

        await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        _eventDispatcher.Verify(
            d => d.RaiseHashComputedAsync(
                It.Is<HashComputedEventArgs>(e => e.ItemIdentifier == _compiledPath && e.ItemType == "output_file"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _eventDispatcher.Verify(
            d => d.RaiseHashComputedAsync(
                It.Is<HashComputedEventArgs>(e => e.ItemIdentifier == _browserCompiledPath && e.ItemType == "browser_output_file"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_DnsOnlyConfig_NeverTouchesBrowserArtifactFields()
    {
        SetUpConfiguration(new CompilerConfiguration { Name = "DnsOnly", Sources = [new FilterSource { Source = "x" }] });
        _filterCompiler
            .Setup(c => c.CompileAsync(It.IsAny<CompilerOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompilerResult { Success = true, OutputPath = _compiledPath });

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.True(result.Success);
        Assert.Null(result.BrowserOutputPath);
        Assert.Null(result.BrowserOutputHash);
        Assert.Null(result.BrowserRuleCount);
        _outputWriter.Verify(w => w.ComputeHashAsync(_browserCompiledPath, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_MixedEngineConfig_PublishesBothArtifactsToDerivedPaths()
    {
        var publishedDns = Path.Combine(_tempDirectory, "published.txt");
        var publishedBrowser = Path.Combine(_tempDirectory, "published.browser.txt");
        File.WriteAllText(publishedDns, "dns");
        File.WriteAllText(publishedBrowser, "browser");

        SetUpConfiguration(new CompilerConfiguration
        {
            Name = "Mixed",
            Sources = [new FilterSource { Source = "x" }, new FilterSource { Source = "y", Engine = "browser" }],
            Output = new OutputSettings { Path = "published.txt" },
        });
        SetUpMixedEngineCompilation();
        _outputPublisher
            .Setup(p => p.PublishAsync(_compiledPath, It.IsAny<OutputSettings>(), It.IsAny<ArchivingSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutputPublishResult { Success = true, FinalPath = publishedDns });
        _outputPublisher
            .Setup(p => p.PublishAsync(
                _browserCompiledPath,
                It.Is<OutputSettings>(o => o.Path != null && o.Path.EndsWith("published.browser.txt")),
                It.IsAny<ArchivingSettings?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutputPublishResult { Success = true, FinalPath = publishedBrowser });
        _outputWriter
            .Setup(w => w.ComputeHashAsync(publishedDns, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DnsHash);
        _outputWriter
            .Setup(w => w.ComputeHashAsync(publishedBrowser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BrowserHash);
        _outputWriter
            .Setup(w => w.CountRulesAsync(publishedDns, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _outputWriter
            .Setup(w => w.CountRulesAsync(publishedBrowser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.True(result.Success);
        Assert.Equal(publishedDns, result.OutputPath);
        Assert.Equal(publishedBrowser, result.BrowserOutputPath);
    }

    [Fact]
    public async Task RunAsync_WhenBrowserArtifactPublishFails_ReturnsFailureNamingTheAlreadyPublishedDnsArtifact()
    {
        var publishedDns = Path.Combine(_tempDirectory, "published.txt");
        File.WriteAllText(publishedDns, "dns");

        SetUpConfiguration(new CompilerConfiguration
        {
            Name = "Mixed",
            Sources = [new FilterSource { Source = "x" }, new FilterSource { Source = "y", Engine = "browser" }],
            Output = new OutputSettings { Path = "published.txt", ConflictStrategy = "error" },
        });
        SetUpMixedEngineCompilation();
        _outputPublisher
            .Setup(p => p.PublishAsync(_compiledPath, It.IsAny<OutputSettings>(), It.IsAny<ArchivingSettings?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutputPublishResult { Success = true, FinalPath = publishedDns });
        _outputPublisher
            .Setup(p => p.PublishAsync(
                _browserCompiledPath,
                It.IsAny<OutputSettings>(),
                It.IsAny<ArchivingSettings?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutputPublishResult { Success = false, ErrorMessage = "browser output already exists" });
        _outputWriter
            .Setup(w => w.ComputeHashAsync(publishedDns, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DnsHash);
        _outputWriter
            .Setup(w => w.CountRulesAsync(publishedDns, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        // The DNS artifact was already published successfully - this must not be silently
        // dropped: the failure names the already-published path so the caller sees the
        // partial state explicitly rather than assuming nothing happened.
        Assert.False(result.Success);
        Assert.Contains(publishedDns, result.ErrorMessage);
        Assert.Contains("browser output already exists", result.ErrorMessage);
        Assert.Equal(publishedDns, result.OutputPath);
        _outputWriter.Verify(w => w.ComputeHashAsync(_browserCompiledPath, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_MixedEngineConfigWithHashVerification_RecordsBothArtifactsUnderTheirOwnPaths()
    {
        SetUpConfiguration(new CompilerConfiguration
        {
            Name = "Mixed",
            Sources = [new FilterSource { Source = "x" }, new FilterSource { Source = "y", Engine = "browser" }],
            HashVerification = new HashVerificationSettings { Mode = "warning", HashDatabasePath = ".hashes.json" },
        });
        SetUpMixedEngineCompilation();
        _hashDatabaseService
            .Setup(h => h.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, HashDatabaseEntry>());

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.True(result.Success);
        _hashDatabaseService.Verify(
            h => h.RecordAsync(It.IsAny<string>(), _compiledPath, It.Is<HashDatabaseEntry>(e => e.Hash == DnsHash), It.IsAny<CancellationToken>()),
            Times.Once);
        _hashDatabaseService.Verify(
            h => h.RecordAsync(It.IsAny<string>(), _browserCompiledPath, It.Is<HashDatabaseEntry>(e => e.Hash == BrowserHash), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
