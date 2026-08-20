#Requires -Modules Pester
using module ..\Classes\CompilerLogger.psm1

Describe 'CompilerLogger Class Tests' {
    Context 'Constructor Tests' {
        It 'Should create logger with default constructor' {
            $logger = [CompilerLogger]::new()
            
            $logger | Should -Not -BeNullOrEmpty
            $logger.LogLevel | Should -Be 'INFO'
            $logger.EnableConsole | Should -Be $true
            $logger.EnableFile | Should -Be $false
            $logger.LogFile | Should -BeNullOrEmpty
        }

        It 'Should create logger with log level' {
            $logger = [CompilerLogger]::new('DEBUG')
            
            $logger.LogLevel | Should -Be 'DEBUG'
            $logger.EnableConsole | Should -Be $true
            $logger.EnableFile | Should -Be $false
        }

        It 'Should create logger with log level and file' {
            $tempFile = [System.IO.Path]::GetTempFileName()
            try {
                $logger = [CompilerLogger]::new('WARN', $tempFile)
                
                $logger.LogLevel | Should -Be 'WARN'
                $logger.LogFile | Should -Be $tempFile
                $logger.EnableConsole | Should -Be $true
                $logger.EnableFile | Should -Be $true
            }
            finally {
                Remove-Item $tempFile -ErrorAction SilentlyContinue
            }
        }

        It 'Should normalize log level to uppercase' {
            $logger = [CompilerLogger]::new('debug')
            
            $logger.LogLevel | Should -Be 'DEBUG'
        }
    }

    Context 'Static Factory Methods' {
        It 'Should create logger from environment variables' {
            $env:ADGUARD_COMPILER_LOG_LEVEL = 'ERROR'
            try {
                $logger = [CompilerLogger]::FromEnvironment()
                
                $logger.LogLevel | Should -Be 'ERROR'
            }
            finally {
                Remove-Item Env:\ADGUARD_COMPILER_LOG_LEVEL -ErrorAction SilentlyContinue
            }
        }

        It 'Should use default INFO level when no environment variable' {
            Remove-Item Env:\ADGUARD_COMPILER_LOG_LEVEL -ErrorAction SilentlyContinue
            
            $logger = [CompilerLogger]::FromEnvironment()
            
            $logger.LogLevel | Should -Be 'INFO'
        }

        It 'Should enable file logging from environment variable' {
            $tempFile = [System.IO.Path]::GetTempFileName()
            $env:ADGUARD_COMPILER_LOG_FILE = $tempFile
            try {
                $logger = [CompilerLogger]::FromEnvironment()
                
                $logger.LogFile | Should -Be $tempFile
                $logger.EnableFile | Should -Be $true
            }
            finally {
                Remove-Item Env:\ADGUARD_COMPILER_LOG_FILE -ErrorAction SilentlyContinue
                Remove-Item $tempFile -ErrorAction SilentlyContinue
            }
        }

        It 'Should detect DEBUG from DEBUG environment variable' {
            $env:DEBUG = '1'
            try {
                $result = [CompilerLogger]::IsDebugEnabled()
                
                $result | Should -Be $true
            }
            finally {
                Remove-Item Env:\DEBUG -ErrorAction SilentlyContinue
            }
        }
    }

    Context 'Logging Methods' {
        It 'Should log Info messages' {
            $logger = [CompilerLogger]::new('INFO')
            
            { $logger.Info('Test info message') } | Should -Not -Throw
        }

        It 'Should log Warn messages' {
            $logger = [CompilerLogger]::new('INFO')
            
            { $logger.Warn('Test warning message') } | Should -Not -Throw
        }

        It 'Should log Error messages' {
            $logger = [CompilerLogger]::new('INFO')
            
            { $logger.Error('Test error message') } | Should -Not -Throw
        }

        It 'Should log Debug messages' {
            $logger = [CompilerLogger]::new('DEBUG')
            
            { $logger.Debug('Test debug message') } | Should -Not -Throw
        }

        It 'Should respect log level filtering' {
            $logger = [CompilerLogger]::new('ERROR')
            $logger.SetConsoleLogging($false)
            
            # These should not throw even though they won't output
            { $logger.Info('Should be filtered') } | Should -Not -Throw
            { $logger.Debug('Should be filtered') } | Should -Not -Throw
        }

        It 'Should log to file when enabled' {
            $tempFile = [System.IO.Path]::GetTempFileName()
            try {
                $logger = [CompilerLogger]::new('INFO', $tempFile)
                $logger.Info('Test file logging')
                
                Start-Sleep -Milliseconds 100  # Allow time for file write
                
                $tempFile | Should -Exist
                $content = Get-Content $tempFile -Raw
                $content | Should -Match 'Test file logging'
            }
            finally {
                Remove-Item $tempFile -ErrorAction SilentlyContinue
            }
        }
    }

    Context 'Configuration Methods' {
        It 'Should set log level' {
            $logger = [CompilerLogger]::new('INFO')
            
            $logger.SetLogLevel('ERROR')
            
            $logger.LogLevel | Should -Be 'ERROR'
        }

        It 'Should throw on invalid log level' {
            $logger = [CompilerLogger]::new('INFO')
            
            { $logger.SetLogLevel('INVALID') } | Should -Throw
        }

        It 'Should enable/disable console logging' {
            $logger = [CompilerLogger]::new()
            
            $logger.SetConsoleLogging($false)
            $logger.EnableConsole | Should -Be $false
            
            $logger.SetConsoleLogging($true)
            $logger.EnableConsole | Should -Be $true
        }

        It 'Should enable/disable file logging' {
            $logger = [CompilerLogger]::new()
            
            $logger.SetFileLogging($false)
            $logger.EnableFile | Should -Be $false
            
            $logger.SetFileLogging($true)
            $logger.EnableFile | Should -Be $true
        }

        It 'Should set log file path' {
            $logger = [CompilerLogger]::new()
            $tempFile = [System.IO.Path]::GetTempFileName()
            
            $logger.SetLogFile($tempFile)
            
            $logger.LogFile | Should -Be $tempFile
            $logger.EnableFile | Should -Be $true
            
            Remove-Item $tempFile -ErrorAction SilentlyContinue
        }
    }

    Context 'Serialization Methods' {
        It 'Should convert to hashtable' {
            $logger = [CompilerLogger]::new('INFO')
            
            $hashtable = $logger.ToHashtable()
            
            $hashtable | Should -BeOfType [hashtable]
            $hashtable.LogLevel | Should -Be 'INFO'
            $hashtable.EnableConsole | Should -Be $true
            $hashtable.EnableFile | Should -Be $false
        }

        It 'Should convert to string' {
            $logger = [CompilerLogger]::new('DEBUG')
            
            $string = $logger.ToString()
            
            $string | Should -Match 'CompilerLogger'
            $string | Should -Match 'DEBUG'
            $string | Should -Match 'Console'
        }
    }

    Context 'File Logging Edge Cases' {
        It 'Should create directory if it does not exist' {
            $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString())
            $tempFile = Join-Path $tempDir 'test.log'
            
            try {
                $logger = [CompilerLogger]::new('INFO', $tempFile)
                $logger.Info('Test directory creation')
                
                Start-Sleep -Milliseconds 100
                
                $tempDir | Should -Exist
                $tempFile | Should -Exist
            }
            finally {
                Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }

        It 'Should handle file write errors gracefully' {
            $logger = [CompilerLogger]::new('INFO', 'Z:\nonexistent\path\file.log')
            $logger.SetConsoleLogging($false)
            
            # Should not throw even if file write fails
            { $logger.Info('This should not crash') } | Should -Not -Throw
        }
    }

    Context 'Log Level Hierarchy' {
        It 'Should filter DEBUG when level is INFO' {
            $tempFile = [System.IO.Path]::GetTempFileName()
            try {
                $logger = [CompilerLogger]::new('INFO', $tempFile)
                $logger.SetConsoleLogging($false)
                
                $logger.Debug('Debug message')
                $logger.Info('Info message')
                
                Start-Sleep -Milliseconds 100
                
                $content = Get-Content $tempFile -Raw
                $content | Should -Not -Match 'Debug message'
                $content | Should -Match 'Info message'
            }
            finally {
                Remove-Item $tempFile -ErrorAction SilentlyContinue
            }
        }

        It 'Should log all levels when set to DEBUG' {
            $tempFile = [System.IO.Path]::GetTempFileName()
            try {
                $logger = [CompilerLogger]::new('DEBUG', $tempFile)
                $logger.SetConsoleLogging($false)
                
                $logger.Debug('Debug message')
                $logger.Info('Info message')
                $logger.Warn('Warn message')
                $logger.Error('Error message')
                
                Start-Sleep -Milliseconds 100
                
                $content = Get-Content $tempFile -Raw
                $content | Should -Match 'Debug message'
                $content | Should -Match 'Info message'
                $content | Should -Match 'Warn message'
                $content | Should -Match 'Error message'
            }
            finally {
                Remove-Item $tempFile -ErrorAction SilentlyContinue
            }
        }

        It 'Should only log ERROR when level is ERROR' {
            $tempFile = [System.IO.Path]::GetTempFileName()
            try {
                $logger = [CompilerLogger]::new('ERROR', $tempFile)
                $logger.SetConsoleLogging($false)
                
                $logger.Debug('Debug message')
                $logger.Info('Info message')
                $logger.Warn('Warn message')
                $logger.Error('Error message')
                
                Start-Sleep -Milliseconds 100
                
                $content = Get-Content $tempFile -Raw
                $content | Should -Not -Match 'Debug message'
                $content | Should -Not -Match 'Info message'
                $content | Should -Not -Match 'Warn message'
                $content | Should -Match 'Error message'
            }
            finally {
                Remove-Item $tempFile -ErrorAction SilentlyContinue
            }
        }
    }
}
