#Requires -Modules Pester
using module ..\Classes\RulesValidatorResult.psm1

Describe 'RulesValidatorResult Class Tests' {

    Context 'Constructor' {

        It 'Should create default instance with invalid state' {
            $result = [RulesValidatorResult]::new()

            $result.IsValid | Should -Be $false
            $result.Format | Should -Be 'unknown'
            $result.ValidRules | Should -Be 0
            $result.InvalidRules | Should -Be 0
            $result.Messages | Should -BeNullOrEmpty
            $result.Timestamp | Should -Not -BeNullOrEmpty
        }
    }

    Context 'FromCliOutput' {

        It 'Should build a passing result from valid CLI output' {
            $parsed = [PSCustomObject]@{
                is_valid      = $true
                format        = 'adblock'
                valid_rules   = 5
                invalid_rules = 0
                messages      = @()
            }

            $result = [RulesValidatorResult]::FromCliOutput($parsed)

            $result.IsValid | Should -Be $true
            $result.Format | Should -Be 'adblock'
            $result.ValidRules | Should -Be 5
            $result.InvalidRules | Should -Be 0
            $result.Messages | Should -BeNullOrEmpty
        }

        It 'Should build a failing result with messages from invalid CLI output' {
            $parsed = [PSCustomObject]@{
                is_valid      = $false
                format        = 'unknown'
                valid_rules   = 0
                invalid_rules = 1
                messages      = @('Line 1: Invalid syntax: bad rule', 'No valid rules found')
            }

            $result = [RulesValidatorResult]::FromCliOutput($parsed)

            $result.IsValid | Should -Be $false
            $result.InvalidRules | Should -Be 1
            $result.Messages.Count | Should -Be 2
            $result.Messages[0] | Should -Match 'Invalid syntax'
        }
    }

    Context 'Methods' {

        It 'Should convert to hashtable' {
            $result = [RulesValidatorResult]::new()
            $result.IsValid = $true
            $result.ValidRules = 3

            $hash = $result.ToHashtable()

            $hash | Should -BeOfType [hashtable]
            $hash.IsValid | Should -Be $true
            $hash.ValidRules | Should -Be 3
        }

        It 'Should convert to JSON' {
            $result = [RulesValidatorResult]::new()
            $json = $result.ToJson()

            $json | Should -Not -BeNullOrEmpty
            $json | Should -Match '"IsValid"'
            $json | Should -Match '"ValidRules"'
        }

        It 'Should have meaningful ToString for valid result' {
            $result = [RulesValidatorResult]::new()
            $result.IsValid = $true
            $result.ValidRules = 10

            $result.ToString() | Should -Match 'Valid'
        }

        It 'Should have meaningful ToString for invalid result' {
            $result = [RulesValidatorResult]::new()
            $result.IsValid = $false
            $result.InvalidRules = 2

            $result.ToString() | Should -Match 'Invalid'
        }
    }
}
