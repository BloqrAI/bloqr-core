#Requires -Modules Pester

<#
.SYNOPSIS
    Tests for the Invoke-BloqrCompilerChunked chunked/parallel compile pipeline (#420).

.DESCRIPTION
    Like Invoke-BloqrCompiler.Tests.ps1, mocks Get-BloqrCompilerCommand with a small fake
    script so these tests are deterministic and independent of whether hostlist-compiler is
    actually installed. The fake script derives its "unique" output rule from the chunk
    config's own source URL (each chunk gets a genuinely distinct source, courtesy of
    Split-BloqrCompilerConfiguration) rather than the shell's own PID - a subprocess PID can
    be reused by the OS across short-lived sibling processes, which made an earlier version
    of this fixture intermittently under-count distinct chunks.
#>

BeforeAll {
    $script:CommonManifest = Join-Path $PSScriptRoot '..' '..' 'Common' 'Common.psd1'
    $script:ModuleManifest = Join-Path $PSScriptRoot '..' 'BloqrCompiler.psd1'
    Import-Module $script:CommonManifest -Force
    Import-Module $script:ModuleManifest -Force

    function New-FakeChunkCompiler {
        param(
            [Parameter(Mandatory = $true)][string]$Directory,
            [int]$ExitCode = 0
        )

        $scriptPath = Join-Path $Directory 'fake-chunk-compiler'
        $body = @'
#!/bin/sh
OUT=""
CONFIG=""
while [ "$#" -gt 0 ]; do
  case "$1" in
    --output) OUT="$2"; shift 2;;
    --config) CONFIG="$2"; shift 2;;
    *) shift;;
  esac
done
if [ -n "$OUT" ]; then
  SOURCE_URL=$(grep -o '"source"[^"]*"[^"]*"' "$CONFIG" | head -1 | sed 's/.*: *"//;s/"$//')
  cat <<RULESEOF > "$OUT"
||shared.com^
||chunk-marker-for-$SOURCE_URL^
RULESEOF
fi
exit __EXITCODE__
'@
        $body = $body.Replace('__EXITCODE__', $ExitCode)
        Set-Content -Path $scriptPath -Value $body
        if (-not $IsWindows) {
            & chmod +x $scriptPath
        }
        return $scriptPath
    }

    function New-TestConfig {
        param(
            [Parameter(Mandatory = $true)][string]$Directory,
            [int]$SourceCount = 3
        )

        $configPath = Join-Path $Directory 'compiler-config.json'
        $sources = 1..$SourceCount | ForEach-Object {
            @{ name = "source-$_"; source = "https://example.com/list-$_.txt" }
        }
        $config = @{
            name    = 'chunk-test-filter'
            sources = @($sources)
        }
        ($config | ConvertTo-Json -Depth 10) | Set-Content -Path $configPath
        return $configPath
    }
}

Describe 'Invoke-BloqrCompilerChunked' {

    BeforeEach {
        $script:tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid())
        New-Item -ItemType Directory -Path $script:tempDir | Out-Null
    }

    AfterEach {
        Remove-Item -Path $script:tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'Fails cleanly when the configuration file does not exist' {
        $result = Invoke-BloqrCompilerChunked -ConfigPath (Join-Path $script:tempDir 'does-not-exist.json')

        $result.Success | Should -Be $false
        $result.ErrorMessage | Should -Match 'Configuration error'
    }

    It 'Fails cleanly when hostlist-compiler cannot be found' {
        $configPath = New-TestConfig -Directory $script:tempDir
        Mock -ModuleName BloqrCompiler Get-BloqrCompilerCommand { $null }

        $result = Invoke-BloqrCompilerChunked -ConfigPath $configPath -OutputPath (Join-Path $script:tempDir 'output.txt')

        $result.Success | Should -Be $false
        $result.ErrorMessage | Should -Match 'hostlist-compiler not found'
    }

    It 'Compiles chunks in parallel and merges/dedupes the results' {
        $configPath = New-TestConfig -Directory $script:tempDir -SourceCount 3
        $outputPath = Join-Path $script:tempDir 'output.txt'
        $fakeCompiler = New-FakeChunkCompiler -Directory $script:tempDir
        Mock -ModuleName BloqrCompiler Get-BloqrCompilerCommand { @{ Executable = $fakeCompiler; Arguments = @() } }.GetNewClosure()

        $result = Invoke-BloqrCompilerChunked -ConfigPath $configPath -OutputPath $outputPath -MaxParallel 3

        $result.Success | Should -Be $true
        # ||shared.com^ appears once per chunk but is deduplicated to 1; each chunk's
        # marker rule is derived from that chunk's own (genuinely distinct) source URL, so
        # with 3 chunks the merged count is 1 (shared) + 3 (one per chunk's unique source) = 4.
        $result.RuleCount | Should -Be 4
        $result.OutputPath | Should -Be $outputPath
        $result.Hash | Should -Not -BeNullOrEmpty
        Test-Path -LiteralPath $outputPath | Should -Be $true

        $content = Get-Content -LiteralPath $outputPath
        ($content | Where-Object { $_ -eq '||shared.com^' } | Measure-Object).Count | Should -Be 1
    }

    It 'Reports failure when any chunk fails to compile' {
        $configPath = New-TestConfig -Directory $script:tempDir -SourceCount 2
        $outputPath = Join-Path $script:tempDir 'output.txt'
        $fakeCompiler = New-FakeChunkCompiler -Directory $script:tempDir -ExitCode 1
        Mock -ModuleName BloqrCompiler Get-BloqrCompilerCommand { @{ Executable = $fakeCompiler; Arguments = @() } }.GetNewClosure()

        $result = Invoke-BloqrCompilerChunked -ConfigPath $configPath -OutputPath $outputPath -MaxParallel 2

        $result.Success | Should -Be $false
        $result.ErrorMessage | Should -Match 'chunk\(s\) failed'
    }

    It 'Rejects non-JSON configuration formats with a clear error' {
        $configPath = Join-Path $script:tempDir 'compiler-config.yaml'
        Set-Content -Path $configPath -Value "name: test-filter`nsources:`n  - source: https://example.com/list.txt`n"

        $result = Invoke-BloqrCompilerChunked -ConfigPath $configPath

        $result.Success | Should -Be $false
        $result.ErrorMessage | Should -Match 'Only JSON configuration files can be compiled today|powershell-yaml'
    }

    It 'Produces exactly one chunk when there is only one source' {
        $configPath = New-TestConfig -Directory $script:tempDir -SourceCount 1
        $outputPath = Join-Path $script:tempDir 'output.txt'
        $fakeCompiler = New-FakeChunkCompiler -Directory $script:tempDir
        Mock -ModuleName BloqrCompiler Get-BloqrCompilerCommand { @{ Executable = $fakeCompiler; Arguments = @() } }.GetNewClosure()

        $result = Invoke-BloqrCompilerChunked -ConfigPath $configPath -OutputPath $outputPath -MaxParallel 4

        $result.Success | Should -Be $true
        $result.RuleCount | Should -Be 2
    }
}
