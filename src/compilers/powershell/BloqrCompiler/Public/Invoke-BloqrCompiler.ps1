using module ..\Classes\CompilerConfiguration.psm1
using module ..\Classes\CompilerResult.psm1

function Invoke-BloqrCompiler {
    <#
    .SYNOPSIS
        Compiles AdGuard-style filter rules from a compiler configuration file.

    .DESCRIPTION
        Loads and validates a compiler configuration via the CompilerConfiguration
        class, shells out to @bloqr/compiler-core (via `deno run jsr:@bloqr/compiler-core/cli`
        - see #439's "Engine/output resolution decision" in the module README) to
        produce the compiled rules file, counts rules and computes a SHA-384 hash of
        the output, runs the bloqr-validate syntax check via Invoke-RulesValidator,
        and optionally copies the result into a rules directory.

        A configuration that mixes DNS and browser-syntax sources (via per-source
        `engine`/top-level `defaultEngine`) produces a second, browser-syntax
        artifact. After the DNS artifact is compiled and validated, this function
        checks (by file existence, not by re-parsing the configuration) whether a
        browser artifact was written to -BrowserOutputPath (or, if that wasn't
        passed, the default `.browser.txt`-suffixed path derived from -OutputPath -
        see Get-BloqrBrowserOutputPath). When present, it is hashed, rule-counted,
        and run through the same mandatory bloqr-validate check as the DNS artifact.
        The already-published DNS artifact is never rolled back if the browser
        artifact fails validation - the returned error names the DNS artifact
        explicitly so the caller knows it is still intact.

        The bloqr-validate check is fail-closed by default for both artifacts: a
        missing/failed validator, or output it reports as invalid, makes this
        function return a failure CompilerResult - "could not validate" is not
        treated the same as "no findings". Pass -AllowUnvalidatedOutput to
        explicitly opt out (not recommended outside deliberate debugging); pass
        -FailOnWarnings to also fail on informational findings reported alongside
        otherwise-valid output. Per #439's interim note, this fail-closed default
        applies identically to the browser artifact until #434 (native
        browser-syntax validation) lands.

        Only JSON configuration files are compiled today - YAML is parsed by
        CompilerConfiguration for inspection purposes but isn't wired into the
        compiler invocation here, and TOML isn't supported by CompilerConfiguration
        at all yet. Both fail with a clear error pointing at JSON rather than
        silently misbehaving.

    .PARAMETER ConfigPath
        Path to the compiler configuration file. Defaults to
        $env:ADGUARD_COMPILER_CONFIG if set.

    .PARAMETER OutputPath
        Path to write the compiled (DNS/primary) rules file. Defaults to
        $env:ADGUARD_COMPILER_OUTPUT if set, otherwise a timestamped file under
        an `output/` directory next to the configuration file.

    .PARAMETER Engine
        Compilation engine override passed through to @bloqr/compiler-core as
        `--engine` ("dns" or "browser"). Omit (or pass "auto") to use the
        configuration's own defaultEngine/per-source engine resolution - the
        default single-artifact, all-DNS command line stays byte-identical to
        pre-feature behavior when this is omitted.

    .PARAMETER BrowserOutputPath
        Explicit output path for the browser-syntax artifact, passed through as
        `--browser-output`. When omitted, the browser artifact (if any) is expected
        at the path Get-BloqrBrowserOutputPath derives from -OutputPath, and that
        default is used for post-compile detection only - it is never placed on
        the command line unless this parameter is passed explicitly.

    .PARAMETER CopyToRules
        Copy the compiled output(s) into the rules directory after a successful
        compile. Copies both the DNS and browser artifacts when a browser artifact
        was produced.

    .PARAMETER RulesDirectory
        Destination directory for -CopyToRules. Defaults to a `rules/` directory
        at the repository root.

    .PARAMETER AllowUnvalidatedOutput
        Explicit opt-out of the mandatory bloqr-validate syntax check, for both the
        DNS artifact and (when present) the browser artifact. Security-relevant:
        leave this off in production. When off (the default), a missing/failed
        validator or invalid output fails compilation; there is no silent skip. Use
        only for deliberate debugging of unvalidated output.

    .PARAMETER FailOnWarnings
        Also fail compilation when bloqr-validate reports informational findings
        alongside otherwise-valid output, for either artifact.

    .OUTPUTS
        CompilerResult

    .EXAMPLE
        Invoke-BloqrCompiler -ConfigPath ./compiler-config.json -CopyToRules

    .EXAMPLE
        Invoke-BloqrCompiler -ConfigPath ./compiler-config.json -Engine browser -BrowserOutputPath ./output/rules.browser.txt
    #>
    [CmdletBinding()]
    [OutputType([CompilerResult])]
    param(
        [Parameter(Mandatory = $false)]
        [string]$ConfigPath = $env:ADGUARD_COMPILER_CONFIG,

        [Parameter(Mandatory = $false)]
        [string]$OutputPath = $env:ADGUARD_COMPILER_OUTPUT,

        [Parameter(Mandatory = $false)]
        [string]$Engine,

        [Parameter(Mandatory = $false)]
        [string]$BrowserOutputPath,

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

    # --browser-output is only threaded through to the command line when the caller
    # explicitly asked for it, so the default command line for an all-DNS,
    # single-artifact config stays byte-identical to pre-feature behavior. The
    # explicit-or-derived path is still needed below for post-compile detection.
    $explicitBrowserOutputRequested = $PSBoundParameters.ContainsKey('BrowserOutputPath') -and $BrowserOutputPath
    $resolvedBrowserOutputPath = if ($explicitBrowserOutputRequested) { $BrowserOutputPath } else { Get-BloqrBrowserOutputPath -OutputPath $OutputPath }

    $commandParams = @{
        ConfigPath = $config.ConfigPath
        OutputPath = $OutputPath
        Engine     = $Engine
    }
    if ($explicitBrowserOutputRequested) {
        $commandParams.BrowserOutputPath = $BrowserOutputPath
    }

    $compilerCommand = Get-BloqrCompilerCommand @commandParams
    if (-not $compilerCommand) {
        return [CompilerResult]::CreateFailure('deno not found. Install from https://deno.com, or see the module README for details.')
    }

    $compilerOutput = & $compilerCommand.Executable @($compilerCommand.Arguments) 2>&1 | Out-String
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        $result = [CompilerResult]::CreateFailure("@bloqr/compiler-core exited with code ${exitCode}: $compilerOutput")
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

    # A browser-syntax artifact only exists when the configuration actually mixed
    # engines (or -Engine browser was forced) - detected by file presence, not by
    # re-parsing the configuration, since the compiler CLI is the single source of
    # truth for whether a source resolved to the browser engine. The DNS artifact
    # above is already published at this point and is never rolled back if the
    # browser artifact below fails validation.
    if (Test-Path -LiteralPath $resolvedBrowserOutputPath -PathType Leaf) {
        $browserRuleCount = Get-RuleCount -Path $resolvedBrowserOutputPath
        $browserHash = Get-RulesFileHash -Path $resolvedBrowserOutputPath

        $browserValidation = Invoke-RulesValidator -Path $resolvedBrowserOutputPath
        if (-not $browserValidation) {
            if (-not $AllowUnvalidatedOutput) {
                return [CompilerResult]::CreateFailure(
                    "bloqr-validate could not run against the browser-syntax artifact at '$resolvedBrowserOutputPath' " +
                    "(the DNS artifact was already published successfully at '$OutputPath'). " +
                    'Pass -AllowUnvalidatedOutput to bypass this check (not recommended).'
                )
            }
            Write-Warning 'bloqr-validate could not run against the browser-syntax artifact; proceeding unvalidated because -AllowUnvalidatedOutput was set.'
        }
        elseif ($browserValidation.IsValid) {
            Write-Verbose "bloqr-validator (browser artifact): $($browserValidation.ValidRules) valid, $($browserValidation.InvalidRules) invalid rule(s)"
            if ($FailOnWarnings -and $browserValidation.Messages.Count -gt 0 -and -not $AllowUnvalidatedOutput) {
                $messageText = $browserValidation.Messages -join '; '
                return [CompilerResult]::CreateFailure(
                    "bloqr-validator reported warnings for the browser-syntax artifact (-FailOnWarnings is set; " +
                    "the DNS artifact was already published successfully at '$OutputPath'): $messageText"
                )
            }
            foreach ($message in $browserValidation.Messages) {
                Write-Warning "bloqr-validator (browser artifact): $message"
            }
        }
        else {
            foreach ($message in $browserValidation.Messages) {
                Write-Warning "bloqr-validator (browser artifact): $message"
            }
            if (-not $AllowUnvalidatedOutput) {
                $messageText = if ($browserValidation.Messages.Count -gt 0) { $browserValidation.Messages -join '; ' } else { "$($browserValidation.InvalidRules) invalid rule(s) of $($browserValidation.ValidRules + $browserValidation.InvalidRules)" }
                return [CompilerResult]::CreateFailure(
                    "Browser-syntax artifact at '$resolvedBrowserOutputPath' failed bloqr-validator syntax validation " +
                    "(the DNS artifact was already published successfully at '$OutputPath'): $messageText"
                )
            }
        }

        $result.BrowserOutputPath = $resolvedBrowserOutputPath
        $result.BrowserOutputHash = $browserHash
        $result.BrowserRuleCount = $browserRuleCount
    }

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

        if ($result.BrowserOutputPath) {
            $browserDestPath = Join-Path $RulesDirectory 'adguard_user_filter.browser.txt'
            Copy-Item -LiteralPath $result.BrowserOutputPath -Destination $browserDestPath -Force
            Write-Verbose "Copied compiled browser-syntax rules to: $browserDestPath"
        }
    }

    return $result
}
