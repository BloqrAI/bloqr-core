namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// The Dashboard's embeddable-library API boundary: every operation the interactive menus and
/// the CLI surface expose, available as plain async calls with no dependency on Spectre.Console
/// or terminal rendering. Per the epic: "the WPF application will have the Dashboard console
/// application embedded as a library... Dashboard should expose all inner operations via CLI
/// switches, APIs, etc." (#271). A future .NET MAUI host depends on this interface (and the rest of
/// <c>Bloqr.Dashboard.Abstractions</c>/<c>Bloqr.Dashboard.Core</c>) instead of anything in
/// <c>Bloqr.Dashboard.Console</c>.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Compiles filter rules from the given compiler config, identical to what
    /// <c>CompileMenuService</c>'s interactive "Compile using a specific config file" action does
    /// minus the live progress display.
    /// </summary>
    /// <remarks>
    /// <para><b>Dual-engine shape (#440, Wave 3 of epic #432):</b> this method still returns a
    /// single <see cref="CompilerResult"/> rather than gaining a "per engine" result type or a
    /// second overload. <see cref="CompilerResult"/> already carries both artifacts after #436 -
    /// the primary/DNS fields (<see cref="CompilerResult.OutputPath"/>,
    /// <see cref="CompilerResult.OutputHash"/>, <see cref="CompilerResult.RuleCount"/>) plus the
    /// browser ones (<see cref="CompilerResult.BrowserOutputPath"/>,
    /// <see cref="CompilerResult.BrowserOutputHash"/>, <see cref="CompilerResult.BrowserRuleCount"/>,
    /// populated only when the config mixed both source types). A future MAUI/WPF host can render
    /// both artifacts - with their independent hashes and paths - straight off one result, with no
    /// need to correlate two separate calls or two result objects.
    /// </para>
    /// <para>
    /// What <i>does</i> change is the input: this gained an optional <paramref name="engine"/>
    /// and <paramref name="browserOutputPath"/> parameter, mirroring
    /// <c>Bloqr.Compiler.Abstractions.CompilerOptions.Engine</c> and <c>.BrowserOutputPath</c>
    /// exactly (same type, same semantics) so a caller can force a single engine or override
    /// where the browser artifact lands, the same way <c>Bloqr.Compiler.Dotnet.Console</c>'s own
    /// <c>--engine</c>/<c>--browser-output</c> flags do (#436). Both are forwarded to
    /// <c>IBloqrCompilerService.RunAsync(CompilerOptions, ...)</c> unchanged; leaving them
    /// <c>null</c> preserves the pre-#440 "auto" behavior byte-for-byte, so this is additive, not
    /// a breaking change to existing callers.
    /// </para>
    /// </remarks>
    /// <param name="compilerConfigPath">Path to the compiler config file.</param>
    /// <param name="engine">
    /// Which compilation engine to force: <c>"dns"</c> or <c>"browser"</c>. <c>null</c> or
    /// <c>"auto"</c> (the default) detects the engine per source.
    /// </param>
    /// <param name="browserOutputPath">
    /// Overrides the output path for the browser-syntax artifact of a mixed-engine config.
    /// <c>null</c> uses the compiler's own derived default. Ignored for single-engine configs.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CompilerResult> CompileAsync(
        string compilerConfigPath,
        string? engine = null,
        string? browserOutputPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a compiler config file without compiling it.
    /// </summary>
    /// <remarks>
    /// No engine parameter is needed here (#440): validation checks a config's structure and
    /// sources up front, independent of which engine(s) a later <see cref="CompileAsync"/> call
    /// would route those sources through - a config with mixed DNS/browser sources validates the
    /// same way regardless of engine selection, since engine selection only affects compilation
    /// routing, not config-level validity.
    /// </remarks>
    /// <param name="compilerConfigPath">Path to the compiler config file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ValidationResult> ValidateCompilerConfigAsync(string compilerConfigPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the names of every profile defined in the Dashboard's own configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<string>> ListProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the currently active profile's name, or <c>null</c> if none is active.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string?> GetActiveProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a named profile, persisting the change to the Dashboard's own configuration.
    /// </summary>
    /// <param name="profileName">The profile name to activate. Must already exist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when <paramref name="profileName"/> does not match a known profile.</exception>
    Task ActivateProfileAsync(string profileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the compiler config path(s) registered to the active profile, so a caller can
    /// compile "whatever the active profile points at" without separately loading the Dashboard
    /// configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active profile's compiler config paths, or an empty list if no profile is active.</returns>
    Task<IReadOnlyList<string>> GetActiveProfileCompilerConfigsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Benchmarks real .NET compiler performance (chunked vs unchunked) against the canned
    /// <c>benchmarks/data/</c> datasets, identical to what the <c>Bloqr.Compiler.Dotnet.Console</c>
    /// <c>--benchmark</c> CLI mode does (#417) - not a synthetic simulation. Backs the
    /// Diagnostics menu's benchmark action (#423). Only benchmarks the .NET compiler; unlike the
    /// root Launcher, the Dashboard doesn't shell out to the other four language compilers.
    /// </summary>
    /// <param name="size">A canned dataset size ("small", "medium", "large", "xlarge"), or "all".</param>
    /// <param name="dataDir">
    /// Directory containing the canned datasets. Auto-discovered from the current directory when
    /// <c>null</c>.
    /// </param>
    /// <param name="numSources">Number of identical duplicated sources to compile per size.</param>
    /// <param name="maxParallel">Maximum parallel chunk workers for the chunked run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One result per benchmarked size.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="size"/> is not a recognized size or "all".
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// No data directory was given or could be found (e.g. a binary-only release checkout).
    /// </exception>
    Task<List<BenchmarkRunResult>> RunBenchmarkAsync(
        string size = "all",
        string? dataDir = null,
        int numSources = 4,
        int maxParallel = 4,
        CancellationToken cancellationToken = default);
}
