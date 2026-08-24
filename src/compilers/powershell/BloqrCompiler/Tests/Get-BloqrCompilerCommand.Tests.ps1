#Requires -Modules Pester

<#
.SYNOPSIS
    Tests for Get-BloqrCompilerCommand and Get-BloqrBrowserOutputPath (#439).

.DESCRIPTION
    Both are module-private functions, so all calls run via InModuleScope.
    Get-BloqrCompilerCommand resolves `deno` on PATH, so most of these tests are
    skipped (rather than failed) when `deno` isn't installed in the sandbox -
    mirroring the Rust wrapper's `if find_command("deno").is_none() { return; }`
    guard in its own equivalent tests. The "deno not found" case is still covered
    unconditionally by mocking `Get-Command` to hide `deno`.
#>

BeforeAll {
    $script:CommonManifest = Join-Path $PSScriptRoot '..' '..' 'Common' 'Common.psd1'
    $script:ModuleManifest = Join-Path $PSScriptRoot '..' 'BloqrCompiler.psd1'
    Import-Module $script:CommonManifest -Force
    Import-Module $script:ModuleManifest -Force

    $script:DenoAvailable = [bool](Get-Command -Name 'deno' -ErrorAction SilentlyContinue)
}

Describe 'Get-BloqrCompilerCommand' {

    It 'Returns $null when deno is not on PATH' {
        InModuleScope BloqrCompiler {
            Mock Get-Command { $null } -ParameterFilter { $Name -eq 'deno' }
            $result = Get-BloqrCompilerCommand
            $result | Should -Be $null
        }
    }

    It 'Builds a base command with no --config/--output/--engine/--browser-output when none are passed' -Skip:(-not $script:DenoAvailable) {
        InModuleScope BloqrCompiler {
            $result = Get-BloqrCompilerCommand
            $result | Should -Not -Be $null
            $result.Arguments | Should -Contain 'jsr:@bloqr/compiler-core/cli'
            $result.Arguments | Should -Not -Contain '--config'
            $result.Arguments | Should -Not -Contain '--engine'
            $result.Arguments | Should -Not -Contain '--browser-output'
        }
    }

    It 'Appends --config and --output when both are passed' -Skip:(-not $script:DenoAvailable) {
        InModuleScope BloqrCompiler {
            $result = Get-BloqrCompilerCommand -ConfigPath 'config.json' -OutputPath 'output.txt'
            $configIdx = $result.Arguments.IndexOf('--config')
            $outputIdx = $result.Arguments.IndexOf('--output')
            $configIdx | Should -BeGreaterOrEqual 0
            $result.Arguments[$configIdx + 1] | Should -Be 'config.json'
            $outputIdx | Should -BeGreaterOrEqual 0
            $result.Arguments[$outputIdx + 1] | Should -Be 'output.txt'
        }
    }

    It 'Omits --engine when Engine is not passed' -Skip:(-not $script:DenoAvailable) {
        InModuleScope BloqrCompiler {
            $result = Get-BloqrCompilerCommand -ConfigPath 'config.json' -OutputPath 'output.txt'
            $result.Arguments | Should -Not -Contain '--engine'
        }
    }

    It 'Omits --engine when Engine is "auto" (case-insensitive)' -Skip:(-not $script:DenoAvailable) {
        InModuleScope BloqrCompiler {
            $result = Get-BloqrCompilerCommand -ConfigPath 'config.json' -OutputPath 'output.txt' -Engine 'AUTO'
            $result.Arguments | Should -Not -Contain '--engine'
        }
    }

    It 'Includes --engine when Engine is "dns" or "browser"' -Skip:(-not $script:DenoAvailable) {
        InModuleScope BloqrCompiler {
            $result = Get-BloqrCompilerCommand -ConfigPath 'config.json' -OutputPath 'output.txt' -Engine 'browser'
            $engineIdx = $result.Arguments.IndexOf('--engine')
            $engineIdx | Should -BeGreaterOrEqual 0
            $result.Arguments[$engineIdx + 1] | Should -Be 'browser'
        }
    }

    It 'Omits --browser-output when BrowserOutputPath is not passed' -Skip:(-not $script:DenoAvailable) {
        InModuleScope BloqrCompiler {
            $result = Get-BloqrCompilerCommand -ConfigPath 'config.json' -OutputPath 'output.txt'
            $result.Arguments | Should -Not -Contain '--browser-output'
        }
    }

    It 'Includes --browser-output only when BrowserOutputPath is passed explicitly' -Skip:(-not $script:DenoAvailable) {
        InModuleScope BloqrCompiler {
            $result = Get-BloqrCompilerCommand -ConfigPath 'config.json' -OutputPath 'output.txt' -BrowserOutputPath 'output.browser.txt'
            $browserIdx = $result.Arguments.IndexOf('--browser-output')
            $browserIdx | Should -BeGreaterOrEqual 0
            $result.Arguments[$browserIdx + 1] | Should -Be 'output.browser.txt'
        }
    }

    It 'Produces a byte-identical base command line to the no-engine-specified case for an all-DNS config' -Skip:(-not $script:DenoAvailable) {
        InModuleScope BloqrCompiler {
            $withoutEngine = Get-BloqrCompilerCommand -ConfigPath 'config.json' -OutputPath 'output.txt'
            $withAutoEngine = Get-BloqrCompilerCommand -ConfigPath 'config.json' -OutputPath 'output.txt' -Engine 'auto'

            ($withoutEngine.Arguments -join ' ') | Should -Be ($withAutoEngine.Arguments -join ' ')
        }
    }
}

Describe 'Get-BloqrBrowserOutputPath' {

    It 'Replaces a .txt suffix with .browser.txt' {
        InModuleScope BloqrCompiler {
            Get-BloqrBrowserOutputPath -OutputPath '/out/rules.txt' | Should -Be '/out/rules.browser.txt'
        }
    }

    It 'Appends .browser.txt when there is no .txt extension' {
        InModuleScope BloqrCompiler {
            Get-BloqrBrowserOutputPath -OutputPath '/out/rules' | Should -Be '/out/rules.browser.txt'
        }
    }

    It 'Appends .browser.txt for a non-.txt extension' {
        InModuleScope BloqrCompiler {
            Get-BloqrBrowserOutputPath -OutputPath '/out/rules.json' | Should -Be '/out/rules.json.browser.txt'
        }
    }
}
