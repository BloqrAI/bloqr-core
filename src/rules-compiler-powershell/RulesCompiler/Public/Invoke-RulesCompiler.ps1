using module ..\Classes\CompilerConfiguration.psm1
using module ..\Classes\CompilerResult.psm1

function Invoke-RulesCompiler {
    <#
    .SYNOPSIS
        Compiles AdGuard-style filter rules from a compiler configuration file.

    .DESCRIPTION
        Loads and validates a compiler configuration via the CompilerConfiguration
        class, shells out to hostlist-compiler (native binary or `npx
        @adguard/hostlist-compiler`) to produce the compiled rules file, counts
        rules and computes a SHA-384 hash of the output, runs the rules-validate
        syntax check via Invoke-RulesValidator (informational findings only - see
        that function's help for details), and optionally copies the result into
        a rules directory.

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

    .OUTPUTS
        CompilerResult

    .EXAMPLE
        Invoke-RulesCompiler -ConfigPath ./compiler-config.json -CopyToRules
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
        [string]$RulesDirectory
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

    $compilerCommand = Get-RulesCompilerCommand
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

    # Rules-validator findings are informational and never fail compilation -
    # a $null result just means the validator binary wasn't found, which is a
    # skip, not an error.
    $validation = Invoke-RulesValidator -Path $OutputPath
    if ($validation) {
        if ($validation.IsValid) {
            Write-Verbose "rules-validator: $($validation.ValidRules) valid, $($validation.InvalidRules) invalid rule(s)"
        }
        else {
            foreach ($message in $validation.Messages) {
                Write-Warning "rules-validator: $message"
            }
        }
    }

    $result = [CompilerResult]::CreateSuccess($ruleCount, $OutputPath, $outputHash, $elapsedMs)
    $result.CompilerOutput = $compilerOutput
    $result.ConfigFormat = $config.Format

    if ($CopyToRules) {
        if (-not $RulesDirectory) {
            $repoRoot = $PSScriptRoot
            for ($i = 0; $i -lt 4; $i++) { $repoRoot = Split-Path -Parent $repoRoot }
            $RulesDirectory = Join-Path $repoRoot 'rules'
        }

        New-Item -ItemType Directory -Path $RulesDirectory -Force | Out-Null
        $destPath = Join-Path $RulesDirectory 'adguard_user_filter.txt'
        Copy-Item -LiteralPath $OutputPath -Destination $destPath -Force
        Write-Verbose "Copied compiled rules to: $destPath"
    }

    return $result
}
