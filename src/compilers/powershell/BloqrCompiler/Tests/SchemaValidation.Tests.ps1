#Requires -Version 7.0
using module ..\Classes\CompilerConfiguration.psm1

<#
.SYNOPSIS
    Tests for JSON Schema config validation (#518, follow-up to #502).

.DESCRIPTION
    Covers the bundled schema drift guard (Tests/../schemas/compiler-config.schema.json vs.
    the repo-root schemas/compiler-config.schema.json) and accept/reject behavior for both
    CompilerConfiguration's file-loading path and its Validate() method, matching the .NET,
    TypeScript, Python, Rust, and Swift compilers' equivalent coverage.
#>

BeforeAll {
    function New-TestConfigFile {
        param(
            [Parameter(Mandatory = $true)][string]$Directory,
            [Parameter(Mandatory = $true)]$ConfigData
        )

        $configPath = Join-Path $Directory 'compiler-config.json'
        ($ConfigData | ConvertTo-Json -Depth 10) | Set-Content -Path $configPath
        return $configPath
    }

    $script:BundledSchemaPath = Join-Path $PSScriptRoot '..' 'schemas' 'compiler-config.schema.json'
    # Tests/ -> BloqrCompiler/ -> powershell/ -> compilers/ -> src/ -> repo root (5 levels)
    $script:CanonicalSchemaPath = Join-Path $PSScriptRoot '..' '..' '..' '..' '..' 'schemas' 'compiler-config.schema.json'
}

Describe 'Bundled schema drift guard' {
    It 'Stays in sync with the repo-root canonical schema' {
        Test-Path $script:CanonicalSchemaPath | Should -BeTrue -Because 'the canonical schema should exist at the expected repo-root path'

        $bundled = Get-Content -LiteralPath $script:BundledSchemaPath -Raw
        $canonical = Get-Content -LiteralPath $script:CanonicalSchemaPath -Raw

        $bundled | Should -Be $canonical -Because 'src/compilers/powershell/BloqrCompiler/schemas/compiler-config.schema.json has drifted from schemas/compiler-config.schema.json - copy the canonical file over the bundled one'
    }
}

Describe 'CompilerConfiguration schema validation (#518)' {
    BeforeEach {
        $script:tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid())
        New-Item -ItemType Directory -Path $script:tempDir | Out-Null
    }

    AfterEach {
        Remove-Item -Path $script:tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'Loads a minimal schema-valid configuration' {
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name    = 'test-filter'
            sources = @(@{ source = 'https://example.com/list.txt' })
        }

        { [CompilerConfiguration]::new($configPath) } | Should -Not -Throw
    }

    It 'Rejects a config file with an unrecognized top-level property' {
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name        = 'test-filter'
            sources     = @(@{ source = 'https://example.com/list.txt' })
            bogusField  = 'not in the schema'
        }

        { [CompilerConfiguration]::new($configPath) } | Should -Throw '*schema*'
    }

    It 'Rejects a config file with an unrecognized source property' {
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name    = 'test-filter'
            sources = @(@{ source = 'https://example.com/list.txt'; bogus = $true })
        }

        { [CompilerConfiguration]::new($configPath) } | Should -Throw '*schema*'
    }

    It 'Rejects a config file with a schema-invalid transformation name' {
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name            = 'test-filter'
            sources         = @(@{ source = 'https://example.com/list.txt' })
            transformations = @('NotARealTransformation')
        }

        { [CompilerConfiguration]::new($configPath) } | Should -Throw '*schema*'
    }

    It 'Accepts a non-URI homepage (documented Test-Json limitation)' {
        # Unlike ajv (TypeScript, via ajv-formats), Python's jsonschema (via format_checker +
        # rfc3987), the Rust jsonschema crate (via should_validate_formats(true)), and
        # JSONSchema.swift (which enforces "format" by default), PowerShell 7's built-in
        # Test-Json cmdlet does not evaluate the "format" keyword at all and exposes no parameter
        # to opt in - confirmed empirically (this test failed with no exception thrown before this
        # comment was added). This is a real, currently-unclosed gap versus the other four
        # wrappers in this epic: a schema-invalid homepage value passes here. Every other
        # keyword (type, enum, required, additionalProperties, pattern, minLength/minItems, etc.)
        # is still enforced normally, as the rest of this file demonstrates.
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name     = 'test-filter'
            homepage = 'not a uri'
            sources  = @(@{ source = 'https://example.com/list.txt' })
        }

        { [CompilerConfiguration]::new($configPath) } | Should -Not -Throw
    }

    It 'Accepts ConflictDetection/RuleOptimizer at the schema layer (rejected later by Validate())' {
        # Regression coverage: the canonical schema's enum includes these (the TS reference
        # implementation supports them) even though this wrapper's own Validate() rejects them as
        # unsupported by this toolkit - both are correct at their own layer.
        $configPath = New-TestConfigFile -Directory $script:tempDir -ConfigData @{
            name            = 'test-filter'
            sources         = @(@{ source = 'https://example.com/list.txt' })
            transformations = @('ConflictDetection', 'RuleOptimizer')
        }

        { [CompilerConfiguration]::new($configPath) } | Should -Not -Throw

        $config = [CompilerConfiguration]::new($configPath)
        { $config.Validate() } | Should -Throw '*ConflictDetection*'
    }

    It 'Rejects a directly-constructed configuration with a schema-invalid version string via Validate()' {
        # Regression coverage: schema validation must also run for a CompilerConfiguration that
        # never went through LoadFromFile - not just the file-based path.
        $config = [CompilerConfiguration]::new()
        $config.Name = 'test-filter'
        $config.Version = 'not-a-semver'
        $config.Sources = @([PSCustomObject]@{ source = 'https://example.com/list.txt' })

        { $config.Validate() } | Should -Throw '*schema*'
    }

    It 'Passes Validate() for a directly-constructed schema-valid configuration' {
        $config = [CompilerConfiguration]::new()
        $config.Name = 'test-filter'
        $config.Sources = @([PSCustomObject]@{ source = 'https://example.com/list.txt' })

        { $config.Validate() } | Should -Not -Throw
    }
}
