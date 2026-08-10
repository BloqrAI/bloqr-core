namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// The outcome of an <see cref="ICompilerConfigGuard"/> load or recovery attempt.
/// </summary>
/// <param name="Success">Whether a valid configuration is available.</param>
/// <param name="Configuration">The loaded/recovered configuration, when <paramref name="Success"/> is <c>true</c>.</param>
/// <param name="Message">
/// A human-readable description of what happened — an error on failure, or a note (e.g. "restored
/// from backup X") when recovery occurred. <c>null</c> on an ordinary successful load.
/// </param>
public sealed record CompilerConfigGuardResult(bool Success, CompilerConfiguration? Configuration, string? Message);
