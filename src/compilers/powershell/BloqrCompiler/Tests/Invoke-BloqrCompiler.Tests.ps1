#Requires -Modules Pester
using module ..\Classes\RulesValidatorResult.psm1

<#
.SYNOPSIS
    Tests for the Invoke-BloqrCompiler compile pipeline (#368, #439).

.DESCRIPTION
    Invoke-BloqrCompiler shells out to `deno run jsr:@bloqr/compiler-core/cli`
    rather than embedding a compiler itself, so these tests mock
    Get-BloqrCompilerCommand (the module-private resolver) with a small fake
    script that writes canned output to whatever --output (and, for the
    dual-artifact tests, --browser-output) path it's given - keeping the tests
    deterministic and independent of whether deno is actually installed,
    mirroring the approach used for Invoke-RulesValidator.Tests.ps1's
    bloqr-validate mocking.
#>

BeforeAll {
    $script:CommonManifest = Join-Path $PSScriptRoot '..' '..' 'Common' 'Common.psd1'
    $script:ModuleManifest = Join-Path $PSScriptRoot '..' 'BloqrCompiler.psd1'
    Import-Module $script:CommonManifest -Force
    Import-Module $script:ModuleManifest -Force

    function New-FakeCompilerCore {
        param(
            [Parameter(Mandatory = $true)][string]$Directory,
            [string]$RulesContent = "||example.com^`n@@||allowed.com^`n",
            [string]$BrowserRulesContent,
            [int]$ExitCode = 0
        )

        $scriptPath = Join-Path $Directory 'fake-compiler-core'
        $browserHeredoc = if ($BrowserRulesContent) {
            @"
if [ -n "`$BROWSER_OUT" ]; then
  cat <<'BROWSEREOF' > "`$BROWSER_OUT"
$BrowserRulesContent
BROWSEREOF
fi
"@
        }
        else { '' }

        $body = @"
#!/bin/sh
OUT=""
BROWSER_OUT=""
while [ "`$#" -gt 0 ]; do
  case "`$1" in
    --output) OUT="`$2"; shift 2;;
    --browser-output) BROWSER_OUT="`$2"; shift 2;;
    *) shift;;
  esac
done
if [ -n "`$OUT" ]; then
  cat <<'RULESEOF' > "`$OUT"
