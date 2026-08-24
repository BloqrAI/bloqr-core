#Requires -Modules Pester
using module ..\Classes\CompilerConfiguration.psm1

<#
.SYNOPSIS
    Tests for Split-BloqrCompilerConfiguration's defaultEngine threading (#439).

.DESCRIPTION
    Split-BloqrCompilerConfiguration is module-private, so it's invoked via
    InModuleScope. Source-chunking behavior itself is already covered indirectly
    by Invoke-BloqrCompilerChunked.Tests.ps1; these tests focus on the #439-added
    defaultEngine field being threaded through to each chunk.
#>

BeforeAll {
    $script:CommonManifest = Join-Path $PSScriptRoot '..' '..' 'Common' 'Common.psd1'
    $script:ModuleManifest = Join-Path $PSScriptRoot '..' 'BloqrCompiler.psd1'
    Import-Module $script:CommonManifest -Force
    Import-Module $script:ModuleManifest -Force
}

Describe 'Split-BloqrCompilerConfiguration defaultEngine threading (#439)' {

    It 'Threads DefaultEngine through to every chunk' {
        InModuleScope BloqrCompiler {
            $config = [CompilerConfiguration]::new()
            $config.Name = 'test-filter'
            $config.DefaultEngine = 'browser'
            $config.Sources = @(
                [PSCustomObject]@{ name = 'a'; source = 'https://example.com/a.txt' },
                [PSCustomObject]@{ name = 'b'; source = 'https://example.com/b.txt' },
                [PSCustomObject]@{ name = 'c'; source = 'https://example.com/c.txt' }
            )

            $chunks = Split-BloqrCompilerConfiguration -Config $config -MaxParallel 2

            $chunks.Count | Should -BeGreaterThan 0
            foreach ($chunk in $chunks) {
                $chunk.defaultEngine | Should -Be 'browser'
            }
        }
    }

    It 'Leaves defaultEngine null on each chunk when the source config has none set' {
        InModuleScope BloqrCompiler {
            $config = [CompilerConfiguration]::new()
            $config.Name = 'test-filter'
            $config.Sources = @(
                [PSCustomObject]@{ name = 'a'; source = 'https://example.com/a.txt' }
            )

            $chunks = Split-BloqrCompilerConfiguration -Config $config -MaxParallel 2

            $chunks[0].defaultEngine | Should -BeNullOrEmpty
        }
    }
}
