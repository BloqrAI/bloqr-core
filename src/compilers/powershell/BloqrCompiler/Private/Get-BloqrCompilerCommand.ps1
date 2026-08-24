# Shared base arguments for invoking @bloqr/compiler-core (published on JSR) via Deno.
# Module-scoped so Invoke-BloqrCompilerChunked's -Parallel blocks (which run in separate
# runspaces without this module imported) can capture them via $using: without duplicating
# the permission list in two places.
$script:BloqrDenoPermissions = @('--allow-read', '--allow-write', '--allow-env', '--allow-net', '--allow-run')
$script:BloqrJsrSpecifier = 'jsr:@bloqr/compiler-core/cli'

function Get-BloqrCompilerCommand {
    <#
    .SYNOPSIS
        Resolves how to invoke @bloqr/compiler-core (via Deno/JSR).

    .DESCRIPTION
        Resolves `deno` on PATH and builds the argument list for
        `deno run --allow-read --allow-write --allow-env --allow-net --allow-run
        jsr:@bloqr/compiler-core/cli`, the same Deno + JSR resolver used by the
        Rust/.NET/Python/TypeScript wrappers (see #424, #437, #438). This module
        previously resolved to `hostlist-compiler`/`npx @adguard/hostlist-compiler`;
        that resolver has been retired in favor of this one so PowerShell gains
        engine (`-Engine`)/dual-artifact (`-BrowserOutputPath`) support like its
        siblings - see #439 and the module README's "Engine/output resolution
        decision" section.

        Returns $null (rather than throwing) when `deno` isn't found, mirroring the
        prior resolver's "return null, don't throw" contract so callers can produce
        their own actionable error message.

        `-ConfigPath`/`-OutputPath` are optional so this function can also be used
        to resolve just the executable and its base arguments (e.g. by
        Invoke-BloqrCompilerChunked, which appends per-chunk --config/--output
        itself); when supplied, `--config`/`--output` are appended in that order,
        followed by `--engine` (only when set and not "auto", case-insensitive) and
        `--browser-output` (only when `-BrowserOutputPath` is explicitly passed) -
        so the default command line for an all-DNS, single-artifact compile is
        exactly what it would have been had engine support never been added.

    .PARAMETER ConfigPath
        Path to the compiler configuration file. When supplied, appends
        `--config <ConfigPath>`.

    .PARAMETER OutputPath
        Path to write the compiled (DNS/primary) rules file. When supplied, appends
        `--output <OutputPath>`.

    .PARAMETER Engine
        Compilation engine override ("dns" or "browser"). Omitted entirely (not
        even an empty `--engine`) when $null, empty, or "auto" (case-insensitive) -
        that is the CLI's own default, so omitting it keeps the command line
        identical to the no-engine-specified case.

    .PARAMETER BrowserOutputPath
        Explicit output path for the browser-syntax artifact. Only emitted as
        `--browser-output <BrowserOutputPath>` when this parameter is explicitly
        passed with a non-empty value - the default (derived) browser-output path
        used for post-compile detection is never placed on the command line unless
        the caller asked for it there.

    .OUTPUTS
        Hashtable with `Executable` and `Arguments` keys, or $null if not found.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory = $false)]
        [string]$ConfigPath,

        [Parameter(Mandatory = $false)]
        [string]$OutputPath,

        [Parameter(Mandatory = $false)]
        [string]$Engine,

        [Parameter(Mandatory = $false)]
        [string]$BrowserOutputPath
    )

    $deno = Get-Command -Name 'deno' -ErrorAction SilentlyContinue
    if (-not $deno) {
        return $null
    }

    $arguments = @('run') + $script:BloqrDenoPermissions + @($script:BloqrJsrSpecifier)

    if ($ConfigPath) {
        $arguments += @('--config', $ConfigPath)
    }
    if ($OutputPath) {
        $arguments += @('--output', $OutputPath)
    }

    # "auto" is the CLI's own default, so omitting it (like an unset engine) keeps
    # the command line identical to the no-engine-specified case - the
    # byte-identical-output guarantee's command-line analogue (see #437/#438).
    if ($Engine -and $Engine.ToLowerInvariant() -ne 'auto') {
        $arguments += @('--engine', $Engine)
    }

    if ($PSBoundParameters.ContainsKey('BrowserOutputPath') -and $BrowserOutputPath) {
        $arguments += @('--browser-output', $BrowserOutputPath)
    }

    return @{ Executable = $deno.Source; Arguments = $arguments }
}
