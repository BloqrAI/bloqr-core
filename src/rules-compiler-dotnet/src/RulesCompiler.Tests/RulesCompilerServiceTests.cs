namespace RulesCompiler.Tests;

public sealed class RulesCompilerServiceTests : IDisposable
{
    // Built rather than written as hex-shaped string literals so scanners don't mistake
    // these deterministic test fixtures for real embedded secrets.
    private static readonly string OutputHash = new('a', 96);
    private static readonly string DifferentHash = new('b', 96);

    private readonly string _tempDirectory;
    private readonly string _configPath;
    private readonly string _compiledPath;

    private readonly Mock<IConfigurationReader> _configurationReader = new();
    private readonly Mock<IFilterCompiler> _filterCompiler = new();
    private readonly Mock<IOutputWriter> _outputWriter = new();
    private readonly Mock<IOutputPublisher> _outputPublisher = new();
    private readonly Mock<IHashDatabaseService> _hashDatabaseService = new();
    private readonly Mock<IRulesValidatorService> _rulesValidatorService = new();
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

        // Unavailable by default (as it would be wherever the native library isn't deployed
        // alongside the test binaries) so existing tests exercise the pipeline's graceful
        // degradation path rather than needing real bloqr-validator behavior.
        _rulesValidatorService.Setup(v => v.IsAvailable).Returns(false);

        _service = new RulesCompilerService(
            new Mock<ILogger<RulesCompilerService>>().Object,
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

    [Fact]
    public async Task RunAsync_WhenRulesValidatorUnavailable_SkipsValidationAndStillSucceeds()
    {
        SetUpConfiguration(new CompilerConfiguration { Name = "Test", Sources = [new FilterSource { Source = "x" }] });
        SetUpSuccessfulCompilation();
        _rulesValidatorService.Setup(v => v.IsAvailable).Returns(false);

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.True(result.Success);
        _rulesValidatorService.Verify(
            v => v.ValidateLocalFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventDispatcher.Verify(
            d => d.RaiseValidationAsync(It.IsAny<ValidationEventArgs>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenRulesValidatorFindsValidSyntax_RaisesValidationEventAndSucceeds()
    {
        SetUpConfiguration(new CompilerConfiguration { Name = "Test", Sources = [new FilterSource { Source = "x" }] });
        SetUpSuccessfulCompilation();
        _rulesValidatorService.Setup(v => v.IsAvailable).Returns(true);
        _rulesValidatorService
            .Setup(v => v.ValidateLocalFileAsync(_compiledPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyntaxValidationResult { IsValid = true, Format = "Adblock", ValidRules = 3, InvalidRules = 0 });

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.True(result.Success);
        _eventDispatcher.Verify(
            d => d.RaiseValidationAsync(
                It.Is<ValidationEventArgs>(a => a.StageName == "bloqr-validator"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenRulesValidatorFindsInvalidSyntaxAndHandlerAborts_FailsCompilation()
    {
        SetUpConfiguration(new CompilerConfiguration { Name = "Test", Sources = [new FilterSource { Source = "x" }] });
        SetUpSuccessfulCompilation();
        _rulesValidatorService.Setup(v => v.IsAvailable).Returns(true);
        _rulesValidatorService
            .Setup(v => v.ValidateLocalFileAsync(_compiledPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyntaxValidationResult
            {
                IsValid = false,
                Format = "Adblock",
                ValidRules = 2,
                InvalidRules = 1,
                Messages = ["bad rule at line 3"],
            });
        _eventDispatcher
            .Setup(d => d.RaiseValidationAsync(It.IsAny<ValidationEventArgs>(), It.IsAny<CancellationToken>()))
            .Callback<ValidationEventArgs, CancellationToken>((args, _) =>
            {
                args.Abort = true;
                args.AbortReason = "syntax invalid";
            })
            .Returns(Task.CompletedTask);

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.False(result.Success);
        Assert.Equal("syntax invalid", result.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_WhenRulesValidatorFindsInvalidSyntaxButHandlerDoesNotAbort_StillSucceeds()
    {
        SetUpConfiguration(new CompilerConfiguration { Name = "Test", Sources = [new FilterSource { Source = "x" }] });
        SetUpSuccessfulCompilation();
        _rulesValidatorService.Setup(v => v.IsAvailable).Returns(true);
        _rulesValidatorService
            .Setup(v => v.ValidateLocalFileAsync(_compiledPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyntaxValidationResult { IsValid = false, Format = "Adblock", ValidRules = 2, InvalidRules = 1 });

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.True(result.Success);
    }

    [Fact]
    public async Task RunAsync_OnSuccess_RaisesStartingConfigurationLoadedAndCompletedEvents()
    {
        SetUpConfiguration(new CompilerConfiguration { Name = "Test", Sources = [new FilterSource { Source = "x" }] });
        SetUpSuccessfulCompilation();

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.True(result.Success);
        _eventDispatcher.Verify(
            d => d.RaiseCompilationStartingAsync(It.IsAny<CompilationStartedEventArgs>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _eventDispatcher.Verify(
            d => d.RaiseConfigurationLoadedAsync(
                It.Is<ConfigurationLoadedEventArgs>(a => a.Configuration.Name == "Test"), It.IsAny<CancellationToken>()),
            Times.Once);
        _eventDispatcher.Verify(
            d => d.RaiseCompilationCompletedAsync(
                It.Is<CompilationCompletedEventArgs>(a => a.Result == result), It.IsAny<CancellationToken>()),
            Times.Once);
        _eventDispatcher.Verify(
            d => d.RaiseCompilationErrorAsync(It.IsAny<CompilationErrorEventArgs>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenCompilationFails_RaisesCompilationErrorInsteadOfCompleted()
    {
        SetUpConfiguration(new CompilerConfiguration { Name = "Test", Sources = [new FilterSource { Source = "x" }] });
        _filterCompiler
            .Setup(c => c.CompileAsync(It.IsAny<CompilerOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompilerResult { Success = false, ErrorMessage = "boom" });

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.False(result.Success);
        _eventDispatcher.Verify(
            d => d.RaiseCompilationErrorAsync(It.IsAny<CompilationErrorEventArgs>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _eventDispatcher.Verify(
            d => d.RaiseCompilationCompletedAsync(It.IsAny<CompilationCompletedEventArgs>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenCompilationStartingHandlerCancels_ReturnsFailureWithoutCompiling()
    {
        _eventDispatcher
            .Setup(d => d.RaiseCompilationStartingAsync(It.IsAny<CompilationStartedEventArgs>(), It.IsAny<CancellationToken>()))
            .Callback<CompilationStartedEventArgs, CancellationToken>((args, _) =>
            {
                args.Cancel = true;
                args.CancelReason = "not now";
            })
            .Returns(Task.CompletedTask);

        var result = await _service.RunAsync(new CompilerOptions { ConfigPath = _configPath, ValidateConfig = false });

        Assert.False(result.Success);
        Assert.Equal("not now", result.ErrorMessage);
        _filterCompiler.Verify(
            c => c.CompileAsync(It.IsAny<CompilerOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventDispatcher.Verify(
            d => d.RaiseCompilationErrorAsync(It.IsAny<CompilationErrorEventArgs>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
