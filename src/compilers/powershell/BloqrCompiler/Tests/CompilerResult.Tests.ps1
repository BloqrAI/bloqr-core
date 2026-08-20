#Requires -Version 7.0
using module ..\Classes\CompilerResult.psm1

Describe "CompilerResult Class Tests" {
    
    Context "Constructors" {
        
        It "Should create default instance with failure state" {
            $result = [CompilerResult]::new()
            
            $result.Success | Should -Be $false
            $result.RuleCount | Should -Be 0
            $result.OutputPath | Should -BeNullOrEmpty
            $result.Hash | Should -BeNullOrEmpty
            $result.ElapsedMs | Should -Be 0
            $result.ConfigFormat | Should -Be 'json'
            $result.Timestamp | Should -Not -BeNullOrEmpty
        }
        
        It "Should create instance with success and rule count" {
            $result = [CompilerResult]::new($true, 100)
            
            $result.Success | Should -Be $true
            $result.RuleCount | Should -Be 100
        }
        
        It "Should create instance with all parameters" {
            $result = [CompilerResult]::new($true, 150, 'output.txt', 'ABC123', 5000, 'yaml')
            
            $result.Success | Should -Be $true
            $result.RuleCount | Should -Be 150
            $result.OutputPath | Should -Be 'output.txt'
            $result.Hash | Should -Be 'ABC123'
            $result.ElapsedMs | Should -Be 5000
            $result.ConfigFormat | Should -Be 'yaml'
        }
    }
    
    Context "Static Factory Methods" {
        
        It "Should create failure result via CreateFailure" {
            $result = [CompilerResult]::CreateFailure("Test error")
            
            $result.Success | Should -Be $false
            $result.ErrorMessage | Should -Be "Test error"
        }
        
        It "Should create success result via CreateSuccess" {
            $result = [CompilerResult]::CreateSuccess(200, 'test.txt', 'HASH456', 3000)
            
            $result.Success | Should -Be $true
            $result.RuleCount | Should -Be 200
            $result.OutputPath | Should -Be 'test.txt'
            $result.Hash | Should -Be 'HASH456'
            $result.ElapsedMs | Should -Be 3000
        }
    }
    
    Context "Methods" {
        
        It "Should convert to hashtable" {
            $result = [CompilerResult]::CreateSuccess(100, 'output.txt', 'HASH', 1000)
            $hash = $result.ToHashtable()
            
            $hash | Should -BeOfType [hashtable]
            $hash.Success | Should -Be $true
            $hash.RuleCount | Should -Be 100
            $hash.OutputPath | Should -Be 'output.txt'
            $hash.Hash | Should -Be 'HASH'
        }
        
        It "Should convert to JSON" {
            $result = [CompilerResult]::CreateSuccess(50, 'test.txt', 'ABC', 500)
            $json = $result.ToJson()
            
            $json | Should -Not -BeNullOrEmpty
            $json | Should -Match '"Success"'
            $json | Should -Match '"RuleCount"'
        }
        
        It "Should have meaningful ToString for success" {
            $result = [CompilerResult]::CreateSuccess(100, 'output.txt', 'HASH', 2000)
            $string = $result.ToString()
            
            $string | Should -Match "Success"
            $string | Should -Match "100 rules"
            $string | Should -Match "2000ms"
        }
        
        It "Should have meaningful ToString for failure" {
            $result = [CompilerResult]::CreateFailure("Compilation failed")
            $string = $result.ToString()
            
            $string | Should -Match "Failed"
            $string | Should -Match "Compilation failed"
        }
        
        It "Should validate successful result" {
            $result = [CompilerResult]::CreateSuccess(100, 'output.txt', 'HASH', 1000)
            $result.IsValid() | Should -Be $true
        }
        
        It "Should validate successful result requires OutputPath" {
            $result = [CompilerResult]::new($true, 100)
            $result.IsValid() | Should -Be $false
        }
    }
    
    Context "GetFormattedDuration" {
        
        It "Should format duration in milliseconds" {
            $result = [CompilerResult]::new($true, 100)
            $result.ElapsedMs = 500
            
            $result.GetFormattedDuration() | Should -Be "500ms"
        }
        
        It "Should format duration in seconds" {
            $result = [CompilerResult]::new($true, 100)
            $result.ElapsedMs = 5500
            
            $formatted = $result.GetFormattedDuration()
            $formatted | Should -Match "^\d+\.\d+s$"
        }
        
        It "Should format duration in minutes and seconds" {
            $result = [CompilerResult]::new($true, 100)
            $result.ElapsedMs = 125000  # 125 seconds = 2m 5s
            
            $formatted = $result.GetFormattedDuration()
            $formatted | Should -Match "2m \d+s"
        }
    }
    
    Context "Properties" {
        
        It "Should allow setting ErrorMessage" {
            $result = [CompilerResult]::new()
            $result.ErrorMessage = "Custom error"
            
            $result.ErrorMessage | Should -Be "Custom error"
        }
        
        It "Should allow setting CompilerOutput" {
            $result = [CompilerResult]::new()
            $result.CompilerOutput = "Compiler output text"
            
            $result.CompilerOutput | Should -Be "Compiler output text"
        }
        
        It "Should track timestamp" {
            $before = Get-Date
            $result = [CompilerResult]::new()
            $after = Get-Date
            
            $result.Timestamp | Should -BeGreaterOrEqual $before
            $result.Timestamp | Should -BeLessOrEqual $after
        }
    }
}
