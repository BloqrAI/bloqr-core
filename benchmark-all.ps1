<#
.SYNOPSIS
    Root-level benchmark comparison script for ad-blocking repository

.DESCRIPTION
    Runs every available language's native benchmark command (Rust/.NET/TypeScript/
    Python/PowerShell), collects their JSON output, prints a comparison table, and writes
    a combined JSON summary. Skips any language whose toolchain isn't installed, matching
    launcher.ps1's tool-detection convention. See benchmarks/README.md for the shared
    data/JSON-output contract these commands follow, and issue #421.

.PARAMETER Size
    Dataset size: small, medium, large, xlarge, or all (default: all)

.PARAMETER Sources
    Identical duplicated sources for the chunked run (default: 4)

.PARAMETER MaxParallel
    Max parallel workers for the chunked run (default: each language's own default)

.PARAMETER Languages
    Comma-separated subset to run (default: all five)

.PARAMETER Output
    Path for the combined JSON summary (default: benchmarks/results/benchmark-all-<timestamp>.json)

.EXAMPLE
    .\benchmark-all.ps1
    Run all five languages, all dataset sizes

.EXAMPLE
    .\benchmark-all.ps1 -Size small

.EXAMPLE
    .\benchmark-all.ps1 -Languages rust,python

.EXAMPLE
    .\benchmark-all.ps1 -Sources 8 -MaxParallel 8
#>

[CmdletBinding()]
param(
    [Parameter(HelpMessage = "Dataset size: small, medium, large, xlarge, or all")]
    [ValidateSet('small', 'medium', 'large', 'xlarge', 'all')]
    [string]$Size = 'all',

    [Parameter(HelpMessage = "Identical duplicated sources for the chunked run")]
    [int]$Sources = 4,

    [Parameter(HelpMessage = "Max parallel workers for the chunked run")]
    [int]$MaxParallel = 0,

    [Parameter(HelpMessage = "Comma-separated subset of languages to run")]
    [string[]]$Languages = @('rust', 'dotnet', 'typescript', 'python', 'powershell'),

    [Parameter(HelpMessage = "Path for the combined JSON summary")]
    [string]$Output
)

$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

$ResultsDir = Join-Path $ScriptDir 'benchmarks' 'results'
New-Item -ItemType Directory -Path $ResultsDir -Force | Out-Null
$Timestamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')
$CombinedFile = if ($Output) { $Output } else { Join-Path $ResultsDir "benchmark-all-$Timestamp.json" }

$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid())
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null

