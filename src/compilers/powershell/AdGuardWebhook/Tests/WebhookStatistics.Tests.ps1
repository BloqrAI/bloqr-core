#Requires -Version 7.0
using module ..\Classes\WebhookStatistics.psm1

Describe "WebhookStatistics Class Tests" {
    
    Context "Constructor and Initialization" {
        
        It "Should create instance with zero values" {
            $stats = [WebhookStatistics]::new()
            
            $stats.TotalAttempts | Should -Be 0
            $stats.SuccessfulAttempts | Should -Be 0
            $stats.FailedAttempts | Should -Be 0
            $stats.SuccessRate | Should -Be 0.0
            $stats.TotalElapsedMs | Should -Be 0
            $stats.AverageResponseMs | Should -Be 0
            $stats.MinResponseMs | Should -Be ([long]::MaxValue)
            $stats.MaxResponseMs | Should -Be 0
        }
        
        It "Should initialize timestamps" {
            $before = Get-Date
            $stats = [WebhookStatistics]::new()
            $after = Get-Date
            
            $stats.StartTime | Should -BeGreaterOrEqual $before
            $stats.StartTime | Should -BeLessOrEqual $after
        }
    }
    
    Context "RecordSuccess" {
        
        It "Should record successful attempt" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordSuccess(100)
            
            $stats.TotalAttempts | Should -Be 1
            $stats.SuccessfulAttempts | Should -Be 1
            $stats.FailedAttempts | Should -Be 0
            $stats.SuccessRate | Should -Be 100.0
        }
        
        It "Should track min response time" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordSuccess(500)
            $stats.RecordSuccess(200)
            $stats.RecordSuccess(800)
            
            $stats.MinResponseMs | Should -Be 200
        }
        
        It "Should track max response time" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordSuccess(500)
            $stats.RecordSuccess(200)
            $stats.RecordSuccess(800)
            
            $stats.MaxResponseMs | Should -Be 800
        }
        
        It "Should calculate average response time" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordSuccess(100)
            $stats.RecordSuccess(200)
            $stats.RecordSuccess(300)
            
            $stats.AverageResponseMs | Should -Be 200
        }
    }
    
    Context "RecordFailure" {
        
        It "Should record failed attempt" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordFailure()
            
            $stats.TotalAttempts | Should -Be 1
            $stats.SuccessfulAttempts | Should -Be 0
            $stats.FailedAttempts | Should -Be 1
            $stats.SuccessRate | Should -Be 0.0
        }
        
        It "Should not affect response times" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordSuccess(100)
            $stats.RecordFailure()
            
            $stats.AverageResponseMs | Should -Be 100
            $stats.MinResponseMs | Should -Be 100
            $stats.MaxResponseMs | Should -Be 100
        }
    }
    
    Context "Calculate Success Rate" {
        
        It "Should calculate 100% success rate" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordSuccess(100)
            $stats.RecordSuccess(200)
            $stats.RecordSuccess(150)
            
            $stats.SuccessRate | Should -Be 100.0
        }
        
        It "Should calculate 50% success rate" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordSuccess(100)
            $stats.RecordFailure()
            
            $stats.SuccessRate | Should -Be 50.0
        }
        
        It "Should calculate mixed success rate" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordSuccess(100)
            $stats.RecordSuccess(200)
            $stats.RecordSuccess(150)
            $stats.RecordFailure()
            
            $stats.SuccessRate | Should -Be 75.0
        }
    }
    
    Context "Reset" {
        
        It "Should reset all statistics" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordSuccess(100)
            $stats.RecordSuccess(200)
            $stats.RecordFailure()
            
            $stats.Reset()
            
            $stats.TotalAttempts | Should -Be 0
            $stats.SuccessfulAttempts | Should -Be 0
            $stats.FailedAttempts | Should -Be 0
            $stats.SuccessRate | Should -Be 0.0
            $stats.ResponseTimes.Count | Should -Be 0
        }
    }
    
    Context "Conversion Methods" {
        
        It "Should convert to hashtable" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordSuccess(100)
            
            $hash = $stats.ToHashtable()
            
            $hash | Should -BeOfType [hashtable]
            $hash.TotalAttempts | Should -Be 1
            $hash.SuccessfulAttempts | Should -Be 1
            $hash.SuccessRate | Should -Not -BeNullOrEmpty
        }
        
        It "Should convert to JSON" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordSuccess(100)
            
            $json = $stats.ToJson()
            
            $json | Should -Not -BeNullOrEmpty
            $json | Should -Match '"TotalAttempts"'
            $json | Should -Match '"SuccessRate"'
        }
        
        It "Should have meaningful ToString" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordSuccess(100)
            $stats.RecordSuccess(200)
            
            $string = $stats.ToString()
            
            $string | Should -Match "2/2 successful"
            $string | Should -Match "100\.00%"
        }
    }
    
    Context "Helper Methods" {
        
        It "Should format success rate" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordSuccess(100)
            $stats.RecordSuccess(200)
            $stats.RecordFailure()
            
            $formatted = $stats.GetFormattedSuccessRate()
            $formatted | Should -Match "66\.67%"
        }
        
        It "Should detect failures" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordSuccess(100)
            $stats.RecordFailure()
            
            $stats.HasFailures() | Should -Be $true
        }
        
        It "Should detect all successful" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordSuccess(100)
            $stats.RecordSuccess(200)
            
            $stats.AllSuccessful() | Should -Be $true
        }
        
        It "Should not be all successful with failures" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordSuccess(100)
            $stats.RecordFailure()
            
            $stats.AllSuccessful() | Should -Be $false
        }
    }
    
    Context "Merge" {
        
        It "Should merge two statistics" {
            $stats1 = [WebhookStatistics]::new()
            $stats1.RecordSuccess(100)
            $stats1.RecordSuccess(200)
            
            $stats2 = [WebhookStatistics]::new()
            $stats2.RecordSuccess(150)
            $stats2.RecordFailure()
            
            $stats1.Merge($stats2)
            
            $stats1.TotalAttempts | Should -Be 4
            $stats1.SuccessfulAttempts | Should -Be 3
            $stats1.FailedAttempts | Should -Be 1
            $stats1.SuccessRate | Should -Be 75.0
        }
        
        It "Should update min/max when merging" {
            $stats1 = [WebhookStatistics]::new()
            $stats1.RecordSuccess(500)
            
            $stats2 = [WebhookStatistics]::new()
            $stats2.RecordSuccess(100)
            $stats2.RecordSuccess(800)
            
            $stats1.Merge($stats2)
            
            $stats1.MinResponseMs | Should -Be 100
            $stats1.MaxResponseMs | Should -Be 800
        }
    }
    
    Context "ToTableObject" {
        
        It "Should create table-friendly object" {
            $stats = [WebhookStatistics]::new()
            $stats.RecordSuccess(100)
            $stats.RecordSuccess(200)
            
            $tableObj = $stats.ToTableObject()
            
            $tableObj.'Total Attempts' | Should -Be 2
            $tableObj.'Successful' | Should -Be 2
            $tableObj.'Failed' | Should -Be 0
            $tableObj.'Success Rate' | Should -Match "%"
        }
    }
}
