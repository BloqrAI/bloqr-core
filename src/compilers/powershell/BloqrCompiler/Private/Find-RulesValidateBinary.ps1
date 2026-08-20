function Find-RulesValidateBinary {
    <#
    .SYNOPSIS
        Locates the `bloqr-validate` CLI binary (from src/validation/).

    .DESCRIPTION
        Resolution order: the `RULES_VALIDATE_PATH` environment variable, then `PATH`
        (via Get-Command), then a dev-convenience fallback to the Cargo workspace's
        `target/{release,debug}` output. Returns `$null` (rather than throwing) when not
        found, so callers can degrade gracefully - mirroring the Rust/Python/TypeScript
        `find_rules_validate_binary` equivalents wired for #361.

    .OUTPUTS
        System.String. Path to the binary, or $null if not found.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    if ($env:RULES_VALIDATE_PATH -and (Test-Path -LiteralPath $env:RULES_VALIDATE_PATH -PathType Leaf)) {
        return $env:RULES_VALIDATE_PATH
    }

    $onPath = Get-Command -Name 'bloqr-validate' -ErrorAction SilentlyContinue
    if ($onPath) {
        return $onPath.Source
    }

    $binaryName = if ($IsWindows) { 'bloqr-validate.exe' } else { 'bloqr-validate' }

    # Dev-convenience fallback: Private/ -> BloqrCompiler/ -> powershell/ -> compilers/ -> src/ -> repo root
    $repoRoot = $PSScriptRoot
    for ($i = 0; $i -lt 5; $i++) {
        $repoRoot = Split-Path -Parent $repoRoot
    }

    foreach ($profileName in @('release', 'debug')) {
        $candidate = Join-Path $repoRoot 'target' $profileName $binaryName
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    return $null
}
