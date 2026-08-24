#Requires -Modules Pester
using module ..\Classes\CompilerResult.psm1

<#
.SYNOPSIS
    Tests for the Invoke-BloqrCompilerBenchmark real chunked-vs-unchunked benchmark (#420).

.DESCRIPTION
    Mocks Invoke-BloqrCompiler and Invoke-BloqrCompilerChunked directly (rather than the
    deno/@bloqr/compiler-core binary they shell out to) since the benchmark's own logic - dataset
    discovery, size validation, config synthesis, speedup calculation, JSON shape - is what
    these tests are exercising, not the underlying compile pipeline (already covered by
    Invoke-BloqrCompiler.Tests.ps1 and Invoke-BloqrCompilerChunked.Tests.ps1).
#>

BeforeAll {
    $script:CommonManifest = Join-Path $PSScriptRoot '..' '..' 'Common' 'Common.psd1'
    $script:ModuleManifest = Join-Path $PSScriptRoot '..' 'BloqrCompiler.psd1'
    Import-Module $script:CommonManifest -Force
    Import-Module $script:ModuleManifest -Force

    function New-BenchmarkDataDir {
        param([Parameter(Mandatory = $true)][string]$Directory)

        $dataDir = Join-Path $Directory 'benchmarks' 'data'
        New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
        foreach ($size in @('small', 'medium', 'large', 'xlarge')) {
            Set-Content -Path (Join-Path $dataDir "$size.txt") -Value '||example.com^'
        }
        return $dataDir
    }
}

Describe 'Invoke-BloqrCompilerBenchmark' {

    BeforeEach {
        $script:tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid())
        New-Item -ItemType Directory -Path $script:tempDir | Out-Null
    }

    AfterEach {
        Remove-Item -Path $script:tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'Fails cleanly when no benchmarks/data directory can be found or given' {
        # No -DataDirectory passed, so this exercises Find-BloqrBenchmarkDataDirectory's
        # auto-discovery - isolate it from this repo's own benchmarks/data by running from
        # an empty directory with no such tree above it.
        $isolatedDir = Join-Path $script:tempDir 'isolated'
        New-Item -ItemType Directory -Path $isolatedDir | Out-Null
        Push-Location $isolatedDir
        try {
            $results = Invoke-BloqrCompilerBenchmark -Size small -ErrorAction SilentlyContinue -ErrorVariable benchmarkError
        }
        finally {
            Pop-Location
        }

        $results | Should -BeNullOrEmpty
        $benchmarkError | Should -Not -BeNullOrEmpty
    }

    It 'Reports a missing dataset file as a result error, not an exception' {
        $dataDir = Join-Path $script:tempDir 'data'
        New-Item -ItemType Directory -Path $dataDir | Out-Null

        $results = Invoke-BloqrCompilerBenchmark -Size small -DataDirectory $dataDir

        $results.Count | Should -Be 1
        $results[0].Size | Should -Be 'small'
        $results[0].UnchunkedSuccess | Should -Be $false
        $results[0].ChunkedSuccess | Should -Be $false
        $results[0].ErrorMessage | Should -Match 'dataset file not found'
    }

    It 'Runs the real unchunked and chunked pipelines and computes speedup' {
        $dataDir = New-BenchmarkDataDir -Directory $script:tempDir

        Mock -ModuleName BloqrCompiler Invoke-BloqrCompiler {
            $result = [CompilerResult]::CreateSuccess(10, $OutputPath, 'deadbeef', 100)
            return $result
        }
        Mock -ModuleName BloqrCompiler Invoke-BloqrCompilerChunked {
            $result = [CompilerResult]::CreateSuccess(10, $OutputPath, 'deadbeef', 40)
            return $result
        }

        $results = Invoke-BloqrCompilerBenchmark -Size small -DataDirectory $dataDir -Sources 4 -MaxParallel 4

        $results.Count | Should -Be 1
        $results[0].UnchunkedSuccess | Should -Be $true
        $results[0].UnchunkedMs | Should -Be 100
        $results[0].ChunkedSuccess | Should -Be $true
        $results[0].ChunkedMs | Should -Be 40
        $results[0].Speedup | Should -Be 2.5
        $results[0].ErrorMessage | Should -BeNullOrEmpty
    }

    It 'Expands Size "all" to every canned dataset size' {
        $dataDir = New-BenchmarkDataDir -Directory $script:tempDir

        Mock -ModuleName BloqrCompiler Invoke-BloqrCompiler {
            [CompilerResult]::CreateSuccess(5, $OutputPath, 'deadbeef', 50)
        }
        Mock -ModuleName BloqrCompiler Invoke-BloqrCompilerChunked {
            [CompilerResult]::CreateSuccess(5, $OutputPath, 'deadbeef', 50)
        }

        $results = Invoke-BloqrCompilerBenchmark -Size all -DataDirectory $dataDir

        $results.Count | Should -Be 4
        @($results.Size) | Should -Be @('small', 'medium', 'large', 'xlarge')
    }

    It 'Leaves speedup null when the chunked run failed' {
        $dataDir = New-BenchmarkDataDir -Directory $script:tempDir

        Mock -ModuleName BloqrCompiler Invoke-BloqrCompiler {
            [CompilerResult]::CreateSuccess(5, $OutputPath, 'deadbeef', 50)
        }
        Mock -ModuleName BloqrCompiler Invoke-BloqrCompilerChunked {
            [CompilerResult]::CreateFailure('deno not found')
        }

        $results = Invoke-BloqrCompilerBenchmark -Size small -DataDirectory $dataDir

        $results[0].ChunkedSuccess | Should -Be $false
        $results[0].Speedup | Should -BeNullOrEmpty
        $results[0].ErrorMessage | Should -Match 'deno not found'
    }

    It 'Emits a JSON array with camelCase keys matching the other language wrappers when -AsJson is set' {
        $dataDir = New-BenchmarkDataDir -Directory $script:tempDir

        Mock -ModuleName BloqrCompiler Invoke-BloqrCompiler {
            [CompilerResult]::CreateSuccess(10, $OutputPath, 'deadbeef', 100)
        }
        Mock -ModuleName BloqrCompiler Invoke-BloqrCompilerChunked {
            [CompilerResult]::CreateSuccess(10, $OutputPath, 'deadbeef', 40)
        }

        $json = Invoke-BloqrCompilerBenchmark -Size small -DataDirectory $dataDir -AsJson

        # Assert on the raw JSON text for array-shape, not the parsed result:
        # ConvertFrom-Json unwraps a single-element JSON array back into a bare object,
        # which would make this assertion pass even if the emitted JSON were a lone
        # object rather than a proper 1-element array.
        $json.Trim() | Should -Match '^\['
        $json.Trim() | Should -Match '\]$'

        $parsed = $json | ConvertFrom-Json -NoEnumerate
        @($parsed).Count | Should -Be 1
        $parsed[0].size | Should -Be 'small'
        $parsed[0].unchunkedMs | Should -Be 100
        $parsed[0].chunkedMs | Should -Be 40
        $parsed[0].speedup | Should -Be 2.5
    }
}
