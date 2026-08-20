#Requires -Version 7.0

<#
.SYNOPSIS
    PowerShell class for logging compiler operations.

.DESCRIPTION
    The CompilerLogger class provides structured logging functionality for the
    AdGuard rules compiler, supporting console and file output with configurable
    log levels.

.NOTES
    Author: Jayson Knight
    GitHub: jaypatrick
#>

class CompilerLogger {
    # Properties
    [string]$LogLevel
    [string]$LogFile
    [bool]$EnableConsole
    [bool]$EnableFile
    [hashtable]$LogLevelOrder

    # Default Constructor
    CompilerLogger() {
        $this.LogLevel = 'INFO'
        $this.LogFile = $null
        $this.EnableConsole = $true
        $this.EnableFile = $false
        $this.InitializeLogLevels()
    }

    # Constructor with log level
    CompilerLogger([string]$logLevel) {
        $this.LogLevel = $logLevel.ToUpper()
        $this.LogFile = $null
        $this.EnableConsole = $true
        $this.EnableFile = $false
        $this.InitializeLogLevels()
    }

    # Constructor with log level and file
    CompilerLogger([string]$logLevel, [string]$logFile) {
        $this.LogLevel = $logLevel.ToUpper()
        $this.LogFile = $logFile
        $this.EnableConsole = $true
        $this.EnableFile = $true
        $this.InitializeLogLevels()
    }

    # Initialize log level ordering
    hidden [void] InitializeLogLevels() {
        $this.LogLevelOrder = @{
            'DEBUG' = 0
            'INFO'  = 1
            'WARN'  = 2
            'ERROR' = 3
        }
    }

    # Check if a log level should be logged
    hidden [bool] ShouldLog([string]$level) {
        $currentLevelValue = $this.LogLevelOrder[$this.LogLevel]
        $messageLevelValue = $this.LogLevelOrder[$level]
        
        if ($null -eq $currentLevelValue) { $currentLevelValue = 1 }  # Default to INFO
        if ($null -eq $messageLevelValue) { $messageLevelValue = 1 }
        
        return $messageLevelValue -ge $currentLevelValue
    }

    # Format log message
    hidden [string] FormatMessage([string]$level, [string]$message) {
        $timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
        return "[$level] $timestamp - $message"
    }

    # Write to console with color
    hidden [void] WriteConsole([string]$level, [string]$message) {
        if (-not $this.EnableConsole) { return }

        $formattedMessage = $this.FormatMessage($level, $message)
        
        switch ($level) {
            'INFO'  { Write-Host $formattedMessage -ForegroundColor Cyan }
            'WARN'  { Write-Host $formattedMessage -ForegroundColor Yellow }
            'ERROR' { Write-Host $formattedMessage -ForegroundColor Red }
            'DEBUG' { Write-Host $formattedMessage -ForegroundColor Gray }
            default { Write-Host $formattedMessage }
        }
    }

    # Write to file
    hidden [void] WriteFile([string]$level, [string]$message) {
        if (-not $this.EnableFile -or [string]::IsNullOrEmpty($this.LogFile)) { return }

        try {
            $formattedMessage = $this.FormatMessage($level, $message)
            
            # Ensure directory exists
            $directory = Split-Path -Path $this.LogFile -Parent
            if ($directory -and -not (Test-Path $directory)) {
                New-Item -ItemType Directory -Path $directory -Force | Out-Null
            }
            
            # Append to log file
            Add-Content -Path $this.LogFile -Value $formattedMessage -Encoding UTF8
        }
        catch {
            Write-Warning "Failed to write to log file: $_"
        }
    }

    # Main logging method
    [void] Log([string]$level, [string]$message) {
        $level = $level.ToUpper()
        
        if (-not $this.ShouldLog($level)) {
            return
        }

        $this.WriteConsole($level, $message)
        $this.WriteFile($level, $message)
    }

    # Convenience methods
    [void] Info([string]$message) {
        $this.Log('INFO', $message)
    }

    [void] Warn([string]$message) {
        $this.Log('WARN', $message)
    }

    [void] Error([string]$message) {
        $this.Log('ERROR', $message)
    }

    [void] Debug([string]$message) {
        $this.Log('DEBUG', $message)
    }

    # Log with formatted arguments
    [void] LogFormat([string]$level, [string]$format, [object[]]$args) {
        try {
            $message = $format -f $args
            $this.Log($level, $message)
        }
        catch {
            $this.Error("Failed to format log message: $_")
        }
    }

    # Set log level
    [void] SetLogLevel([string]$level) {
        $level = $level.ToUpper()
        if ($this.LogLevelOrder.ContainsKey($level)) {
            $this.LogLevel = $level
        }
        else {
            throw "Invalid log level: $level. Valid levels are: DEBUG, INFO, WARN, ERROR"
        }
    }

    # Enable/disable console logging
    [void] SetConsoleLogging([bool]$enable) {
        $this.EnableConsole = $enable
    }

    # Enable/disable file logging
    [void] SetFileLogging([bool]$enable) {
        $this.EnableFile = $enable
    }

    # Set log file path
    [void] SetLogFile([string]$path) {
        $this.LogFile = $path
        if (-not [string]::IsNullOrEmpty($path)) {
            $this.EnableFile = $true
        }
    }

    # Get configuration as hashtable
    [hashtable] ToHashtable() {
        return @{
            LogLevel      = $this.LogLevel
            LogFile       = $this.LogFile
            EnableConsole = $this.EnableConsole
            EnableFile    = $this.EnableFile
        }
    }

    # String representation
    [string] ToString() {
        $status = @()
        if ($this.EnableConsole) { $status += 'Console' }
        if ($this.EnableFile) { $status += "File($($this.LogFile))" }
        
        $statusStr = if ($status.Count -gt 0) { $status -join ', ' } else { 'Disabled' }
        return "CompilerLogger: Level=$($this.LogLevel), Output=[$statusStr]"
    }

    # Create logger from environment variables
    static [CompilerLogger] FromEnvironment() {
        $envLogLevel = if ($env:ADGUARD_COMPILER_LOG_LEVEL) { $env:ADGUARD_COMPILER_LOG_LEVEL } else { 'INFO' }
        $envLogFile = $env:ADGUARD_COMPILER_LOG_FILE
        
        if ($envLogFile) {
            return [CompilerLogger]::new($envLogLevel, $envLogFile)
        }
        else {
            return [CompilerLogger]::new($envLogLevel)
        }
    }

    # Check if DEBUG mode is enabled via environment
    static [bool] IsDebugEnabled() {
        return ($env:DEBUG -eq '1' -or $env:DEBUG -eq 'true' -or $env:ADGUARD_COMPILER_VERBOSE -eq '1' -or $env:ADGUARD_COMPILER_VERBOSE -eq 'true')
    }
}

# Export the class
Export-ModuleMember -Variable CompilerLogger
