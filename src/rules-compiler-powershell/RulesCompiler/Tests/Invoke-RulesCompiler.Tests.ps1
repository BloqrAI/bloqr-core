#Requires -Modules Pester

<#
.SYNOPSIS
    Tests for the Invoke-RulesCompiler compile pipeline (#368).

.DESCRIPTION
    Invoke-RulesCompiler shells out to hostlist-compiler rather than embedding a
    compiler itself, so these tests mock Get-RulesCompilerCommand (the
    module-private resolver) with a small fake script that writes canned output
    to whatever --output path it's given - keeping the tests deterministic and
    independent of whether hostlist-compiler is actually installed, mirroring the
    approach used for Invoke-RulesValidator.Tests.ps1's rules-validate mocking.
#>

BeforeAll {
    $script:CommonManifest = Join-Path $PSScriptRoot '..' '..' 'Common' 'Common.psd1'
    $script:ModuleManifest = Join-Path $PSScriptRoot '..' 'RulesCompiler.psd1'
    Import-Module $script:CommonManifest -Force
    Import-Module $script:ModuleManifest -Force

    function New-FakeHostlistCompiler {
        param(
            [Parameter(Mandatory = $true)][string]$Directory,
            [string]$RulesContent = "||example.com^`n@@||allowed.com^`n",
            [int]$ExitCode = 0
        )

        $scriptPath = Join-Path $Directory 'fake-hostlist-compiler'
        $body = @"
#!/bin/sh
OUT=""
while [ "`$#" -gt 0 ]; do
  case "`$1" in
    --output) OUT="`$2"; shift 2;;
    *) shift;;
  esac
done
if [ -n "`$OUT" ]; then
  cat <<'RULESEOF' > "`$OUT"
$RulesContent
RULESEOF
fi
exit $ExitCode
"@
        Set-Content -Path $scriptPath -Value $body
        if (-not $IsWindows) {
            & chmod +x $scriptPath
        }
        return $scriptPath
    }

    function New-TestConfig {
        param(
            [Parameter(Mandatory = $true)][string]$Directory,
            [string]$Name = 'test-filter'
        )

        $configPath = Join-Path $Directory 'compiler-config.json'
        $config = @{
            name    = $Name
            sources = @(@{ source = 'https://example.com/list.txt' })
        }
        ($config | ConvertTo-Json -Depth 10) | Set-Content -Path $configPath
        return $configPath
    }
}

Describe 'Invoke-RulesCompiler' {

    BeforeEach {
        $script:tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid())
        New-Item -ItemType Directory -Path $script:tempDir | Out-Null
    }

    AfterEach {
        Remove-Item -Path $script:tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'Fails cleanly when no ConfigPath is provided or set via environment' {
        $originalEnv = $env:ADGUARD_COMPILER_CONFIG
        $env:ADGUARD_COMPILER_CONFIG = $null
        try {
            $result = Invoke-RulesCompiler -ConfigPath ''
        }
        finally {
            $env:ADGUARD_COMPILER_CONFIG = $originalEnv
        }

        $result.Success | Should -Be $false
        $result.ErrorMessage | Should -Match 'No configuration path specified'
    }

    It 'Fails cleanly when the configuration file does not exist' {
        $result = Invoke-RulesCompiler -ConfigPath (Join-Path $script:tempDir 'does-not-exist.json')

        $result.Success | Should -Be $false
        $result.ErrorMessage | Should -Match 'Configuration error'
    }

    It 'Fails cleanly when hostlist-compiler cannot be found' {
        $configPath = New-TestConfig -Directory $script:tempDir
        Mock -ModuleName RulesCompiler Get-RulesCompilerCommand { $null }

        $result = Invoke-RulesCompiler -ConfigPath $configPath -OutputPath (Join-Path $script:tempDir 'output.txt')

        $result.Success | Should -Be $false
        $result.ErrorMessage | Should -Match 'hostlist-compiler not found'
    }

    It 'Compiles successfully and returns a populated CompilerResult' {
        $configPath = New-TestConfig -Directory $script:tempDir
        $outputPath = Join-Path $script:tempDir 'output.txt'
        $fakeCompiler = New-FakeHostlistCompiler -Directory $script:tempDir
        Mock -ModuleName RulesCompiler Get-RulesCompilerCommand { @{ Executable = $fakeCompiler; Arguments = @() } }.GetNewClosure()
        Mock -ModuleName RulesCompiler Invoke-RulesValidator { $null }

        $result = Invoke-RulesCompiler -ConfigPath $configPath -OutputPath $outputPath

        $result.Success | Should -Be $true
        $result.RuleCount | Should -Be 2
        $result.OutputPath | Should -Be $outputPath
        $result.Hash | Should -Not -BeNullOrEmpty
        $result.ConfigFormat | Should -Be 'json'
        Test-Path -LiteralPath $outputPath | Should -Be $true
    }

    It 'Reports failure when hostlist-compiler exits non-zero' {
        $configPath = New-TestConfig -Directory $script:tempDir
        $outputPath = Join-Path $script:tempDir 'output.txt'
        $fakeCompiler = New-FakeHostlistCompiler -Directory $script:tempDir -ExitCode 1
        Mock -ModuleName RulesCompiler Get-RulesCompilerCommand { @{ Executable = $fakeCompiler; Arguments = @() } }.GetNewClosure()

        $result = Invoke-RulesCompiler -ConfigPath $configPath -OutputPath $outputPath

        $result.Success | Should -Be $false
        $result.ErrorMessage | Should -Match 'hostlist-compiler exited with code 1'
    }

    It 'Copies to the rules directory when -CopyToRules is set' {
        $configPath = New-TestConfig -Directory $script:tempDir
        $outputPath = Join-Path $script:tempDir 'output.txt'
        $rulesDir = Join-Path $script:tempDir 'rules'
        $fakeCompiler = New-FakeHostlistCompiler -Directory $script:tempDir
        Mock -ModuleName RulesCompiler Get-RulesCompilerCommand { @{ Executable = $fakeCompiler; Arguments = @() } }.GetNewClosure()
        Mock -ModuleName RulesCompiler Invoke-RulesValidator { $null }

        $result = Invoke-RulesCompiler -ConfigPath $configPath -OutputPath $outputPath -CopyToRules -RulesDirectory $rulesDir

        $result.Success | Should -Be $true
        Test-Path -LiteralPath (Join-Path $rulesDir 'adguard_user_filter.txt') | Should -Be $true
    }

    It 'Rejects non-JSON configuration formats with a clear error' {
        # CompilerConfiguration itself may fail to parse YAML at all when the
        # optional powershell-yaml module isn't installed (as in CI) - either
        # way, a YAML config must never reach hostlist-compiler.
        $configPath = Join-Path $script:tempDir 'compiler-config.yaml'
        Set-Content -Path $configPath -Value "name: test-filter`nsources:`n  - source: https://example.com/list.txt`n"

        $result = Invoke-RulesCompiler -ConfigPath $configPath

        $result.Success | Should -Be $false
        $result.ErrorMessage | Should -Match 'Only JSON configuration files can be compiled today|powershell-yaml'
    }
}
