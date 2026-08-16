#Requires -Modules Pester

<#
.SYNOPSIS
    Tests for the Invoke-RulesValidator bloqr-validator CLI wiring (#361).

.DESCRIPTION
    Invoke-RulesValidator shells out to the `bloqr-validate` binary rather than
    binding against a native library directly, so these tests mock
    Find-RulesValidateBinary (the module-private binary-discovery helper) with a
    small fake shell script that emits canned `--json file` output - keeping the
    tests deterministic and independent of whether the Rust workspace has been
    built locally, mirroring the approach used for the Rust/Python/TypeScript
    bloqr-validator wiring.
#>

BeforeAll {
    $script:CommonManifest = Join-Path $PSScriptRoot '..' '..' 'Common' 'Common.psd1'
    $script:ModuleManifest = Join-Path $PSScriptRoot '..' 'RulesCompiler.psd1'
    Import-Module $script:CommonManifest -Force
    Import-Module $script:ModuleManifest -Force

    function New-FakeRulesValidateBinary {
        param(
            [Parameter(Mandatory = $true)][string]$Directory,
            [Parameter(Mandatory = $true)][string]$Stdout,
            [int]$ExitCode = 0
        )

        $scriptPath = Join-Path $Directory 'bloqr-validate'
        $body = "#!/bin/sh`ncat <<'RVEOF'`n$Stdout`nRVEOF`nexit $ExitCode`n"
        Set-Content -Path $scriptPath -Value $body -NoNewline
        if (-not $IsWindows) {
            & chmod +x $scriptPath
        }
        return $scriptPath
    }
}

Describe 'Invoke-RulesValidator' {

    BeforeEach {
        $script:tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid())
        New-Item -ItemType Directory -Path $script:tempDir | Out-Null
        $script:outputFile = Join-Path $script:tempDir 'output.txt'
        Set-Content -Path $script:outputFile -Value '||example.com^'
    }

    AfterEach {
        Remove-Item -Path $script:tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'Returns $null when the binary cannot be found' {
        Mock -ModuleName RulesCompiler Find-RulesValidateBinary { $null }

        $result = Invoke-RulesValidator -Path $script:outputFile

        $result | Should -BeNullOrEmpty
    }

    It 'Returns a passing result for valid syntax' {
        $binary = New-FakeRulesValidateBinary -Directory $script:tempDir `
            -Stdout '{"is_valid":true,"format":"adblock","valid_rules":1,"invalid_rules":0,"messages":[]}'
        Mock -ModuleName RulesCompiler Find-RulesValidateBinary { $binary }.GetNewClosure()

        $result = Invoke-RulesValidator -Path $script:outputFile

        $result | Should -Not -BeNullOrEmpty
        $result.IsValid | Should -Be $true
        $result.ValidRules | Should -Be 1
        $result.InvalidRules | Should -Be 0
        $result.Messages | Should -BeNullOrEmpty
    }

    It 'Returns a failing result with messages for invalid syntax' {
        $binary = New-FakeRulesValidateBinary -Directory $script:tempDir -ExitCode 1 `
            -Stdout '{"is_valid":false,"format":"unknown","valid_rules":0,"invalid_rules":1,"messages":["line 1: bad rule"]}'
        Mock -ModuleName RulesCompiler Find-RulesValidateBinary { $binary }.GetNewClosure()

        $result = Invoke-RulesValidator -Path $script:outputFile

        $result.IsValid | Should -Be $false
        $result.InvalidRules | Should -Be 1
        $result.Messages | Should -Contain 'line 1: bad rule'
    }

    It 'Passes a HashDatabasePath override through to the CLI invocation' {
        $binary = New-FakeRulesValidateBinary -Directory $script:tempDir `
            -Stdout '{"is_valid":true,"format":"adblock","valid_rules":1,"invalid_rules":0,"messages":[]}'
        Mock -ModuleName RulesCompiler Find-RulesValidateBinary { $binary }.GetNewClosure()
        $customHashDb = Join-Path $script:tempDir 'custom-hashes.json'

        $result = Invoke-RulesValidator -Path $script:outputFile -HashDatabasePath $customHashDb

        $result.IsValid | Should -Be $true
    }

    It 'Returns $null when the CLI produces non-JSON output' {
        $binary = New-FakeRulesValidateBinary -Directory $script:tempDir -ExitCode 1 -Stdout 'not json'
        Mock -ModuleName RulesCompiler Find-RulesValidateBinary { $binary }.GetNewClosure()

        $result = Invoke-RulesValidator -Path $script:outputFile

        $result | Should -BeNullOrEmpty
    }
}
