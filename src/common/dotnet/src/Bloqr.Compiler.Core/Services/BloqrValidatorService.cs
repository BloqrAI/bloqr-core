namespace Bloqr.Compiler.Core.Services;

/// <summary>
/// Default implementation of <see cref="IBloqrValidatorService"/>, P/Invoking into
/// <c>bloqr-validator-core</c>'s native <c>bloqr_validator</c> library.
/// </summary>
public sealed class BloqrValidatorService : IBloqrValidatorService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILogger<BloqrValidatorService> _logger;
    private readonly Lock _initLock = new();

    private bool _initialized;
    private bool _available;
    private IntPtr _handle;

    /// <summary>
    /// Initializes a new instance of the <see cref="BloqrValidatorService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public BloqrValidatorService(ILogger<BloqrValidatorService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            return _available;
        }
    }

    /// <inheritdoc/>
    public Task<SyntaxValidationResult?> ValidateLocalFileAsync(
        string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!IsAvailable)
        {
            return Task.FromResult<SyntaxValidationResult?>(null);
        }

        var status = BloqrValidatorNativeMethods.bloqr_validator_validate_local_file(
            _handle, path, out var resultPtr);

        var result = ReadResultJson(resultPtr, status, "validate_local_file",
            json => JsonSerializer.Deserialize<SyntaxValidationResult>(json, SerializerOptions));

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<UrlValidationResult?> ValidateRemoteUrlAsync(
        string url, string? expectedHash = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (!IsAvailable)
        {
            return Task.FromResult<UrlValidationResult?>(null);
        }

        var status = BloqrValidatorNativeMethods.bloqr_validator_validate_remote_url(
            _handle, url, expectedHash, out var resultPtr);

        var result = ReadResultJson(resultPtr, status, "validate_remote_url",
            json => JsonSerializer.Deserialize<UrlValidationResult>(json, SerializerOptions));

        return Task.FromResult(result);
    }

    /// <summary>
    /// Reads and frees the native out-parameter JSON string, deserializing it with
    /// <paramref name="deserialize"/> only when <paramref name="status"/> indicates success.
    /// A <c>ValidationFailed</c> status (the JSON is <c>{"error": "..."}</c> in that case) or
    /// any other non-success status is logged and treated as "no result" rather than thrown.
    /// </summary>
    private T? ReadResultJson<T>(IntPtr resultPtr, int status, string operation, Func<string, T?> deserialize)
        where T : class
    {
        if (resultPtr == IntPtr.Zero)
        {
            if (status != 0)
            {
                _logger.LogWarning(
                    "bloqr-validator {Operation} returned status {Status} with no result", operation, status);
            }

            return null;
        }

        try
        {
            var json = Marshal.PtrToStringUTF8(resultPtr);
            if (json is null)
            {
                return null;
            }

            if (status != 0)
            {
                _logger.LogWarning("bloqr-validator {Operation} failed: {Error}", operation, json);
                return null;
            }

            return deserialize(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse bloqr-validator {Operation} result", operation);
            return null;
        }
        finally
        {
            BloqrValidatorNativeMethods.bloqr_validator_free_string(resultPtr);
        }
    }

    /// <summary>
    /// Builds the native <c>ValidationConfig</c> JSON passed to <c>bloqr_validator_new</c>.
    /// bloqr-validator-core's internal hash tracking (a separate, redundant mechanism from
    /// .NET's own <see cref="IHashDatabaseService"/> sidecar, #273) always writes its database
    /// on every call regardless of verification mode, using a relative
    /// <c>"data/input/.hashes.json"</c> default that isn't guaranteed to exist or be writable
    /// from this process's working directory. Point it at a guaranteed-writable location
    /// instead of relying on a filesystem layout neither side owns.
    /// </summary>
    private static string BuildConfigJson()
    {
        var directory = Path.Combine(Path.GetTempPath(), "bloqr-validator");
        Directory.CreateDirectory(directory);
        var hashDatabasePath = Path.Combine(directory, ".hashes.json");

        var config = new
        {
            hash_verification = new
            {
                mode = "disabled",
                hash_database_path = hashDatabasePath,
            },
        };

        return JsonSerializer.Serialize(config);
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (_initLock)
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                _handle = BloqrValidatorNativeMethods.bloqr_validator_new(BuildConfigJson());
                _available = _handle != IntPtr.Zero;

                if (!_available)
                {
                    _logger.LogWarning("bloqr_validator_new returned a null handle; bloqr-validator is unavailable");
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                // The native library isn't built/deployed alongside this assembly yet (#276
                // owns packaging it). Treat as gracefully unavailable rather than crashing the
                // whole compilation pipeline over an optional integrity check.
                _logger.LogWarning(ex, "bloqr_validator native library is unavailable; skipping bloqr-validator checks");
                _available = false;
            }
            finally
            {
                _initialized = true;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            BloqrValidatorNativeMethods.bloqr_validator_free(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
