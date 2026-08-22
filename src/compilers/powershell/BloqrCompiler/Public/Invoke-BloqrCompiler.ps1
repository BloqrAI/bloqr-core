using module ..\Classes\CompilerConfiguration.psm1
using module ..\Classes\CompilerResult.psm1

function Invoke-BloqrCompiler {
    <#
    .SYNOPSIS
        Compiles AdGuard-style filter rules from a compiler configuration file.

    .DESCRIPTION
        Loads and validates a compiler configuration via the CompilerConfiguration
        class, shells out to hostlist-compiler (native binary or `npx
        @adguard/hostlist-compiler`) to produce the compiled rules file, counts
        rules and computes a SHA-384 hash of the output, runs the bloqr-validate
        syntax check via Invoke-RulesValidator, and optionally copies the result
        into a rules directory.

        The bloqr-validate check is fail-closed by default: a missing/failed
        validator, or output it reports as invalid, makes this function return a
        failure CompilerResult - "could not validate" is not treated the same as
        "no findings". Pass -AllowUnvalidatedOutput to explicitly opt out (not
        recommended outside deliberate debugging); pass -FailOnWarnings to also
        fail on informational findings reported alongside otherwise-valid output.

        Only JSON configuration files are compiled today - YAML is parsed by
        CompilerConfiguration for inspection purposes but isn't wired into the
        hostlist-compiler invocation here, and TOML isn't supported by
        CompilerConfiguration at all yet. Both fail with a clear error pointing at
        JSON rather than silently misbehaving.

    .PARAMETER ConfigPath
        Path to the compiler configuration file. Defaults to
        $env:ADGUARD_COMPILER_CONFIG if set.

    .PARAMETER OutputPath
        Path to write the compiled rules file. Defaults to
        $env:ADGUARD_COMPILER_OUTPUT if set, otherwise a timestamped file under
        an `output/` directory next to the configuration file.

    .PARAMETER CopyToRules
        Copy the compiled output into the rules directory after a successful
        compile.

    .PARAMETER RulesDirectory
        Destination directory for -CopyToRules. Defaults to a `rules/` directory
        at the repository root.

    .PARAMETER AllowUnvalidatedOutput
        Explicit opt-out of the mandatory bloqr-validate syntax check. Security-
        relevant: leave this off in production. When off (the default), a missing/
        failed validator or invalid output fails compilation; there is no silent
        skip. Use only for deliberate debugging of unvalidated output.

    .PARAMETER FailOnWarnings
        Also fail compilation when bloqr-validate reports informational findings
        alongside otherwise-valid output.

    .OUTPUTS
        CompilerResult

    .EXAMPLE
        Invoke-BloqrCompiler -ConfigPath ./compiler-config.json -CopyToRules
    #>
    [CmdletBinding()]
    [OutputType([CompilerResult])]
    param(
        [Parameter(Mandatory = $false)]
        [string]$ConfigPath = $env:ADGUARD_COMPILER_CONFIG,

        [Parameter(Mandatory = $false)]
        [string]$OutputPath = $env:ADGUARD_COMPILER_OUTPUT,

        [Parameter(Mandatory = $false)]
        [switch]$CopyToRules,

        [Parameter(Mandatory = $false)]
        [string]$RulesDirectory,

        [Parameter(Mandatory = $false)]
        [switch]$AllowUnvalidatedOutput,

        [Parameter(Mandatory = $false)]
        [switch]$FailOnWarnings
    )

    $startTime = Get-Date

    if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
        return [CompilerResult]::CreateFailure('No configuration path specified. Pass -ConfigPath or set $env:ADGUARD_COMPILER_CONFIG.')
    }

    try {
        $config = [CompilerConfiguration]::new($ConfigPath)
        $config.Validate()
    }
    catch {
        return [CompilerResult]::CreateFailure("Configuration error: $_")
    }

    if ($config.Format -ne 'json') {
        return [CompilerResult]::CreateFailure(
            "Only JSON configuration files can be compiled today (got '$($config.Format)'). Convert your configuration to JSON."
        )
    }

    if (-not $OutputPath) {
        $timestamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')
        $OutputPath = Join-Path (Split-Path -Parent $config.ConfigPath) 'output' "compiled-$timestamp.txt"
    }

    $outputDir = Split-Path -Parent $OutputPath
    if ($outputDir -and -not (Test-Path -LiteralPath $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }

    $compilerCommand = Get-BloqrCompilerCommand
    if (-not $compilerCommand) {
        return [CompilerResult]::CreateFailure('hostlist-compiler not found. Install with: npm install -g @adguard/hostlist-compiler')
    }

    $compilerArgs = $compilerCommand.Arguments + @('--config', $config.ConfigPath, '--output', $OutputPath)
    $compilerOutput = & $compilerCommand.Executable @compilerArgs 2>&1 | Out-String
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        $result = [CompilerResult]::CreateFailure("hostlist-compiler exited with code ${exitCode}: $compilerOutput")
        $result.CompilerOutput = $compilerOutput
        $result.ConfigFormat = $config.Format
        return $result
    }

    $ruleCount = Get-RuleCount -Path $OutputPath
    $outputHash = Get-RulesFileHash -Path $OutputPath
    $elapsedMs = [long](((Get-Date) - $startTime).TotalMilliseconds)

    # Mandatory rules-validator syntax check - fail-closed by default. A $null
    # result means the validator couldn't run at all (binary not found, invocation
    # failed, output unparseable) - that tells us nothing about the output's
    # safety, so it is NOT treated as "no findings" unless -AllowUnvalidatedOutput
    # is set.
    $validation = Invoke-RulesValidator -Path $OutputPath
    if (-not $validation) {
        if (-not $AllowUnvalidatedOutput) {
            return [CompilerResult]::CreateFailure(
                'bloqr-validate could not run against the compiled output (binary not found or invocation failed). ' +
                'Pass -AllowUnvalidatedOutput to bypass this check (not recommended).'
            )
        }
        Write-Warning 'bloqr-validate could not run; proceeding unvalidated because -AllowUnvalidatedOutput was set.'
    }
    elseif ($validation.IsValid) {
        Write-Verbose "bloqr-validator: $($validation.ValidRules) valid, $($validation.InvalidRules) invalid rule(s)"
        if ($FailOnWarnings -and $validation.Messages.Count -gt 0 -and -not $AllowUnvalidatedOutput) {
            $messageText = $validation.Messages -join '; '
            return [CompilerResult]::CreateFailure("bloqr-validator reported warnings (-FailOnWarnings is set): $messageText")
        }
        foreach ($message in $validation.Messages) {
            Write-Warning "bloqr-validator: $message"
        }
    }
    else {
        foreach ($message in $validation.Messages) {
            Write-Warning "bloqr-validator: $message"
        }
        if (-not $AllowUnvalidatedOutput) {
            $messageText = if ($validation.Messages.Count -gt 0) { $validation.Messages -join '; ' } else { "$($validation.InvalidRules) invalid rule(s) of $($validation.ValidRules + $validation.InvalidRules)" }
            return [CompilerResult]::CreateFailure("Output file failed bloqr-validator syntax validation: $messageText")
        }
    }

    $result = [CompilerResult]::CreateSuccess($ruleCount, $OutputPath, $outputHash, $elapsedMs)
    $result.CompilerOutput = $compilerOutput
    $result.ConfigFormat = $config.Format

    if ($CopyToRules) {
        if (-not $RulesDirectory) {
            $repoRoot = $PSScriptRoot
            for ($i = 0; $i -lt 5; $i++) { $repoRoot = Split-Path -Parent $repoRoot }
            $RulesDirectory = Join-Path $repoRoot 'rules'
        }

        New-Item -ItemType Directory -Path $RulesDirectory -Force | Out-Null
        $destPath = Join-Path $RulesDirectory 'adguard_user_filter.txt'
        Copy-Item -LiteralPath $OutputPath -Destination $destPath -Force
        Write-Verbose "Copied compiled rules to: $destPath"
    }

    return $result
}
