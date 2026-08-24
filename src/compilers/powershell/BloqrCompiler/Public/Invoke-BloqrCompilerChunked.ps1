using module ..\Classes\CompilerConfiguration.psm1
using module ..\Classes\CompilerResult.psm1

function Invoke-BloqrCompilerChunked {
    <#
    .SYNOPSIS
        Compiles AdGuard-style filter rules using chunked parallel compilation.

    .DESCRIPTION
        Splits the configuration's sources into up to -MaxParallel chunks (source
        chunking strategy, mirroring the Rust/.NET/TypeScript/Python wrappers'
        chunking modules), compiles each chunk in parallel via
        `ForEach-Object -Parallel` - each chunk shelling out to the same
        `deno run jsr:@bloqr/compiler-core/cli` invocation Invoke-BloqrCompiler
        itself uses, so unlike the Rust/.NET/Python wrappers (see #424) there is no
        divergent-compiler risk here - then merges the chunk outputs and
        deduplicates while preserving order, matching the merge behavior of the
        other wrappers' chunking implementations.

        Unlike Invoke-BloqrCompiler, this does not run the mandatory bloqr-validate
        syntax check on each chunk's output - none of the other four wrappers'
        chunked/parallel compilation paths do either, since the merged result is
        what ultimately matters and re-validating every intermediate chunk would
        be redundant. Validate the merged output separately (e.g. via
        Invoke-RulesValidator) if needed.

        Chunked compilation doesn't support the multi-engine/dual-artifact path yet
        (#439, matching the Rust/.NET/Python/TypeScript precedent of deferring this)
        - there are no -Engine/-BrowserOutputPath parameters here, and each chunk
        compiles through the default (config-driven) engine resolution only. The
        underlying configuration's `defaultEngine` is still threaded through to each
        chunk by Split-BloqrCompilerConfiguration, so per-source engine resolution
        within a chunk behaves the same as it would unchunked.

        Only JSON configuration files are supported, matching Invoke-BloqrCompiler.

    .PARAMETER ConfigPath
        Path to the compiler configuration file.

    .PARAMETER OutputPath
        Path to write the merged compiled rules file. Defaults to a timestamped
        file under an `output/` directory next to the configuration file.

    .PARAMETER MaxParallel
        Maximum number of chunks to compile in parallel. Defaults to the number of
        logical processors, capped at 8.

    .OUTPUTS
        CompilerResult

    .EXAMPLE
        Invoke-BloqrCompilerChunked -ConfigPath ./compiler-config.json -MaxParallel 4
    #>
    [CmdletBinding()]
    [OutputType([CompilerResult])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConfigPath,

        [Parameter(Mandatory = $false)]
        [string]$OutputPath,

        [Parameter(Mandatory = $false)]
        [int]$MaxParallel = [Math]::Min(8, [Environment]::ProcessorCount)
    )

    $startTime = Get-Date

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

    # Resolve just the executable and base arguments here (no -ConfigPath/-OutputPath) -
    # each chunk gets its own --config/--output appended below, inside the parallel
    # block. Multi-engine/dual-artifact support is deferred for chunked compilation
    # (#439), so -Engine/-BrowserOutputPath are never passed.
    $compilerCommand = Get-BloqrCompilerCommand
    if (-not $compilerCommand) {
        return [CompilerResult]::CreateFailure('deno not found. Install from https://deno.com, or see the module README for details.')
    }

    if (-not $OutputPath) {
        $timestamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')
        $OutputPath = Join-Path (Split-Path -Parent $config.ConfigPath) 'output' "compiled-chunked-$timestamp.txt"
    }

    $outputDir = Split-Path -Parent $OutputPath
    if ($outputDir -and -not (Test-Path -LiteralPath $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }

    $effectiveMaxParallel = [Math]::Max(1, $MaxParallel)
    # Split-BloqrCompilerConfiguration uses Write-Output -NoEnumerate to guarantee an
    # array result for any chunk count (including 1) - do NOT wrap this call in @(...)
    # here, since doing so would re-wrap that already-correct array as a single nested
    # element instead of collecting it directly.
    $chunks = Split-BloqrCompilerConfiguration -Config $config -MaxParallel $effectiveMaxParallel

    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "bloqr-chunks-$([guid]::NewGuid())"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

    try {
        $chunkInfos = @(for ($i = 0; $i -lt $chunks.Count; $i++) {
            $chunkConfigPath = Join-Path $tempDir "chunk-$i-config.json"
            ($chunks[$i] | ConvertTo-Json -Depth 10) | Set-Content -Path $chunkConfigPath
            [PSCustomObject]@{
                Index      = $i
                ConfigPath = $chunkConfigPath
                OutputPath = Join-Path $tempDir "chunk-$i-output.txt"
            }
        })

        $executable = $compilerCommand.Executable
        $baseArguments = $compilerCommand.Arguments

        $chunkResults = $chunkInfos | ForEach-Object -Parallel {
            $info = $_
            $exe = $using:executable
            $procArgs = $using:baseArguments + @('--config', $info.ConfigPath, '--output', $info.OutputPath)
            $output = & $exe @procArgs 2>&1 | Out-String
            $exitCode = $LASTEXITCODE
            [PSCustomObject]@{
                Index      = $info.Index
                ExitCode   = $exitCode
                Output     = $output
                OutputPath = $info.OutputPath
                Success    = ($exitCode -eq 0) -and (Test-Path -LiteralPath $info.OutputPath)
            }
        } -ThrottleLimit $effectiveMaxParallel

        $failedChunks = @($chunkResults | Where-Object { -not $_.Success })
        if ($failedChunks.Count -gt 0) {
            $chunkErrors = $failedChunks | ForEach-Object { "Chunk $($_.Index + 1): exit $($_.ExitCode): $($_.Output.Trim())" }
            return [CompilerResult]::CreateFailure("$($failedChunks.Count) chunk(s) failed: $($chunkErrors -join '; ')")
        }

        $mergedLines = [System.Collections.Generic.List[string]]::new()
        $seenRules = [System.Collections.Generic.HashSet[string]]::new()
        foreach ($chunkResult in ($chunkResults | Sort-Object Index)) {
            foreach ($line in (Get-Content -LiteralPath $chunkResult.OutputPath)) {
                $trimmed = $line.Trim()
                if (-not $trimmed -or $trimmed.StartsWith('!') -or $trimmed.StartsWith('#')) {
                    $mergedLines.Add($line)
                    continue
                }
                if ($seenRules.Add($line)) {
                    $mergedLines.Add($line)
                }
            }
        }

        Set-Content -Path $OutputPath -Value $mergedLines

        $ruleCount = Get-RuleCount -Path $OutputPath
        $outputHash = Get-RulesFileHash -Path $OutputPath
        $elapsedMs = [long](((Get-Date) - $startTime).TotalMilliseconds)

        $result = [CompilerResult]::CreateSuccess($ruleCount, $OutputPath, $outputHash, $elapsedMs)
        $result.ConfigFormat = $config.Format
        return $result
    }
    finally {
        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
