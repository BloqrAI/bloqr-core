namespace Bloqr.Compiler.Core.Tests;

public sealed class QueuedCompilationEventDispatcherTests
{
    private static CompilerOptions Options => new() { ConfigPath = "config.json" };

    [Fact]
    public async Task RaiseValidationAsync_PassesThroughSynchronously()
    {
        var inner = new Mock<ICompilationEventDispatcher>();
        var args = new ValidationEventArgs(Options, "stage", []);
        inner
            .Setup(d => d.RaiseValidationAsync(args, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await using var dispatcher = new QueuedCompilationEventDispatcher(
            inner.Object, new Mock<ILogger<QueuedCompilationEventDispatcher>>().Object);

        await dispatcher.RaiseValidationAsync(args);

        // A pipeline-critical event must have already been observed by the inner dispatcher by
        // the time RaiseValidationAsync returns - it isn't deferred to the background queue.
        inner.Verify(d => d.RaiseValidationAsync(args, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RaiseHashComputedAsync_ReturnsImmediatelyAndProcessesOnBackgroundQueue()
    {
        var inner = new Mock<ICompilationEventDispatcher>();
        var args = new HashComputedEventArgs(Options, "output.txt", "output_file", new string('a', 96), 100);
        var tcs = new TaskCompletionSource();
        inner
            .Setup(d => d.RaiseHashComputedAsync(args, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Yield();
                tcs.TrySetResult();
            });

        var dispatcher = new QueuedCompilationEventDispatcher(
            inner.Object, new Mock<ILogger<QueuedCompilationEventDispatcher>>().Object);

        var raiseTask = dispatcher.RaiseHashComputedAsync(args);

        // Enqueueing must not block the caller on the inner dispatcher's completion.
        Assert.True(raiseTask.IsCompletedSuccessfully);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        inner.Verify(d => d.RaiseHashComputedAsync(args, It.IsAny<CancellationToken>()), Times.Once);

        await dispatcher.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_DrainsAllQueuedEventsBeforeCompleting()
    {
        var inner = new Mock<ICompilationEventDispatcher>();
        var processedCount = 0;
        inner
            .Setup(d => d.RaiseHashVerifiedAsync(It.IsAny<HashVerifiedEventArgs>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(10);
                Interlocked.Increment(ref processedCount);
            });

        var dispatcher = new QueuedCompilationEventDispatcher(
            inner.Object, new Mock<ILogger<QueuedCompilationEventDispatcher>>().Object);

        for (var i = 0; i < 10; i++)
        {
            var args = new HashVerifiedEventArgs(
                Options, $"file{i}.txt", "output_file", new string('a', 96), new string('a', 96), 100, TimeSpan.Zero);
            await dispatcher.RaiseHashVerifiedAsync(args);
        }

        await dispatcher.DisposeAsync();

        Assert.Equal(10, processedCount);
    }

    [Fact]
    public async Task QueuedEvent_WhenInnerDispatcherThrows_DoesNotFaultTheQueueOrPropagate()
    {
        var inner = new Mock<ICompilationEventDispatcher>();
        var callCount = 0;
        inner
            .Setup(d => d.RaiseCompilationErrorAsync(It.IsAny<CompilationErrorEventArgs>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Interlocked.Increment(ref callCount);
                return Task.FromException(new InvalidOperationException("boom"));
            });

        var dispatcher = new QueuedCompilationEventDispatcher(
            inner.Object, new Mock<ILogger<QueuedCompilationEventDispatcher>>().Object);

        var errorArgs = new CompilationErrorEventArgs(Options, new InvalidOperationException("original"));
        var raiseTask = dispatcher.RaiseCompilationErrorAsync(errorArgs);

        // The faulting inner call must not surface back to the caller of RaiseCompilationErrorAsync.
        await raiseTask;

        await dispatcher.DisposeAsync();

        Assert.Equal(1, callCount);
    }
}
