namespace Bloqr.Compiler.Core.Services;

/// <summary>
/// P/Invoke declarations for <c>rules-validator-core</c>'s native FFI surface
/// (<c>rules_validator.h</c>, generated from <c>src/ffi.rs</c>). See that header and
/// <c>src/rules-validator/README.md</c>'s ".NET / C#" section for the documented pattern this
/// follows verbatim - an opaque handle plus JSON-encoded results, so there's no per-field
/// struct marshaling to keep in sync with the Rust side.
/// </summary>
internal static class RulesValidatorNativeMethods
{
    private const string LibraryName = "rules_validator";

    [DllImport(LibraryName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr rules_validator_new(string? configJson);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void rules_validator_free(IntPtr validator);

    [DllImport(LibraryName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rules_validator_validate_local_file(
        IntPtr validator, string path, out IntPtr outResultJson);

    [DllImport(LibraryName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rules_validator_validate_remote_url(
        IntPtr validator, string url, string? expectedHash, out IntPtr outResultJson);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr rules_validator_version();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void rules_validator_free_string(IntPtr s);
}
