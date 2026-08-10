namespace RulesCompiler.Tests;

public sealed class CompilationEventDispatcherTests
{
    private static CompilerOptions Options => new() { ConfigPath = "config.json" };

    [Fact]
    public async Task RaiseHashComputedAsync_InvokesRegisteredHandlers()
    {
        var handler = new Mock<ICompilationEventHandler>();
        handler
            .Setup(h => h.OnHashComputedAsync(It.IsAny<HashComputedEventArgs>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var dispatcher = new CompilationEventDispatcher(
            new Mock<ILogger<CompilationEventDispatcher>>().Object, [handler.Object]);

        var args = new HashComputedEventArgs(Options, "output.txt", "output_file", new string('a', 96), 100);
        await dispatcher.RaiseHashComputedAsync(args);

        handler.Verify(h => h.OnHashComputedAsync(args, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RaiseHashVerifiedAsync_InvokesRegisteredHandlers()
    {
        var handler = new Mock<ICompilationEventHandler>();
        handler
            .Setup(h => h.OnHashVerifiedAsync(It.IsAny<HashVerifiedEventArgs>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var dispatcher = new CompilationEventDispatcher(
            new Mock<ILogger<CompilationEventDispatcher>>().Object, [handler.Object]);

        var args = new HashVerifiedEventArgs(
            Options, "output.txt", "output_file", new string('a', 96), new string('a', 96), 100, TimeSpan.Zero);
        await dispatcher.RaiseHashVerifiedAsync(args);

        handler.Verify(h => h.OnHashVerifiedAsync(args, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RaiseHashMismatchAsync_InvokesRegisteredHandlers()
    {
        var handler = new Mock<ICompilationEventHandler>();
        handler
            .Setup(h => h.OnHashMismatchAsync(It.IsAny<HashMismatchEventArgs>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var dispatcher = new CompilationEventDispatcher(
            new Mock<ILogger<CompilationEventDispatcher>>().Object, [handler.Object]);

        var args = new HashMismatchEventArgs(
            Options, "output.txt", "output_file", new string('a', 96), new string('b', 96), 100);
        await dispatcher.RaiseHashMismatchAsync(args);

        handler.Verify(h => h.OnHashMismatchAsync(args, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RaiseHashMismatchAsync_WhenHandlerThrows_Rethrows()
    {
        var handler = new Mock<ICompilationEventHandler>();
        handler
            .Setup(h => h.OnHashMismatchAsync(It.IsAny<HashMismatchEventArgs>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var dispatcher = new CompilationEventDispatcher(
            new Mock<ILogger<CompilationEventDispatcher>>().Object, [handler.Object]);

        var args = new HashMismatchEventArgs(
            Options, "output.txt", "output_file", new string('a', 96), new string('b', 96), 100);

        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.RaiseHashMismatchAsync(args));
    }

    [Fact]
    public async Task RaiseHashComputedAsync_WhenHandlerThrowsTransientExceptionThenSucceeds_RetriesAndSucceeds()
    {
        var attempts = 0;
        var handler = new Mock<ICompilationEventHandler>();
        handler
            .Setup(h => h.OnHashComputedAsync(It.IsAny<HashComputedEventArgs>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;
                return attempts < 3 ? Task.FromException(new IOException("transient")) : Task.CompletedTask;
            });
        var dispatcher = new CompilationEventDispatcher(
            new Mock<ILogger<CompilationEventDispatcher>>().Object, [handler.Object]);

        var args = new HashComputedEventArgs(Options, "output.txt", "output_file", new string('a', 96), 100);
        await dispatcher.RaiseHashComputedAsync(args);

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task RaiseCompilationStartingAsync_WhenHandlerAlwaysThrowsTransientException_RetriesThenRethrows()
    {
        var attempts = 0;
        var handler = new Mock<ICompilationEventHandler>();
        handler
            .Setup(h => h.OnCompilationStartingAsync(It.IsAny<CompilationStartedEventArgs>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;
                return Task.FromException(new IOException("always transient"));
            });
        var dispatcher = new CompilationEventDispatcher(
            new Mock<ILogger<CompilationEventDispatcher>>().Object, [handler.Object]);

        var args = new CompilationStartedEventArgs(Options);

        await Assert.ThrowsAsync<IOException>(() => dispatcher.RaiseCompilationStartingAsync(args));
        // 1 initial attempt + 3 retries per the dispatcher's retry policy.
        Assert.Equal(4, attempts);
    }

    [Fact]
    public async Task RaiseHashMismatchAsync_WhenHandlerThrowsNonTransientException_DoesNotRetry()
    {
        var attempts = 0;
        var handler = new Mock<ICompilationEventHandler>();
        handler
            .Setup(h => h.OnHashMismatchAsync(It.IsAny<HashMismatchEventArgs>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;
                return Task.FromException(new InvalidOperationException("not transient"));
            });
        var dispatcher = new CompilationEventDispatcher(
            new Mock<ILogger<CompilationEventDispatcher>>().Object, [handler.Object]);

        var args = new HashMismatchEventArgs(
            Options, "output.txt", "output_file", new string('a', 96), new string('b', 96), 100);

        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.RaiseHashMismatchAsync(args));
        Assert.Equal(1, attempts);
    }
}
