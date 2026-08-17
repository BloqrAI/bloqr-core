namespace Bloqr.Compiler.Core.Services;

/// <summary>
/// Default implementation of <see cref="IOutputPublisher"/>.
/// </summary>
public class OutputPublisher : IOutputPublisher
{
    private const string ArchiveDirectoryName = "archive";

    private readonly ILogger<OutputPublisher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutputPublisher"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public OutputPublisher(ILogger<OutputPublisher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<OutputPublishResult> PublishAsync(
        string compiledFilePath,
        OutputSettings output,
        ArchivingSettings? archiving,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compiledFilePath);
        ArgumentNullException.ThrowIfNull(output);

        if (string.IsNullOrWhiteSpace(output.Path))
        {
            return new OutputPublishResult { Success = true, FinalPath = compiledFilePath };
        }

        var destinationPath = Path.GetFullPath(output.Path);
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        if (!File.Exists(destinationPath))
        {
            await CopyAsync(compiledFilePath, destinationPath, cancellationToken);
            return new OutputPublishResult { Success = true, FinalPath = destinationPath };
        }

        switch (output.ConflictStrategy.ToLowerInvariant())
        {
            case "error":
                return new OutputPublishResult
                {
                    Success = false,
                    ErrorMessage = $"Output file already exists at {destinationPath} and conflictStrategy is 'error'.",
                };

            case "overwrite":
                string? archivedPath = null;
                if (archiving?.Enabled == true)
                {
                    archivedPath = await ArchiveAsync(destinationPath, archiving, cancellationToken);
                }

                await CopyAsync(compiledFilePath, destinationPath, cancellationToken);
                return new OutputPublishResult
                {
                    Success = true,
                    FinalPath = destinationPath,
                    ArchivedPath = archivedPath,
                };

            default:
                // "rename" and any unrecognized value: never touch the existing file.
                var renamedPath = NextAvailablePath(destinationPath);
                await CopyAsync(compiledFilePath, renamedPath, cancellationToken);
                return new OutputPublishResult { Success = true, FinalPath = renamedPath };
        }
    }

    private static string NextAvailablePath(string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(destinationPath);
        var extension = Path.GetExtension(destinationPath);

        for (var i = 1; ; i++)
        {
            var candidate = Path.Combine(directory, $"{baseName}_{i}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private async Task<string> ArchiveAsync(
        string destinationPath,
        ArchivingSettings archiving,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(destinationPath);
        var extension = Path.GetExtension(destinationPath);

        var archiveDirectory = Path.Combine(directory, ArchiveDirectoryName);
        Directory.CreateDirectory(archiveDirectory);

        var archivedPath = Path.Combine(
            archiveDirectory,
            $"{baseName}-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}{extension}");

        File.Move(destinationPath, archivedPath, overwrite: true);
        _logger.LogInformation("Archived {Destination} to {ArchivedPath}", destinationPath, archivedPath);

        PruneArchive(archiveDirectory, baseName, extension, archiving.RetentionDays);

        await Task.CompletedTask;
        return archivedPath;
    }

    private void PruneArchive(string archiveDirectory, string baseName, string extension, int retentionDays)
    {
        if (retentionDays <= 0)
        {
            return;
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        foreach (var file in Directory.GetFiles(archiveDirectory, $"{baseName}-*{extension}"))
        {
            if (File.GetLastWriteTimeUtc(file) >= cutoff.UtcDateTime)
            {
                continue;
            }

            try
            {
                File.Delete(file);
                _logger.LogDebug("Pruned expired archive entry {File}", file);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Failed to prune expired archive entry {File}", file);
            }
        }
    }

    private static async Task CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        const int bufferSize = 81920;
        await using var sourceStream = new FileStream(
            sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous);
        await using var destinationStream = new FileStream(
            destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous);

        await sourceStream.CopyToAsync(destinationStream, bufferSize, cancellationToken);
    }
}
