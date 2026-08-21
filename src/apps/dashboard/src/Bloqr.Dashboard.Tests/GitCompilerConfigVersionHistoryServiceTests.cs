using System.Diagnostics;
using Bloqr.Compiler.Core.Helpers;

namespace Bloqr.Dashboard.Tests;

public sealed class GitCompilerConfigVersionHistoryServiceTests : IDisposable
{
    private readonly string _repoDirectory;
    private readonly GitCompilerConfigVersionHistoryService _service;

    public GitCompilerConfigVersionHistoryServiceTests()
    {
        _repoDirectory = Directory.CreateTempSubdirectory("git-history-tests-").FullName;
        _service = new GitCompilerConfigVersionHistoryService(
            new CommandHelper(NullLogger<CommandHelper>.Instance),
            NullLogger<GitCompilerConfigVersionHistoryService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_repoDirectory))
        {
            Directory.Delete(_repoDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task IsUnderVersionControlAsync_OutsideAnyGitRepo_ReturnsFalse()
    {
        var configPath = Path.Combine(_repoDirectory, "compiler-config.json");
        File.WriteAllText(configPath, "{}");

        var result = await _service.IsUnderVersionControlAsync(configPath);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsUnderVersionControlAsync_WithUncommittedFileInAGitRepo_ReturnsFalse()
    {
        InitRepo();
        var configPath = Path.Combine(_repoDirectory, "compiler-config.json");
        File.WriteAllText(configPath, "{}");

        var result = await _service.IsUnderVersionControlAsync(configPath);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsUnderVersionControlAsync_WithCommittedFile_ReturnsTrue()
    {
        InitRepo();
        var configPath = CommitFile("compiler-config.json", "{\"name\":\"v1\"}", "Initial commit");

        var result = await _service.IsUnderVersionControlAsync(configPath);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsCommitsNewestFirst()
    {
        InitRepo();
        var configPath = CommitFile("compiler-config.json", "{\"name\":\"v1\"}", "First version");
        CommitFile("compiler-config.json", "{\"name\":\"v2\"}", "Second version");

        var history = await _service.GetHistoryAsync(configPath);

        history.Should().HaveCount(2);
        history[0].Message.Should().Be("Second version");
        history[1].Message.Should().Be("First version");
    }

    [Fact]
    public async Task GetContentAtRevisionAsync_ReturnsContentFromThatCommit()
    {
        InitRepo();
        var configPath = CommitFile("compiler-config.json", "{\"name\":\"v1\"}", "First version");
        CommitFile("compiler-config.json", "{\"name\":\"v2\"}", "Second version");

        var history = await _service.GetHistoryAsync(configPath);
        var firstCommit = history.Last();

        var content = await _service.GetContentAtRevisionAsync(configPath, firstCommit.ShortSha);

        content.Trim().Should().Be("{\"name\":\"v1\"}");
    }

    [Fact]
    public async Task GetDiffAsync_ReflectsChangesSinceTheGivenRevision()
    {
        InitRepo();
        var configPath = CommitFile("compiler-config.json", "{\"name\":\"v1\"}", "First version");
        File.WriteAllText(configPath, "{\"name\":\"v2-uncommitted\"}");

        var history = await _service.GetHistoryAsync(configPath);

        var diff = await _service.GetDiffAsync(configPath, history[0].ShortSha);

        diff.Should().Contain("v2-uncommitted");
    }

    [Fact]
    public async Task RestoreAsync_OverwritesTheFileWithThatRevisionsContent()
    {
        InitRepo();
        var configPath = CommitFile("compiler-config.json", "{\"name\":\"v1\"}", "First version");
        CommitFile("compiler-config.json", "{\"name\":\"v2\"}", "Second version");

        var history = await _service.GetHistoryAsync(configPath);
        var firstCommit = history.Last();

        await _service.RestoreAsync(configPath, firstCommit.ShortSha);

        File.ReadAllText(configPath).Trim().Should().Be("{\"name\":\"v1\"}");
    }

    private void InitRepo()
    {
        RunGit("init -q .");
        RunGit("config user.email \"test@example.com\"");
        RunGit("config user.name \"Test\"");
        RunGit("config commit.gpgsign false");
    }

    private string CommitFile(string fileName, string content, string message)
    {
        var path = Path.Combine(_repoDirectory, fileName);
        File.WriteAllText(path, content);
        RunGit($"add \"{fileName}\"");
        RunGit($"commit -q -m \"{message}\"");
        return path;
    }

    private void RunGit(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _repoDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(startInfo)!;
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"git {arguments} failed: {error}");
        }
    }
}