$RulesContent
RULESEOF
fi
$browserHeredoc
exit $ExitCode
"@
        Set-Content -Path $scriptPath -Value $body
        if (-not $IsWindows) {
            & chmod +x $scriptPath
        }
        return $scriptPath
    }

    function New-ValidRulesValidatorResult {
        param([string[]]$Messages = @())
        $result = [RulesValidatorResult]::new()
        $result.IsValid = $true
        $result.Format = 'Adblock'
        $result.ValidRules = 2
        $result.InvalidRules = 0
        $result.Messages = $Messages
        return $result
    }

    function New-InvalidRulesValidatorResult {
        $result = [RulesValidatorResult]::new()
        $result.IsValid = $false
        $result.Format = 'Adblock'
        $result.ValidRules = 1
        $result.InvalidRules = 1
        $result.Messages = @('bad rule at line 2')
        return $result
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

    function Register-FakeCompilerMock {
        # $FakeCompiler is referenced inside the nested Mock scriptblock below via
        # GetNewClosure() - PSScriptAnalyzer's static analysis doesn't trace that
        # usage, so PSReviewUnusedParameter is suppressed here.
        [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', 'FakeCompiler', Justification = 'Used inside the nested Mock scriptblock via GetNewClosure()')]
        param([Parameter(Mandatory = $true)][string]$FakeCompiler)
        Mock -ModuleName BloqrCompiler Get-BloqrCompilerCommand {
            param($ConfigPath, $OutputPath, $BrowserOutputPath)
            @{ Executable = $FakeCompiler; Arguments = @('--config', $ConfigPath, '--output', $OutputPath) + $(if ($BrowserOutputPath) { @('--browser-output', $BrowserOutputPath) } else { @() }) }
        }.GetNewClosure()
    }
}

Describe 'Invoke-BloqrCompiler' {

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
            $result = Invoke-BloqrCompiler -ConfigPath ''
        }
        finally {
            $env:ADGUARD_COMPILER_CONFIG = $originalEnv
        }

        $result.Success | Should -Be $false
        $result.ErrorMessage | Should -Match 'No configuration path specified'
    }

    It 'Fails cleanly when the configuration file does not exist' {
        $result = Invoke-BloqrCompiler -ConfigPath (Join-Path $script:tempDir 'does-not-exist.json')

        $result.Success | Should -Be $false
        $result.ErrorMessage | Should -Match 'Configuration error'
    }

    It 'Fails cleanly when deno cannot be found' {
        $configPath = New-TestConfig -Directory $script:tempDir
        Mock -ModuleName BloqrCompiler Get-BloqrCompilerCommand { $null }

        $result = Invoke-BloqrCompiler -ConfigPath $configPath -OutputPath (Join-Path $script:tempDir 'output.txt')

        $result.Success | Should -Be $false
        $result.ErrorMessage | Should -Match 'deno not found'
    }

    It 'Compiles successfully and returns a populated CompilerResult' {
        $configPath = New-TestConfig -Directory $script:tempDir
        $outputPath = Join-Path $script:tempDir 'output.txt'
        $fakeCompiler = New-FakeCompilerCore -Directory $script:tempDir
        Register-FakeCompilerMock -FakeCompiler $fakeCompiler
        Mock -ModuleName BloqrCompiler Invoke-RulesValidator { New-ValidRulesValidatorResult }

        $result = Invoke-BloqrCompiler -ConfigPath $configPath -OutputPath $outputPath

        $result.Success | Should -Be $true
        $result.RuleCount | Should -Be 2
        $result.OutputPath | Should -Be $outputPath
        $result.Hash | Should -Not -BeNullOrEmpty
        $result.ConfigFormat | Should -Be 'json'
        $result.BrowserOutputPath | Should -BeNullOrEmpty
        Test-Path -LiteralPath $outputPath | Should -Be $true
    }

    It 'Reports failure when the compiler exits non-zero' {
        $configPath = New-TestConfig -Directory $script:tempDir
        $outputPath = Join-Path $script:tempDir 'output.txt'
        $fakeCompiler = New-FakeCompilerCore -Directory $script:tempDir -ExitCode 1
        Register-FakeCompilerMock -FakeCompiler $fakeCompiler

        $result = Invoke-BloqrCompiler -ConfigPath $configPath -OutputPath $outputPath

        $result.Success | Should -Be $false
        $result.ErrorMessage | Should -Match '@bloqr/compiler-core exited with code 1'
    }

    It 'Fails closed by default when bloqr-validate could not run' {
        $configPath = New-TestConfig -Directory $script:tempDir
        $outputPath = Join-Path $script:tempDir 'output.txt'
        $fakeCompiler = New-FakeCompilerCore -Directory $script:tempDir
        Register-FakeCompilerMock -FakeCompiler $fakeCompiler
        Mock -ModuleName BloqrCompiler Invoke-RulesValidator { $null }

        $result = Invoke-BloqrCompiler -ConfigPath $configPath -OutputPath $outputPath

        $result.Success | Should -Be $false
        $result.ErrorMessage | Should -Match 'bloqr-validate could not run'
    }

    It 'Succeeds when bloqr-validate could not run but -AllowUnvalidatedOutput is set' {
        $configPath = New-TestConfig -Directory $script:tempDir
        $outputPath = Join-Path $script:tempDir 'output.txt'
        $fakeCompiler = New-FakeCompilerCore -Directory $script:tempDir
        Register-FakeCompilerMock -FakeCompiler $fakeCompiler
        Mock -ModuleName BloqrCompiler Invoke-RulesValidator { $null }

        $result = Invoke-BloqrCompiler -ConfigPath $configPath -OutputPath $outputPath -AllowUnvalidatedOutput

        $result.Success | Should -Be $true
    }

    It 'Fails closed by default when bloqr-validate reports invalid syntax' {
        $configPath = New-TestConfig -Directory $script:tempDir
        $outputPath = Join-Path $script:tempDir 'output.txt'
        $fakeCompiler = New-FakeCompilerCore -Directory $script:tempDir
        Register-FakeCompilerMock -FakeCompiler $fakeCompiler
        Mock -ModuleName BloqrCompiler Invoke-RulesValidator { New-InvalidRulesValidatorResult }

        $result = Invoke-BloqrCompiler -ConfigPath $configPath -OutputPath $outputPath

        $result.Success | Should -Be $false
        $result.ErrorMessage | Should -Match 'bad rule at line 2'
    }

    It 'Succeeds when bloqr-validate reports invalid syntax but -AllowUnvalidatedOutput is set' {
        $configPath = New-TestConfig -Directory $script:tempDir
        $outputPath = Join-Path $script:tempDir 'output.txt'
        $fakeCompiler = New-FakeCompilerCore -Directory $script:tempDir
        Register-FakeCompilerMock -FakeCompiler $fakeCompiler
        Mock -ModuleName BloqrCompiler Invoke-RulesValidator { New-InvalidRulesValidatorResult }

        $result = Invoke-BloqrCompiler -ConfigPath $configPath -OutputPath $outputPath -AllowUnvalidatedOutput

        $result.Success | Should -Be $true
    }

    It 'Fails when bloqr-validate reports warnings and -FailOnWarnings is set' {
        $configPath = New-TestConfig -Directory $script:tempDir
        $outputPath = Join-Path $script:tempDir 'output.txt'
        $fakeCompiler = New-FakeCompilerCore -Directory $script:tempDir
        Register-FakeCompilerMock -FakeCompiler $fakeCompiler
        Mock -ModuleName BloqrCompiler Invoke-RulesValidator { New-ValidRulesValidatorResult -Messages @('suspicious rule at line 1') }

        $result = Invoke-BloqrCompiler -ConfigPath $configPath -OutputPath $outputPath -FailOnWarnings

        $result.Success | Should -Be $false
        $result.ErrorMessage | Should -Match 'warnings'
    }

    It 'Copies to the rules directory when -CopyToRules is set' {
        $configPath = New-TestConfig -Directory $script:tempDir
        $outputPath = Join-Path $script:tempDir 'output.txt'
        $rulesDir = Join-Path $script:tempDir 'rules'
        $fakeCompiler = New-FakeCompilerCore -Directory $script:tempDir
        Register-FakeCompilerMock -FakeCompiler $fakeCompiler
        Mock -ModuleName BloqrCompiler Invoke-RulesValidator { New-ValidRulesValidatorResult }

        $result = Invoke-BloqrCompiler -ConfigPath $configPath -OutputPath $outputPath -CopyToRules -RulesDirectory $rulesDir

        $result.Success | Should -Be $true
        Test-Path -LiteralPath (Join-Path $rulesDir 'adguard_user_filter.txt') | Should -Be $true
    }

    It 'Rejects non-JSON configuration formats with a clear error' {
        # CompilerConfiguration itself may fail to parse YAML at all when the
        # optional powershell-yaml module isn't installed (as in CI) - either
        # way, a YAML config must never reach the compiler.
        $configPath = Join-Path $script:tempDir 'compiler-config.yaml'
        Set-Content -Path $configPath -Value "name: test-filter`nsources:`n  - source: https://example.com/list.txt`n"

        $result = Invoke-BloqrCompiler -ConfigPath $configPath

        $result.Success | Should -Be $false
        $result.ErrorMessage | Should -Match 'Only JSON configuration files can be compiled today|powershell-yaml'
    }

    Context 'Dual-artifact (DNS + browser-syntax) compilation (#439)' {

        It 'Detects, hashes, and rule-counts a browser artifact written at the default derived path' {
            $configPath = New-TestConfig -Directory $script:tempDir
            $outputPath = Join-Path $script:tempDir 'output.txt'
            $fakeCompiler = New-FakeCompilerCore -Directory $script:tempDir -BrowserRulesContent "||ads.example.com^`n"
            # The fake script only writes a browser artifact when it's told to via
            # --browser-output; since -BrowserOutputPath isn't passed, the mock's
            # own default derivation must match Get-BloqrBrowserOutputPath's.
            Mock -ModuleName BloqrCompiler Get-BloqrCompilerCommand {
                param($ConfigPath, $OutputPath)
                $derivedBrowserOutput = if ($OutputPath.EndsWith('.txt')) { $OutputPath.Substring(0, $OutputPath.Length - 4) + '.browser.txt' } else { "$OutputPath.browser.txt" }
                @{ Executable = $fakeCompiler; Arguments = @('--config', $ConfigPath, '--output', $OutputPath, '--browser-output', $derivedBrowserOutput) }
            }.GetNewClosure()
            Mock -ModuleName BloqrCompiler Invoke-RulesValidator { New-ValidRulesValidatorResult }

            $result = Invoke-BloqrCompiler -ConfigPath $configPath -OutputPath $outputPath

            $expectedBrowserPath = $outputPath.Substring(0, $outputPath.Length - 4) + '.browser.txt'
            $result.Success | Should -Be $true
            $result.BrowserOutputPath | Should -Be $expectedBrowserPath
            $result.BrowserOutputHash | Should -Not -BeNullOrEmpty
            $result.BrowserRuleCount | Should -Be 1
        }

        It 'Passes -Engine and an explicit -BrowserOutputPath through to Get-BloqrCompilerCommand' {
            $configPath = New-TestConfig -Directory $script:tempDir
            $outputPath = Join-Path $script:tempDir 'output.txt'
            $browserOutputPath = Join-Path $script:tempDir 'custom.browser.txt'
            $fakeCompiler = New-FakeCompilerCore -Directory $script:tempDir -BrowserRulesContent "||ads.example.com^`n"
            Register-FakeCompilerMock -FakeCompiler $fakeCompiler
            Mock -ModuleName BloqrCompiler Invoke-RulesValidator { New-ValidRulesValidatorResult }

            $result = Invoke-BloqrCompiler -ConfigPath $configPath -OutputPath $outputPath -Engine browser -BrowserOutputPath $browserOutputPath

            $result.Success | Should -Be $true
            $result.BrowserOutputPath | Should -Be $browserOutputPath
            Should -Invoke -ModuleName BloqrCompiler Get-BloqrCompilerCommand -ParameterFilter {
                $Engine -eq 'browser' -and $BrowserOutputPath -eq $browserOutputPath
            }
        }

        It 'Does not pass -BrowserOutputPath through to Get-BloqrCompilerCommand when not explicitly requested' {
            $configPath = New-TestConfig -Directory $script:tempDir
            $outputPath = Join-Path $script:tempDir 'output.txt'
            $fakeCompiler = New-FakeCompilerCore -Directory $script:tempDir
            Register-FakeCompilerMock -FakeCompiler $fakeCompiler
            Mock -ModuleName BloqrCompiler Invoke-RulesValidator { New-ValidRulesValidatorResult }

            $null = Invoke-BloqrCompiler -ConfigPath $configPath -OutputPath $outputPath

            Should -Invoke -ModuleName BloqrCompiler Get-BloqrCompilerCommand -ParameterFilter {
                -not $BrowserOutputPath
            }
        }

        It 'Does not roll back the DNS artifact when the browser artifact fails validation, and names it in the error' {
            $configPath = New-TestConfig -Directory $script:tempDir
            $outputPath = Join-Path $script:tempDir 'output.txt'
            $fakeCompiler = New-FakeCompilerCore -Directory $script:tempDir -BrowserRulesContent "||ads.example.com^`n"
            Mock -ModuleName BloqrCompiler Get-BloqrCompilerCommand {
                param($ConfigPath, $OutputPath)
                $derivedBrowserOutput = if ($OutputPath.EndsWith('.txt')) { $OutputPath.Substring(0, $OutputPath.Length - 4) + '.browser.txt' } else { "$OutputPath.browser.txt" }
                @{ Executable = $fakeCompiler; Arguments = @('--config', $ConfigPath, '--output', $OutputPath, '--browser-output', $derivedBrowserOutput) }
            }.GetNewClosure()
            Mock -ModuleName BloqrCompiler Invoke-RulesValidator {
                param($Path)
                if ($Path -like '*.browser.txt') { return New-InvalidRulesValidatorResult }
                return New-ValidRulesValidatorResult
            }

            $result = Invoke-BloqrCompiler -ConfigPath $configPath -OutputPath $outputPath

            $escapedOutputPath = [regex]::Escape($outputPath)
            $result.Success | Should -Be $false
            $result.ErrorMessage | Should -Match 'Browser-syntax artifact'
            $result.ErrorMessage | Should -Match $escapedOutputPath
            Test-Path -LiteralPath $outputPath | Should -Be $true
        }

        It 'Copies both artifacts when -CopyToRules is set and a browser artifact was produced' {
            $configPath = New-TestConfig -Directory $script:tempDir
            $outputPath = Join-Path $script:tempDir 'output.txt'
            $rulesDir = Join-Path $script:tempDir 'rules'
            $fakeCompiler = New-FakeCompilerCore -Directory $script:tempDir -BrowserRulesContent "||ads.example.com^`n"
            Mock -ModuleName BloqrCompiler Get-BloqrCompilerCommand {
                param($ConfigPath, $OutputPath)
                $derivedBrowserOutput = if ($OutputPath.EndsWith('.txt')) { $OutputPath.Substring(0, $OutputPath.Length - 4) + '.browser.txt' } else { "$OutputPath.browser.txt" }
                @{ Executable = $fakeCompiler; Arguments = @('--config', $ConfigPath, '--output', $OutputPath, '--browser-output', $derivedBrowserOutput) }
            }.GetNewClosure()
            Mock -ModuleName BloqrCompiler Invoke-RulesValidator { New-ValidRulesValidatorResult }

            $result = Invoke-BloqrCompiler -ConfigPath $configPath -OutputPath $outputPath -CopyToRules -RulesDirectory $rulesDir

            $result.Success | Should -Be $true
            Test-Path -LiteralPath (Join-Path $rulesDir 'adguard_user_filter.txt') | Should -Be $true
            Test-Path -LiteralPath (Join-Path $rulesDir 'adguard_user_filter.browser.txt') | Should -Be $true
        }
    }
}
