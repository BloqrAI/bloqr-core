namespace Bloqr.Compiler.Dotnet.Services;

/// <summary>
/// Writes compiled output to destination directories.
/// </summary>
public class OutputWriter : IOutputWriter
{
    private readonly ILogger<OutputWriter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutputWriter"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public OutputWriter(ILogger<OutputWriter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<bool> CopyOutputAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be null or empty.", nameof(sourcePath));

        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path cannot be null or empty.", nameof(destinationPath));

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Source file not found: {sourcePath}", sourcePath);

        try
        {
            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
                _logger.LogDebug("Created directory: {Directory}", destDir);
            }

            // Use streaming copy for better memory efficiency
            const int bufferSize = 81920; // 80KB buffer
            await using var sourceStream = new FileStream(
                sourcePath, 
                FileMode.Open, 
                FileAccess.Read, 
                FileShare.Read, 
                bufferSize, 
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            
            await using var destStream = new FileStream(
                destinationPath, 
                FileMode.Create, 
                FileAccess.Write, 
                FileShare.None, 
                bufferSize, 
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            await sourceStream.CopyToAsync(destStream, bufferSize, cancellationToken);

            _logger.LogInformation("Copied output to {Destination}", destinationPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy output to {Destination}", destinationPath);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<string> ComputeHashAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        await using var stream = File.OpenRead(filePath);
        var hashBytes = await SHA384.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <inheritdoc/>
    public async Task<int> CountRulesAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        int count = 0;
        
        // Use streaming to avoid loading entire file into memory
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var trimmed = line.AsSpan().Trim();

            // Skip comments (lines starting with ! or #)
            if (trimmed.Length > 0 && trimmed[0] is not ('!' or '#'))
            {
                count++;
            }
        }

        return count;
    }
}
