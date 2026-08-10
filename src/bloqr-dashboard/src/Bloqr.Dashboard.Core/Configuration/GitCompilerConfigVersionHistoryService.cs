using Bloqr.Compiler.Core.Helpers;

namespace Bloqr.Dashboard.Core.Configuration;

/// <summary>
/// Default implementation of <see cref="ICompilerConfigVersionHistoryService"/>, shelling out to
/// a real <c>git</c> binary via the same <see cref="CommandHelper"/> the compiler pipeline uses
/// to find and run <c>deno</c>. All commands run with the config file's directory as the working
/// directory and address the file with a <c>./</c>-relative pathspec, so this works regardless of
/// where in the repository the file actually lives.
/// </summary>
public sealed class GitCompilerConfigVersionHistoryService : ICompilerConfigVersionHistoryService
{
    private const string GitCommand = "git";

    // Unit separator (0x1F): a control character that will never appear in a commit's author
    // name or subject line, so it's safe to split %an/%ad/%s fields on without ambiguity.
    private const string FieldSeparator = "";

    private readonly CommandHelper _commandHelper;
    private readonly ILogger<GitCompilerConfigVersionHistoryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitCompilerConfigVersionHistoryService"/> class.
    /// </summary>
    public GitCompilerConfigVersionHistoryService(
        CommandHelper commandHelper,
        ILogger<GitCompilerConfigVersionHistoryService> logger)
    {
        _commandHelper = commandHelper ?? throw new ArgumentNullException(nameof(commandHelper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> IsUnderVersionControlAsync(string configPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);

        var git = _commandHelper.FindCommand(GitCommand);
        if (git is null)
        {
            return false;
        }

        var directory = ResolveDirectory(configPath);
        var (exitCode, stdOut, _) = await _commandHelper
            .ExecuteAsync(git, "rev-parse --is-inside-work-tree", directory, cancellationToken)
            .ConfigureAwait(false);

        if (exitCode != 0 || !stdOut.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var history = await GetHistoryAsync(configPath, cancellationToken).ConfigureAwait(false);
        return history.Count > 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompilerConfigRevision>> GetHistoryAsync(
        string configPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);

        var git = _commandHelper.FindCommand(GitCommand);
        if (git is null)
        {
            return [];
        }

        var directory = ResolveDirectory(configPath);
        var fileName = Path.GetFileName(configPath);
        var (exitCode, stdOut, stdErr) = await _commandHelper
            .ExecuteAsync(
                git,
                $"log --follow --date=iso-strict --pretty=format:%h{FieldSeparator}%an{FieldSeparator}%ad{FieldSeparator}%s -- \"{fileName}\"",
                directory,
                cancellationToken)
            .ConfigureAwait(false);

        if (exitCode != 0)
        {
            _logger.LogDebug("git log failed for {ConfigPath}: {Error}", configPath, stdErr);
            return [];
        }

        var revisions = new List<CompilerConfigRevision>();
        var lines = stdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(rawLine => rawLine.TrimEnd('\r'));
        foreach (var line in lines)
        {
            var parts = line.Split(FieldSeparator);
            if (parts.Length != 4 || !DateTimeOffset.TryParse(parts[2], out var date))
            {
                continue;
            }

            revisions.Add(new CompilerConfigRevision(parts[0], parts[1], date, parts[3]));
        }

        return revisions;
    }

    /// <inheritdoc />
    public async Task<string> GetContentAtRevisionAsync(
        string configPath,
        string revision,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);

        var git = RequireGit();
        var directory = ResolveDirectory(configPath);
        var fileName = Path.GetFileName(configPath);

        var (exitCode, stdOut, stdErr) = await _commandHelper
            .ExecuteAsync(git, $"show \"{revision}:./{fileName}\"", directory, cancellationToken)
            .ConfigureAwait(false);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"git show failed for {configPath} at {revision}: {stdErr}");
        }

        return stdOut;
    }

    /// <inheritdoc />
    public async Task<string> GetDiffAsync(string configPath, string revision, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);

        var git = RequireGit();
        var directory = ResolveDirectory(configPath);
        var fileName = Path.GetFileName(configPath);

        var (exitCode, stdOut, stdErr) = await _commandHelper
            .ExecuteAsync(git, $"diff {revision} -- \"{fileName}\"", directory, cancellationToken)
            .ConfigureAwait(false);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"git diff failed for {configPath} against {revision}: {stdErr}");
        }

        return stdOut;
    }

    /// <inheritdoc />
    public async Task RestoreAsync(string configPath, string revision, CancellationToken cancellationToken = default)
    {
        // Content restore only: reads the blob at the given revision and overwrites the working
        // file, without touching git's index or any other file - deliberately not a `git
        // checkout`, which would have broader side effects than "put this file's old content back".
        var content = await GetContentAtRevisionAsync(configPath, revision, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(configPath, content, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Restored {ConfigPath} to content from revision {Revision}", configPath, revision);
    }

    private string RequireGit() =>
        _commandHelper.FindCommand(GitCommand)
            ?? throw new InvalidOperationException("git was not found on PATH.");

    private static string ResolveDirectory(string configPath) =>
        Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? Directory.GetCurrentDirectory();
}
