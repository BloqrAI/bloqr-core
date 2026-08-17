namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Wraps <c>bloqr-validator-core</c>'s native FFI surface (<c>bloqr_validator.h</c>) for
/// filter-list syntax and remote-source URL validation. See #264 and
/// <c>src/validation/core/README.md</c>'s ".NET / C#" section for the P/Invoke pattern this
/// implements.
/// </summary>
/// <remarks>
/// The native library isn't guaranteed to be present at runtime yet (its build/packaging into
/// the .NET output directory is tracked separately by #276) - callers must check
/// <see cref="IsAvailable"/> or simply treat a <c>null</c> result as "skip this check", never as
/// a compilation failure. This mirrors how <c>IConfigurationReader</c> callers already treat a
/// missing external tool (e.g. <c>deno</c>) defensively rather than crashing.
/// </remarks>
public interface IBloqrValidatorService
{
    /// <summary>
    /// Gets whether the native <c>bloqr_validator</c> library could be loaded. Checking this
    /// before calling the validation methods is optional - they degrade to returning
    /// <c>null</c> on their own if the library is unavailable - but it lets a caller skip the
    /// attempt (and any related logging) entirely.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Validates a local filter file's syntax (and, as a side effect inside the native
    /// validator, its at-rest hash against its own separate hash database).
    /// </summary>
    /// <param name="path">Path to the local filter file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The validation result, or <c>null</c> if the native library is unavailable or the call
    /// otherwise could not be completed (logged, never thrown).
    /// </returns>
    Task<SyntaxValidationResult?> ValidateLocalFileAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a remote filter source URL (HTTPS enforcement and other security checks),
    /// optionally verifying its content against an expected SHA-384 hash in-flight.
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <param name="expectedHash">Optional expected SHA-384 hash to verify the downloaded content against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The validation result, or <c>null</c> if the native library is unavailable or the call
    /// otherwise could not be completed (logged, never thrown).
    /// </returns>
    Task<UrlValidationResult?> ValidateRemoteUrlAsync(string url, string? expectedHash = null, CancellationToken cancellationToken = default);
}
