namespace Bloqr.Compiler.Core.Helpers;

/// <summary>
/// Helper class for executing external commands.
/// </summary>
public class CommandHelper
{
    private readonly ILogger<CommandHelper> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandHelper"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public CommandHelper(ILogger<CommandHelper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Finds the path to an executable command.
    /// </summary>
    /// <param name="commandName">The name of the command.</param>
    /// <returns>The full path to the command, or null if not found.</returns>
    public string? FindCommand(string commandName)
    {
        var extensions = PlatformHelper.GetExecutableExtensions();
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathDirs = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var dir in pathDirs)
        {
            foreach (var ext in extensions)
            {
                var fullPath = Path.Combine(dir, commandName + ext);
                if (File.Exists(fullPath))
                {
                    _logger.LogDebug("Found command {Command} at {Path}", commandName, fullPath);
                    return fullPath;
                }
            }
        }

        _logger.LogDebug("Command {Command} not found in PATH", commandName);
        return null;
    }

    /// <summary>
    /// Executes a command and returns the result.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="arguments">The command arguments.</param>
    /// <param name="workingDirectory">Optional working directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of (exitCode, stdout, stderr).</returns>
    public async Task<(int ExitCode, string StdOut, string StdErr)> ExecuteAsync(
        string command,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing: {Command} {Arguments}", command, arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory()
        };

        // Ensure UTF-8 encoding for cross-platform compatibility
        startInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
        startInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;

        using var process = new Process { StartInfo = startInfo };

        // Event handlers must be subscribed before Start() - subscribing via a Task.Run scheduled
        // after Start() (as this used to) is racy: a fast-completing process (e.g. `git
        // rev-parse`, which emits one line and exits almost immediately) can finish and fire its
        // output events before the thread-pool task actually runs and subscribes, silently
        // dropping that output. Plain StringBuilders are safe here because Process guarantees
        // OutputDataReceived/ErrorDataReceived are each raised serially, never concurrently with
        // themselves, and the two streams use separate builders so they can't race each other.
        var stdOutBuilder = new System.Text.StringBuilder();
        var stdErrBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                stdOutBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                stdErrBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        _logger.LogDebug("Command exited with code {ExitCode}", process.ExitCode);

        return (process.ExitCode, stdOutBuilder.ToString(), stdErrBuilder.ToString());
    }

    /// <summary>
    /// Gets the version string from a command.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="versionArg">The version argument (default: --version).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The version string, or null if unavailable.</returns>
    public async Task<string?> GetVersionAsync(
        string command,
        string versionArg = "--version",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (exitCode, stdOut, _) = await ExecuteAsync(command, versionArg, cancellationToken: cancellationToken);

            if (exitCode == 0 && !string.IsNullOrWhiteSpace(stdOut))
            {
                // Return first non-empty line
                return stdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get version for {Command}", command);
        }

        return null;
    }
}
