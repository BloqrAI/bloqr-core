using Bloqr.Dashboard.Console.Progress;

namespace Bloqr.Dashboard.Tests;

/// <summary>
/// Covers <see cref="CompilationProgressEventHandler"/>'s two-engine handling (#441): a
/// mixed-engine config gets named "DNS artifact"/"Browser artifact" child tasks under the
/// existing "Compiling" root, completed in the same sequential order the underlying compile
/// runs in - not two concurrent roots (see the handler's own class remarks for why).
/// </summary>
public sealed class CompilationProgressEventHandlerTests
{
    private static CompilerOptions Options(string? engine = null) => new() { ConfigPath = "config.json", Engine = engine };

    [Fact]
    public async Task MixedEngineConfig_AddsNamedDnsAndBrowserChildTasksInOrder()
    {
        var session = new LiveProgressSession();
        var context = new FakeLiveProgressContext();
        session.Current = context;

        var handler = new CompilationProgressEventHandler(session, new NoOpConsoleRenderer());

        await handler.OnCompilationStartingAsync(new CompilationStartedEventArgs(Options())).ConfigureAwait(true);

        var config = new CompilerConfiguration
        {
            Name = "Mixed",
            Sources =
            [
                new FilterSource { Name = "dns-src", Source = "a.txt", Type = "hosts", Engine = "dns" },
                new FilterSource { Name = "browser-src", Source = "b.txt", Type = "adblock", Engine = "browser" },
            ],
        };
        await handler.OnConfigurationLoadedAsync(new ConfigurationLoadedEventArgs(Options(), config)).ConfigureAwait(true);

        context.Tasks.Select(t => t.Description.Trim()).Should().Equal("Compiling", "DNS artifact", "Browser artifact");
        context.Tasks.Should().OnlyContain(t => !t.IsFinished);

        // The DNS artifact finishes when chunks are merged...
        await handler.OnChunksMergedAsync(
            new ChunksMergedEventArgs(Options(), chunkCount: 1, totalRulesBeforeMerge: 10, finalRuleCount: 10, duplicatesRemoved: 0, duration: TimeSpan.Zero))
            .ConfigureAwait(true);

        context.Tasks[1].IsFinished.Should().BeTrue("the DNS child task completes at ChunksMerged");
        context.Tasks[2].IsFinished.Should().BeFalse("the browser child task is not done until the whole compile completes");

        // ...and the browser artifact (published/hashed after the DNS one) finishes when the
        // overall compile completes.
        var result = new CompilerResult { Success = true, OutputPath = "out.txt", RuleCount = 5 };
        await handler.OnCompilationCompletedAsync(new CompilationCompletedEventArgs(Options(), result, TimeSpan.FromMilliseconds(1)))
            .ConfigureAwait(true);

        context.Tasks.Should().OnlyContain(t => t.IsFinished);
    }

    [Fact]
    public async Task SingleEngineConfig_DoesNotAddDnsOrBrowserChildTasks()
    {
        var session = new LiveProgressSession();
        var context = new FakeLiveProgressContext();
        session.Current = context;

        var handler = new CompilationProgressEventHandler(session, new NoOpConsoleRenderer());

        await handler.OnCompilationStartingAsync(new CompilationStartedEventArgs(Options())).ConfigureAwait(true);

        var config = new CompilerConfiguration
        {
            Name = "Dns Only",
            Sources = [new FilterSource { Name = "dns-src", Source = "a.txt", Type = "hosts" }],
        };
        await handler.OnConfigurationLoadedAsync(new ConfigurationLoadedEventArgs(Options(), config)).ConfigureAwait(true);

        context.Tasks.Select(t => t.Description.Trim()).Should().Equal("Compiling");
    }

    [Fact]
    public async Task NoLiveProgressSession_IsNoOp()
    {
        var session = new LiveProgressSession(); // Current stays null.
        var handler = new CompilationProgressEventHandler(session, new NoOpConsoleRenderer());

        await handler.OnCompilationStartingAsync(new CompilationStartedEventArgs(Options())).ConfigureAwait(true);
        var config = new CompilerConfiguration
        {
            Name = "Mixed",
            Sources =
            [
                new FilterSource { Name = "dns-src", Source = "a.txt", Type = "hosts", Engine = "dns" },
                new FilterSource { Name = "browser-src", Source = "b.txt", Type = "adblock", Engine = "browser" },
            ],
        };

        // Should not throw despite there being no live context to add tasks to.
        var act = () => handler.OnConfigurationLoadedAsync(new ConfigurationLoadedEventArgs(Options(), config));
        await act.Should().NotThrowAsync().ConfigureAwait(true);
    }

    private sealed class FakeLiveProgressContext : ILiveProgressContext
    {
        public List<FakeLiveProgressTask> Tasks { get; } = [];

        public ILiveProgressTask AddTask(string description, double maxValue = 100)
        {
            var task = new FakeLiveProgressTask(description, maxValue);
            Tasks.Add(task);
            return task;
        }
    }

    private sealed class FakeLiveProgressTask(string description, double maxValue) : ILiveProgressTask
    {
        public string Description { get; set; } = description;

        public double Value { get; set; }

        public double MaxValue { get; } = maxValue;

        public bool IsFinished => Value >= MaxValue;

        public void Increment(double amount) => Value = Math.Min(MaxValue, Value + amount);

        public void Complete() => Value = MaxValue;
    }

    private sealed class NoOpConsoleRenderer : IConsoleRenderer
    {
        public void WriteLine(string text)
        {
        }

        public void Write(string text)
        {
        }

        public void WriteLine()
        {
        }

        public void WriteStyled(string text, TextStyle style)
        {
        }

        public void RenderTable(ConsoleTable table)
        {
        }

        public void RenderPanel(string content, string? title = null)
        {
        }

        public void RenderRule(string? title = null)
        {
        }

        public void Clear()
        {
        }

        public Task<T> StatusAsync<T>(string status, Func<Task<T>> operation) => operation();

        public Task StatusAsync(string status, Func<Task> operation) => operation();

        public Task<T> ProgressAsync<T>(string description, Func<IProgress<double>, Task<T>> operation) =>
            operation(new Progress<double>());

        public Task<T> LiveProgressAsync<T>(Func<ILiveProgressContext, Task<T>> operation) =>
            throw new NotImplementedException();
    }
}
