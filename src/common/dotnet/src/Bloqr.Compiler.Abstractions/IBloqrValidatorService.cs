namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Wraps <c>bloqr-validator-core</c>'s native FFI surface (<c>bloqr_validator.h</c>) for
/// filter-list syntax and remote-source URL validation. See #264 and
/// <c>src/validation/core/README.md</c>'s ".NET / C#" section for the P/Invoke pattern this
/// implements.
/// </summary>
/// <remarks>
/// This interface stays lenient at the FFI-wrapper level - it returns <c>null</c> rather than
/// throwing when the native library is unavailable or a call otherwise can't complete - but
/// that does NOT mean callers should silently skip the check. Whether "library unavailable" or
/// "returned null" is safe to ignore is a decision each caller must make explicitly:
/// <c>BloqrCompilerService</c>'s compile pipeline (the actual security-relevant checkpoint)
/// fails closed by default via <see cref="CompilerOptions.AllowUnvalidatedOutput"/> - an
/// unavailable/failed validator stops compilation unless that flag is explicitly set. A
/// non-blocking UI surface (e.g. a diagnostics panel reporting native-library availability)
/// may reasonably treat unavailability as informational instead; that's a caller-level choice,
/// not a property of this interface.
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
    /// Validates a local filter file's syntax against a specific engine grammar ("dns" or
    /// "browser"), overriding the default DNS grammar <see cref="ValidateLocalFileAsync(string, CancellationToken)"/>
    /// uses. See <c>docs/adr/0005-browser-syntax-validation-engine.md</c> for what each
    /// engine accepts - in short, "browser" additionally accepts cosmetic rules, extended
    /// CSS, scriptlet injection, and browser-only <c>$</c> modifiers that "dns" rejects.
    /// This is the .NET side of <see cref="CompilerOptions.Engine"/> reaching through to
    /// native validation, per #434's FFI acceptance criterion.
    /// </summary>
    /// <param name="path">Path to the local filter file.</param>
    /// <param name="engine">Either <c>"dns"</c> (default) or <c>"browser"</c>, case-insensitive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The validation result, or <c>null</c> if the native library is unavailable or the call
    /// otherwise could not be completed (logged, never thrown).
    /// </returns>
    Task<SyntaxValidationResult?> ValidateLocalFileAsync(string path, string engine, CancellationToken cancellationToken = default);

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
