function Invoke-BloqrCompilerBenchmark {
    <#
    .SYNOPSIS
        Benchmarks real compilation performance, chunked vs unchunked.

    .DESCRIPTION
        Compiles the canned benchmarks/data/{small,medium,large,xlarge}.txt datasets
        through the real Invoke-BloqrCompiler (unchunked) and Invoke-BloqrCompilerChunked
        (chunked) pipelines - not a simulation - once unchunked and once chunked, and
        reports the actual elapsed time for both. Part of epic #415's per-compiler
        benchmark work; see that issue's other sub-issues for the equivalent
        subcommand/switch in each of the other four language wrappers.

        Unlike the Rust/.NET/Python wrappers (see #424), both paths here shell out to
        the exact same hostlist-compiler/npx binary, so there is no divergent-compiler
        risk - any timing delta reflects chunking overhead alone.

        Both runs cover the same total workload (-Sources identical copies of the
        dataset file, one per chunk), so chunking strategy is the only intended
        variable.

    .PARAMETER Size
        Dataset size to benchmark: small, medium, large, xlarge, or all (default: all).

    .PARAMETER DataDirectory
        Directory containing the canned benchmark data. Auto-discovered if omitted.

    .PARAMETER Sources
        Number of identical duplicated sources for the chunked run (default: 4).

    .PARAMETER MaxParallel
        Max parallel workers for the chunked run. Defaults to the number of logical
        processors, capped at 8.

    .PARAMETER AsJson
        Emit a JSON string instead of returning result objects (for the root
        comparison script - see benchmarks/).

    .OUTPUTS
        PSCustomObject[] (one per benchmarked size), or a JSON string when -AsJson
        is set.

    .EXAMPLE
        Invoke-BloqrCompilerBenchmark -Size small

    .EXAMPLE
        Invoke-BloqrCompilerBenchmark -Size large -Sources 8 -MaxParallel 8

    .EXAMPLE
        Invoke-BloqrCompilerBenchmark -AsJson
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [ValidateSet('small', 'medium', 'large', 'xlarge', 'all')]
        [string]$Size = 'all',

        [Parameter(Mandatory = $false)]
        [string]$DataDirectory,

        [Parameter(Mandatory = $false)]
        [int]$Sources = 4,

        [Parameter(Mandatory = $false)]
        [int]$MaxParallel = [Math]::Min(8, [Environment]::ProcessorCount),

        [Parameter(Mandatory = $false)]
        [switch]$AsJson
    )

    $sizes = if ($Size -eq 'all') { @('small', 'medium', 'large', 'xlarge') } else { @($Size) }

    $resolvedDataDir = if ($DataDirectory) { $DataDirectory } else { Find-BloqrBenchmarkDataDirectory }
    if (-not $resolvedDataDir) {
        Write-Error 'Could not find a benchmarks/data directory. Pass -DataDirectory to point at one explicitly, or run this from within a clone of BloqrAI/bloqr-core.'
        return
    }

    $effectiveSources = [Math]::Max(1, $Sources)
    $effectiveMaxParallel = [Math]::Max(1, $MaxParallel)

    $results = @(foreach ($s in $sizes) {
        $dataPath = Join-Path $resolvedDataDir "$s.txt"
        if (-not (Test-Path -LiteralPath $dataPath -PathType Leaf)) {
            [PSCustomObject]@{
                Size               = $s
                Sources            = $effectiveSources
                MaxParallel        = $effectiveMaxParallel
                UnchunkedSuccess   = $false
                UnchunkedMs        = 0
                UnchunkedRuleCount = 0
                ChunkedSuccess     = $false
                ChunkedMs          = 0
                ChunkedRuleCount   = 0
                Speedup            = $null
                ErrorMessage       = "dataset file not found: $dataPath"
            }
            continue
        }

        $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "bloqr-benchmark-$([guid]::NewGuid())"
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

        try {
            $benchmarkConfig = @{
                name            = "Benchmark - $s"
                description     = "Real-pipeline benchmark of the '$s' canned dataset"
                version         = '1.0.0'
                sources         = @(1..$effectiveSources | ForEach-Object {
                        @{ name = "source-$_"; source = $dataPath; type = 'adblock' }
                    })
                transformations = @('Deduplicate', 'RemoveEmptyLines', 'TrimLines')
            }
            $configPath = Join-Path $tempDir 'config.json'
            ($benchmarkConfig | ConvertTo-Json -Depth 10) | Set-Content -Path $configPath

            $unchunkedResult = Invoke-BloqrCompiler -ConfigPath $configPath -OutputPath (Join-Path $tempDir 'unchunked-output.txt')
            $chunkedResult = Invoke-BloqrCompilerChunked -ConfigPath $configPath -OutputPath (Join-Path $tempDir 'chunked-output.txt') -MaxParallel $effectiveMaxParallel

            $speedup = $null
            if ($unchunkedResult.Success -and $chunkedResult.Success -and $chunkedResult.ElapsedMs -gt 0) {
                $speedup = [double]$unchunkedResult.ElapsedMs / [double]$chunkedResult.ElapsedMs
            }

            $benchmarkError = $null
            if (-not $unchunkedResult.Success) { $benchmarkError = $unchunkedResult.ErrorMessage }
            elseif (-not $chunkedResult.Success) { $benchmarkError = $chunkedResult.ErrorMessage }

            [PSCustomObject]@{
                Size               = $s
                Sources            = $effectiveSources
                MaxParallel        = $effectiveMaxParallel
                UnchunkedSuccess   = $unchunkedResult.Success
                UnchunkedMs        = $unchunkedResult.ElapsedMs
                UnchunkedRuleCount = $unchunkedResult.RuleCount
                ChunkedSuccess     = $chunkedResult.Success
                ChunkedMs          = $chunkedResult.ElapsedMs
                ChunkedRuleCount   = $chunkedResult.RuleCount
                Speedup            = $speedup
                ErrorMessage       = $benchmarkError
            }
        }
        finally {
            Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    })

    if ($AsJson) {
        # camelCase keys, matching the Rust/.NET/TypeScript/Python wrappers' --benchmark-json
        # schema so the root comparison script (see benchmarks/) can parse all five uniformly.
        $jsonReady = $results | ForEach-Object {
            [ordered]@{
                size               = $_.Size
                sources            = $_.Sources
                maxParallel        = $_.MaxParallel
                unchunkedSuccess   = $_.UnchunkedSuccess
                unchunkedMs        = $_.UnchunkedMs
                unchunkedRuleCount = $_.UnchunkedRuleCount
                chunkedSuccess     = $_.ChunkedSuccess
                chunkedMs          = $_.ChunkedMs
                chunkedRuleCount   = $_.ChunkedRuleCount
                speedup            = $_.Speedup
                error              = $_.ErrorMessage
            }
        }
        return ($jsonReady | ConvertTo-Json -Depth 10 -AsArray)
    }

    return $results
}
