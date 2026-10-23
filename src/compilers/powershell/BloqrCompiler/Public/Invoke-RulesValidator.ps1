using module ..\Classes\RulesValidatorResult.psm1

function Invoke-RulesValidator {
    <#
    .SYNOPSIS
        Runs the `bloqr-validate` CLI against a compiled filter-rules file.

    .DESCRIPTION
        Shells out to the `bloqr-validate` binary (from src/validation/) to
        syntax-check a compiled output file, mirroring the Rust/Python/TypeScript
        bloqr-validator wiring done for #361. Findings are informational: this
        function always returns a RulesValidatorResult describing what the validator
        found - callers decide whether an invalid result should stop a pipeline.

        Gracefully returns $null (with a verbose message) when the `bloqr-validate`
        binary cannot be found, or its output cannot be parsed as JSON - callers should
        treat a $null result as "validation skipped", not "validation failed".

    .PARAMETER Path
        Path to the compiled filter-rules file to validate.

    .PARAMETER HashDatabasePath
        Path to the hash database file used for remote-source hash verification.
        Defaults to a `.hashes.json` file alongside Path.

    .PARAMETER Engine
        Which syntax grammar to validate against: 'dns' (the validator's default,
        rejects cosmetic/browser-only syntax) or 'browser' (accepts cosmetic rules,
        extended CSS, scriptlet injection, and browser-only modifiers). When omitted,
        no --engine flag is passed and the validator's default applies. The
        browser-syntax artifact of a dual-engine compile is validated with 'browser'
        (BLO-459).

    .OUTPUTS
        RulesValidatorResult, or $null if validation was skipped.

    .EXAMPLE
        Invoke-RulesValidator -Path ./output/compiled.txt
    #>
    [CmdletBinding()]
    [OutputType([RulesValidatorResult])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $false)]
        [string]$HashDatabasePath,

        [Parameter(Mandatory = $false)]
        [ValidateSet('dns', 'browser')]
        [string]$Engine
    )

    $binary = Find-RulesValidateBinary
    if (-not $binary) {
        Write-Verbose "bloqr-validate binary not found; skipping syntax validation"
        return $null
    }

    if (-not $HashDatabasePath) {
        $HashDatabasePath = Join-Path (Split-Path -Parent $Path) '.hashes.json'
    }

    $validatorArgs = @('--json', 'file', $Path, '--hash-db', $HashDatabasePath)
    if ($Engine) {
        $validatorArgs += @('--engine', $Engine)
    }

    try {
        $stderrFile = [System.IO.Path]::GetTempFileName()
        try {
            $stdout = & $binary @validatorArgs 2>$stderrFile
            $exitCode = $LASTEXITCODE
            $stderrContent = Get-Content -LiteralPath $stderrFile -Raw -ErrorAction SilentlyContinue
            if (-not [string]::IsNullOrWhiteSpace($stderrContent)) {
                Write-Verbose "bloqr-validate stderr (exit code ${exitCode}): $stderrContent"
            }
        }
        finally {
            Remove-Item -LiteralPath $stderrFile -ErrorAction SilentlyContinue
        }
    }
    catch {
        Write-Verbose "bloqr-validate invocation failed, skipping syntax validation: $_"
        return $null
    }

    if ([string]::IsNullOrWhiteSpace($stdout)) {
        Write-Verbose "bloqr-validate produced no output; skipping syntax validation"
        return $null
    }

    try {
        $parsed = ($stdout -join "`n") | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        Write-Verbose "bloqr-validate produced non-JSON output; skipping syntax validation"
        return $null
    }

    return [RulesValidatorResult]::FromCliOutput($parsed)
}
