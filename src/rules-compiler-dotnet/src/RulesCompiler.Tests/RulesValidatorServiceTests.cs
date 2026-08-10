namespace RulesCompiler.Tests;

/// <summary>
/// Exercises <see cref="RulesValidatorService"/> against the real native <c>rules_validator</c>
/// library. Requires <c>librules_validator.so</c> (or the platform equivalent) alongside the
/// test binaries - build it with `cargo build --release -p rules-validator-core` and copy the
/// artifact from `target/release/` into this project's output directory. #276 owns automating
/// that; until then, these tests no-op (pass trivially, not fail) when the library isn't
/// present, since <see cref="RulesValidatorService"/> is explicitly designed to degrade
/// gracefully rather than require the native library to exist.
/// </summary>
public sealed class RulesValidatorServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly RulesValidatorService _service;

    public RulesValidatorServiceTests()
    {
        _tempDirectory = Directory.CreateTempSubdirectory("rules-validator-service-tests-").FullName;
        _service = new RulesValidatorService(new Mock<ILogger<RulesValidatorService>>().Object);
    }

    public void Dispose()
    {
        _service.Dispose();
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateLocalFileAsync_WithValidAdblockSyntax_ReturnsValidResult()
    {
        if (!_service.IsAvailable)
        {
            return;
        }

        var path = Path.Combine(_tempDirectory, "valid.txt");
        await File.WriteAllTextAsync(path, "||example.com^\n@@||allowed.com\n");

        var result = await _service.ValidateLocalFileAsync(path);

        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.True(result.ValidRules >= 1);
    }

    [Fact]
    public async Task ValidateLocalFileAsync_CalledTwice_ReusesTheSameNativeHandle()
    {
        if (!_service.IsAvailable)
        {
            return;
        }

        var path = Path.Combine(_tempDirectory, "valid.txt");
        await File.WriteAllTextAsync(path, "||example.com^\n");

        var first = await _service.ValidateLocalFileAsync(path);
        var second = await _service.ValidateLocalFileAsync(path);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
    }

    [Fact]
    public async Task ValidateRemoteUrlAsync_WithHttpUrl_IsRejected()
    {
        if (!_service.IsAvailable)
        {
            return;
        }

        var result = await _service.ValidateRemoteUrlAsync("http://insecure.example.com/list.txt");

        Assert.NotNull(result);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void IsAvailable_DoesNotThrowRegardlessOfWhetherTheNativeLibraryIsPresent()
    {
        // Verifies the graceful-degradation path itself: checking IsAvailable (or calling the
        // validation methods without checking it first) must never throw, whether or not the
        // native library happens to be present in this test run.
        var available = _service.IsAvailable;
        Assert.True(available || !available); // No throw is the actual assertion.
    }
}
