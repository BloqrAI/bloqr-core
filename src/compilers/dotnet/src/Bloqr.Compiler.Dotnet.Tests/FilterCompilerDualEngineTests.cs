using Bloqr.Compiler.Core.Helpers;

namespace Bloqr.Compiler.Dotnet.Tests;

/// <summary>
/// Tests for <see cref="FilterCompiler"/>'s dual-engine (#436) behavior: passing
/// <c>Engine</c>/<c>BrowserOutputPath</c> through to the underlying CLI command, and
/// detecting a browser-syntax artifact by its (derived-default or explicit) file's
/// presence after compilation.
/// </summary>
public sealed class FilterCompilerDualEngineTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _configPath;

    private readonly Mock<IConfigurationReader> _configurationReader = new();
    private readonly Mock<CommandHelper> _commandHelper;
    private readonly FilterCompiler _compiler;

    public FilterCompilerDualEngineTests()
    {
        _tempDirectory = Directory.CreateTempSubdirectory("filter-compiler-dual-engine-tests-").FullName;
        _configPath = Path.Combine(_tempDirectory, "config.json");
        File.WriteAllText(_configPath, "{}");

        _configurationReader
            .Setup(r => r.ReadConfigurationAsync(_configPath, It.IsAny<ConfigurationFormat?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompilerConfiguration { Name = "Test", Sources = [new FilterSource { Source = "x" }] });
        _configurationReader
            .Setup(r => r.DetectFormat(_configPath))
            .Returns(ConfigurationFormat.Json);

        _commandHelper = new Mock<CommandHelper>(new Mock<ILogger<CommandHelper>>().Object);
        _compiler = new FilterCompiler(new Mock<ILogger<FilterCompiler>>().Object, _configurationReader.Object, _commandHelper.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Stubs the command execution to succeed and write the given file(s), simulating what
    /// the real <c>@bloqr/compiler-core</c> CLI does for a single- vs. mixed-engine config.
    /// </summary>
    private void SetUpCommandExecution(string outputPath, string? browserOutputPath = null)
    {
        _commandHelper
            .Setup(c => c.GetBloqrCompilerCoreCommand(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(("fake-deno", "fake-args"));
        _commandHelper
            .Setup(c => c.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                File.WriteAllText(outputPath, "dns rules");
                if (browserOutputPath is not null)
                {
                    File.WriteAllText(browserOutputPath, "browser rules");
                }
            })
            .ReturnsAsync((0, string.Empty, string.Empty));
    }

    [Fact]
    public async Task CompileAsync_WithNoEngineOptions_PassesNullEngineAndBrowserOutputToCommandHelper()
    {
        var outputPath = Path.Combine(_tempDirectory, "output.txt");
        SetUpCommandExecution(outputPath);

        await _compiler.CompileAsync(new CompilerOptions { ConfigPath = _configPath, OutputPath = outputPath });

        _commandHelper.Verify(
            c => c.GetBloqrCompilerCoreCommand(_configPath, outputPath, false, null, null),
            Times.Once);
    }

    [Fact]
    public async Task CompileAsync_WithEngineAndBrowserOutputOptions_PassesThemToCommandHelper()
    {
        var outputPath = Path.Combine(_tempDirectory, "output.txt");
        var browserOutputPath = Path.Combine(_tempDirectory, "custom-browser.txt");
        SetUpCommandExecution(outputPath, browserOutputPath);

        await _compiler.CompileAsync(new CompilerOptions
        {
            ConfigPath = _configPath,
            OutputPath = outputPath,
            Engine = "browser",
            BrowserOutputPath = browserOutputPath,
        });

        _commandHelper.Verify(
            c => c.GetBloqrCompilerCoreCommand(_configPath, outputPath, false, "browser", browserOutputPath),
            Times.Once);
    }

    [Fact]
    public async Task CompileAsync_WhenNoBrowserArtifactWritten_LeavesResultBrowserOutputPathNull()
    {
        var outputPath = Path.Combine(_tempDirectory, "output.txt");
        SetUpCommandExecution(outputPath); // no browser file written - all-DNS config

        var result = await _compiler.CompileAsync(new CompilerOptions { ConfigPath = _configPath, OutputPath = outputPath });

        Assert.True(result.Success);
        Assert.Null(result.BrowserOutputPath);
    }

    [Fact]
    public async Task CompileAsync_WhenDerivedDefaultBrowserArtifactExists_ReportsItOnTheResult()
    {
        var outputPath = Path.Combine(_tempDirectory, "output.txt");
        var derivedBrowserPath = Path.Combine(_tempDirectory, "output.browser.txt");
        SetUpCommandExecution(outputPath, derivedBrowserPath);

        var result = await _compiler.CompileAsync(new CompilerOptions { ConfigPath = _configPath, OutputPath = outputPath });

        Assert.True(result.Success);
        Assert.Equal(derivedBrowserPath, result.BrowserOutputPath);
    }

    [Fact]
    public async Task CompileAsync_WhenExplicitBrowserOutputPathGiven_UsesItRatherThanTheDerivedDefault()
    {
        var outputPath = Path.Combine(_tempDirectory, "output.txt");
        var explicitBrowserPath = Path.Combine(_tempDirectory, "custom-browser-name.txt");
        SetUpCommandExecution(outputPath, explicitBrowserPath);

        var result = await _compiler.CompileAsync(new CompilerOptions
        {
            ConfigPath = _configPath,
            OutputPath = outputPath,
            BrowserOutputPath = explicitBrowserPath,
        });

        Assert.True(result.Success);
        Assert.Equal(explicitBrowserPath, result.BrowserOutputPath);
    }
}