try {
    Write-Host ""
    Write-Host "======================================================================"
    Write-Host "BENCHMARK COMPARISON (real per-language compiler pipelines)"
    Write-Host "======================================================================"
    Write-Host "Size: $Size   Sources: $Sources   Languages: $($Languages -join ',')"
    Write-Host ""

    $isSelected = { param($name) $Languages -contains $name }

    if (& $isSelected 'rust') {
        if (Get-Command cargo -ErrorAction SilentlyContinue) {
            Write-Host "--- Rust ---" -ForegroundColor Blue
            & cargo build --release -p bloqr-compiler -q 2>&1 | Select-Object -Last 20 | Write-Host
            if ($LASTEXITCODE -eq 0) {
                $rustArgs = @('benchmark', '--size', $Size, '--sources', $Sources, '--json')
                if ($MaxParallel -gt 0) { $rustArgs += @('--max-parallel', $MaxParallel) }
                $rustOutput = & './target/release/bloqr-compiler' @rustArgs 2>"$TempDir/rust.err"
                if ($LASTEXITCODE -eq 0) {
                    $rustOutput | Set-Content -Path (Join-Path $TempDir 'rust.json')
                    Write-Host "Rust benchmark complete" -ForegroundColor Green
                }
                else {
                    Write-Host "Rust benchmark failed:" -ForegroundColor Yellow
                    Get-Content (Join-Path $TempDir 'rust.err') | Write-Host
                }
            }
            else {
                Write-Host "Rust build failed, skipping" -ForegroundColor Yellow
            }
        }
        else {
            Write-Host "cargo not found, skipping Rust" -ForegroundColor Yellow
        }
        Write-Host ""
    }

    if (& $isSelected 'dotnet') {
        if (Get-Command dotnet -ErrorAction SilentlyContinue) {
            Write-Host "--- .NET ---" -ForegroundColor Blue
            Push-Location 'src/compilers/dotnet'
            try {
                $dotnetArgs = @('run', '-c', 'Release', '--project', 'src/Bloqr.Compiler.Dotnet.Console', '--',
                    '--benchmark', '--benchmark-size', $Size, '--benchmark-sources', $Sources, '--benchmark-json')
                if ($MaxParallel -gt 0) { $dotnetArgs += @('--benchmark-max-parallel', $MaxParallel) }
                $dotnetOutput = & dotnet @dotnetArgs 2>"$TempDir/dotnet.err"
                if ($LASTEXITCODE -eq 0) {
                    $dotnetOutput | Set-Content -Path (Join-Path $TempDir 'dotnet.json')
                    Write-Host "  .NET benchmark complete" -ForegroundColor Green
                }
                else {
                    Write-Host "  .NET benchmark failed:" -ForegroundColor Yellow
                    Get-Content (Join-Path $TempDir 'dotnet.err') | Write-Host
                }
            }
            finally {
                Pop-Location
            }
        }
        else {
            Write-Host "dotnet not found, skipping .NET" -ForegroundColor Yellow
        }
        Write-Host ""
    }

    if (& $isSelected 'typescript') {
        if (Get-Command deno -ErrorAction SilentlyContinue) {
            Write-Host "--- TypeScript ---" -ForegroundColor Blue
            Push-Location 'src/compilers/typescript'
            try {
                $tsArgs = @('run', '--allow-read', '--allow-write', '--allow-env', '--allow-net', '--allow-run', 'src/mod.ts',
                    '--benchmark', '--benchmark-size', $Size, '--benchmark-sources', $Sources, '--benchmark-json')
                if ($MaxParallel -gt 0) { $tsArgs += @('--benchmark-max-parallel', $MaxParallel) }
                $tsOutput = & deno @tsArgs 2>"$TempDir/typescript.err"
                if ($LASTEXITCODE -eq 0) {
                    $tsOutput | Set-Content -Path (Join-Path $TempDir 'typescript.json')
                    Write-Host "  TypeScript benchmark complete" -ForegroundColor Green
                }
                else {
                    Write-Host "  TypeScript benchmark failed:" -ForegroundColor Yellow
                    Get-Content (Join-Path $TempDir 'typescript.err') | Write-Host
                }
            }
            finally {
                Pop-Location
            }
        }
        else {
            Write-Host "deno not found, skipping TypeScript" -ForegroundColor Yellow
        }
        Write-Host ""
    }

    if (& $isSelected 'python') {
        if (Get-Command bloqr-compiler -ErrorAction SilentlyContinue) {
            Write-Host "--- Python ---" -ForegroundColor Blue
            $pyArgs = @('--benchmark', '--benchmark-size', $Size, '--benchmark-sources', $Sources, '--benchmark-json')
            if ($MaxParallel -gt 0) { $pyArgs += @('--benchmark-max-parallel', $MaxParallel) }
            $pyOutput = & bloqr-compiler @pyArgs 2>"$TempDir/python.err"
            if ($LASTEXITCODE -eq 0) {
                $pyOutput | Set-Content -Path (Join-Path $TempDir 'python.json')
                Write-Host "Python benchmark complete" -ForegroundColor Green
            }
            else {
                Write-Host "Python benchmark failed:" -ForegroundColor Yellow
                Get-Content (Join-Path $TempDir 'python.err') | Write-Host
            }
        }
        else {
            Write-Host "bloqr-compiler console script not found on PATH (pip install -e src/compilers/python), skipping Python" -ForegroundColor Yellow
        }
        Write-Host ""
    }

    if (& $isSelected 'powershell') {
        if (Get-Command pwsh -ErrorAction SilentlyContinue) {
            Write-Host "--- PowerShell ---" -ForegroundColor Blue
            try {
                Import-Module (Join-Path $ScriptDir 'src/compilers/powershell/Common/Common.psd1') -Force
                Import-Module (Join-Path $ScriptDir 'src/compilers/powershell/BloqrCompiler/BloqrCompiler.psd1') -Force
                $psParams = @{ Size = $Size; Sources = $Sources; AsJson = $true }
                if ($MaxParallel -gt 0) { $psParams['MaxParallel'] = $MaxParallel }
                $psOutput = Invoke-BloqrCompilerBenchmark @psParams
                $psOutput | Set-Content -Path (Join-Path $TempDir 'powershell.json')
                Write-Host "PowerShell benchmark complete" -ForegroundColor Green
            }
            catch {
                Write-Host "PowerShell benchmark failed: $_" -ForegroundColor Yellow
            }
        }
        else {
            Write-Host "pwsh not found, skipping PowerShell" -ForegroundColor Yellow
        }
        Write-Host ""
    }

    # Merge whatever JSON files landed in $TempDir into one combined summary, tagging each
    # result with its language, and print a comparison table.
    $combined = @()
    foreach ($name in @('rust', 'dotnet', 'typescript', 'python', 'powershell')) {
        $path = Join-Path $TempDir "$name.json"
        if (-not (Test-Path -LiteralPath $path)) { continue }
        try {
            $results = @(Get-Content -LiteralPath $path -Raw | ConvertFrom-Json)
        }
        catch {
            Write-Host "[WARN] Could not parse $name benchmark output as JSON, skipping" -ForegroundColor Yellow
            continue
        }
        foreach ($r in $results) {
            $r | Add-Member -MemberType NoteProperty -Name 'language' -Value $name -Force
            $combined += $r
        }
    }

    $CombinedFile | Split-Path -Parent | ForEach-Object { New-Item -ItemType Directory -Path $_ -Force | Out-Null }
    ($combined | ConvertTo-Json -Depth 10 -AsArray) | Set-Content -Path $CombinedFile

    if ($combined.Count -eq 0) {
        Write-Host "No benchmark results collected - nothing to compare."
        return
    }

    Write-Host ('-' * 100)
    Write-Host "RESULTS"
    Write-Host ('-' * 100)
    Write-Host ("{0,-12} {1,-8} {2,-12} {3,-12} {4,-10} {5,-10} {6}" -f 'Language', 'Size', 'Unchunked', 'Chunked', 'Speedup', 'Rules', 'Status')
    Write-Host ('-' * 100)
    foreach ($r in $combined) {
        if ($r.error -and -not $r.unchunkedSuccess -and -not $r.chunkedSuccess) {
            $errText = if ($r.error.Length -gt 60) { $r.error.Substring(0, 60) } else { $r.error }
            Write-Host ("{0,-12} {1,-8} FAILED: {2}" -f $r.language, $r.size, $errText)
            continue
        }
        $speedupStr = if ($null -ne $r.speedup) { "{0:N2}x" -f $r.speedup } else { "n/a" }
        $status = if ($r.unchunkedSuccess -and $r.chunkedSuccess) { "ok" } else { "partial" }
        Write-Host ("{0,-12} {1,-8} {2,-12} {3,-12} {4,-10} {5,-10} {6}" -f `
                $r.language, $r.size, "$($r.unchunkedMs)ms", "$($r.chunkedMs)ms", $speedupStr, $r.chunkedRuleCount, $status)
    }
    Write-Host ('-' * 100)
    Write-Host ""
    Write-Host "Combined summary written to: $CombinedFile"
}
finally {
    Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue
}
