#Requires -Version 7.0
using module ..\Classes\CompilerConfiguration.psm1

<#
.SYNOPSIS
    Tests for CompilerConfiguration's engine/defaultEngine handling (#439).

.DESCRIPTION
    Covers loading and validating the `defaultEngine` (top-level) and `engine`
    (per-source) fields introduced by the dual-engine epic (#432), matching the
    shared JSON Schema (schemas/compiler-config.schema.json) and the Rust/.NET/
    Python/TypeScript wrappers' equivalent fields.
#>

BeforeAll {
    function New-TestConfigFile {
        param(
            [Parameter(Mandatory = $true)][string]$Directory,
            [Parameter(Mandatory = $true)][hashtable]$ConfigData
        )

        $configPath = Join-Path $Directory 'compiler-config.json'
        ($ConfigData | ConvertTo-Json -Depth 10) | Set-Content -Path $configPath
        return $configPath
    }
}

Describe 'CompilerConfiguration engine/defaultEngine (#439)' {

    BeforeEach {
        $script:tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid())
        New-Item -ItemType Directory -Path $script:tempDir | Out-Null
    }

    AfterEach {
        Remove-Item -Path $script:tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'Leaves DefaultEngine null when not present in the config file' {
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name    = 'test-filter'
            sources = @(@{ source = 'https://example.com/list.txt' })
        }

        $config = [CompilerConfiguration]::new($configPath)

        $config.DefaultEngine | Should -BeNullOrEmpty
    }

    It 'Loads DefaultEngine from the defaultEngine JSON key' {
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name          = 'test-filter'
            defaultEngine = 'browser'
            sources       = @(@{ source = 'https://example.com/list.txt' })
        }

        $config = [CompilerConfiguration]::new($configPath)

        $config.DefaultEngine | Should -Be 'browser'
    }

    It 'Loads per-source engine from the sources array' {
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name    = 'test-filter'
            sources = @(
                @{ name = 'dns-source'; source = 'https://example.com/dns.txt'; engine = 'dns' }
                @{ name = 'browser-source'; source = 'https://example.com/browser.txt'; engine = 'browser' }
            )
        }

        $config = [CompilerConfiguration]::new($configPath)

        $config.Sources[0].engine | Should -Be 'dns'
        $config.Sources[1].engine | Should -Be 'browser'
    }

    It 'Passes validation with a valid DefaultEngine' {
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name          = 'test-filter'
            defaultEngine = 'dns'
            sources       = @(@{ source = 'https://example.com/list.txt' })
        }
        $config = [CompilerConfiguration]::new($configPath)

        { $config.Validate() } | Should -Not -Throw
    }

    It 'Fails validation with an invalid DefaultEngine' {
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name          = 'test-filter'
            defaultEngine = 'not-a-real-engine'
            sources       = @(@{ source = 'https://example.com/list.txt' })
        }
        $config = [CompilerConfiguration]::new($configPath)

        { $config.Validate() } | Should -Throw '*defaultEngine*'
    }

    It 'Fails validation with an invalid per-source engine' {
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name    = 'test-filter'
            sources = @(@{ name = 'bad-source'; source = 'https://example.com/list.txt'; engine = 'not-a-real-engine' })
        }
        $config = [CompilerConfiguration]::new($configPath)

        { $config.Validate() } | Should -Throw '*engine*'
    }

    It 'Passes validation when engine/defaultEngine are absent (existing all-DNS configs)' {
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name    = 'test-filter'
            sources = @(@{ source = 'https://example.com/list.txt' })
        }
        $config = [CompilerConfiguration]::new($configPath)

        { $config.Validate() } | Should -Not -Throw
    }

    It 'Includes DefaultEngine in ToHashtable' {
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name          = 'test-filter'
            defaultEngine = 'browser'
            sources       = @(@{ source = 'https://example.com/list.txt' })
        }
        $config = [CompilerConfiguration]::new($configPath)

        $config.ToHashtable().DefaultEngine | Should -Be 'browser'
    }
}

Describe 'CompilerConfiguration transformation validation (#502)' {

    BeforeEach {
        $script:tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid())
        New-Item -ItemType Directory -Path $script:tempDir | Out-Null
    }

    AfterEach {
        Remove-Item -Path $script:tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'Passes validation with every documented transformation' {
        # Explicit list (not [CompilerConfiguration]::ValidTransformations itself) so this
        # test actually checks the documented contract - an accidental omission or typo in
        # the implementation's allowlist would otherwise still pass trivially.
        $documentedTransformations = @(
            'RemoveComments',
            'Compress',
            'RemoveModifiers',
            'Validate',
            'ValidateAllowIp',
            'Deduplicate',
            'InvertAllow',
            'RemoveEmptyLines',
            'TrimLines',
            'InsertFinalNewLine',
            'ConvertToAscii'
        )
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name            = 'test-filter'
            sources         = @(@{ source = 'https://example.com/list.txt' })
            transformations = $documentedTransformations
        }
        $config = [CompilerConfiguration]::new($configPath)

        { $config.Validate() } | Should -Not -Throw
    }

    It 'Fails validation with an unknown global transformation' {
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name            = 'test-filter'
            sources         = @(@{ source = 'https://example.com/list.txt' })
            transformations = @('NotARealTransformation')
        }
        $config = [CompilerConfiguration]::new($configPath)

        { $config.Validate() } | Should -Throw '*NotARealTransformation*'
    }

    It 'Fails validation with a commercial-only transformation this toolkit does not implement' {
        # ConflictDetection/RuleOptimizer are browser-engine, commercial-only
        # transformations that exist in the TypeScript reference implementation's
        # TransformationType enum but are not registered/implemented anywhere in
        # this OSS repo - accepting them here would silently no-op at compile time.
        # Each is asserted independently so an accidental allowlist entry for one
        # can't slip through unnoticed because only the other was tested.
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name            = 'test-filter'
            sources         = @(@{ source = 'https://example.com/list.txt' })
            transformations = @('ConflictDetection')
        }
        $config = [CompilerConfiguration]::new($configPath)

        { $config.Validate() } | Should -Throw '*ConflictDetection*'

        $configPath2 = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name            = 'test-filter'
            sources         = @(@{ source = 'https://example.com/list.txt' })
            transformations = @('RuleOptimizer')
        }
        $config2 = [CompilerConfiguration]::new($configPath2)

        { $config2.Validate() } | Should -Throw '*RuleOptimizer*'
    }

    It 'Fails validation with an unknown per-source transformation' {
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name    = 'test-filter'
            sources = @(@{ name = 'bad-source'; source = 'https://example.com/list.txt'; transformations = @('NotARealTransformation') })
        }
        $config = [CompilerConfiguration]::new($configPath)

        { $config.Validate() } | Should -Throw '*NotARealTransformation*'
    }
}
