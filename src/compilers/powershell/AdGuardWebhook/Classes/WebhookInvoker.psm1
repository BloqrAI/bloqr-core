#Requires -Version 7.0

using module .\WebhookConfiguration.psm1
using module .\WebhookStatistics.psm1
using module ..\..\Common\Classes\CompilerLogger.psm1

<#
.SYNOPSIS
    PowerShell class for invoking webhooks with retry logic.

.DESCRIPTION
    The WebhookInvoker class encapsulates the logic for invoking webhook endpoints
    with configurable retry behavior, statistics tracking, and logging.

.NOTES
    Author: Jayson Knight
    GitHub: jaypatrick
#>

class WebhookInvoker {
    # Properties
    [WebhookConfiguration]$Config
    [WebhookStatistics]$Statistics
    [CompilerLogger]$Logger

    # Default Constructor
    WebhookInvoker([WebhookConfiguration]$config) {
        $this.Config = $config
        $this.Statistics = [WebhookStatistics]::new()
        
        # Create logger based on quiet mode
        if ($config.Quiet) {
            $this.Logger = [CompilerLogger]::new('ERROR')
            $this.Logger.SetConsoleLogging($false)
        }
        else {
            $logLevel = if ([CompilerLogger]::IsDebugEnabled()) { 'DEBUG' } else { 'INFO' }
            $this.Logger = [CompilerLogger]::new($logLevel)
        }
    }

    # Constructor with logger
    WebhookInvoker([WebhookConfiguration]$config, [CompilerLogger]$logger) {
        $this.Config = $config
        $this.Statistics = [WebhookStatistics]::new()
        $this.Logger = $logger
    }

    # Invoke webhook once
    [object] Invoke() {
        $this.Logger.Info("Invoking webhook: $($this.Config.WebhookUrl)")
        
        try {
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            
            $response = Invoke-WebRequest `
                -Uri $this.Config.WebhookUrl `
                -Method Get `
                -UseBasicParsing `
                -TimeoutSec 30 `
                -ErrorAction Stop
            
            $stopwatch.Stop()
            
            $this.Statistics.RecordSuccess($stopwatch.ElapsedMilliseconds)
            $this.Logger.Info("Webhook invoked successfully in $($stopwatch.ElapsedMilliseconds)ms")
            
            return $response
        }
        catch {
            $this.Statistics.RecordFailure()
            $this.Logger.Error("Webhook invocation failed: $_")
            throw
        }
    }

    # Invoke with retry logic
    [bool] InvokeWithRetry() {
        $attempt = 0
        $maxAttempts = $this.Config.RetryCount + 1  # Initial attempt + retries
        
        while ($attempt -lt $maxAttempts) {
            $attempt++
            
            try {
                $this.Logger.Debug("Attempt $attempt of $maxAttempts")
                $this.Invoke()
                return $true
            }
            catch {
                if ($attempt -lt $maxAttempts) {
                    $this.Logger.Warn("Attempt $attempt failed, retrying in $($this.Config.RetryInterval) seconds...")
                    Start-Sleep -Seconds $this.Config.RetryInterval
                }
                else {
                    $this.Logger.Error("All $maxAttempts attempts failed")
                    return $false
                }
            }
        }
        
        return $false
    }

    # Invoke continuously until manually stopped
    [void] InvokeContinuous() {
        $this.Logger.Info("Starting continuous webhook invocation (Press Ctrl+C to stop)")
        
        $iteration = 0
        
        try {
            while ($true) {
                $iteration++
                $this.Logger.Info("Iteration #$iteration")
                
                try {
                    $this.InvokeWithRetry()
                }
                catch {
                    $this.Logger.Error("Iteration #$iteration failed: $_")
                }
                
                # Wait before next iteration
                if ($this.Config.WaitTime -gt 0) {
                    $this.Logger.Debug("Waiting $($this.Config.WaitTime)ms before next iteration")
                    Start-Sleep -Milliseconds $this.Config.WaitTime
                }
                
                # Show progress
                if ($iteration % 10 -eq 0 -and -not $this.Config.Quiet) {
                    $this.ShowProgress()
                }
            }
        }
        catch [System.Management.Automation.PipelineStoppedException] {
            $this.Logger.Info("Continuous invocation stopped by user")
        }
        finally {
            $this.ShowFinalStatistics()
        }
    }

    # Show progress
    hidden [void] ShowProgress() {
        $summary = $this.Statistics.GetDetailedSummary()
        $this.Logger.Info("Progress: $($summary.SuccessfulAttempts)/$($summary.TotalAttempts) successful ($($summary.FormattedSuccessRate))")
    }

    # Show final statistics
    [void] ShowFinalStatistics() {
        if (-not $this.Config.ShowStatistics -and -not $this.Config.Continuous) {
            return
        }

        $this.Logger.Info("Webhook invocation statistics:")
        
        $summary = $this.Statistics.GetDetailedSummary()
        
        switch ($this.Config.Format) {
            'Json' {
                $jsonOutput = $this.Statistics.ToJson()
                Write-Host $jsonOutput
            }
            'List' {
                Write-Host ""
                Write-Host "Webhook Statistics" -ForegroundColor Cyan
                Write-Host "==================" -ForegroundColor Cyan
                foreach ($key in $summary.Keys) {
                    $value = $summary[$key]
                    Write-Host "${key}: " -NoNewline -ForegroundColor Gray
                    Write-Host $value -ForegroundColor White
                }
            }
            'Table' {
                $this.Statistics.ToTableObject() | Format-Table -AutoSize
            }
        }
    }

    # Execute based on configuration
    [object] Execute() {
        # Validate configuration first
        try {
            $this.Config.Validate()
        }
        catch {
            $this.Logger.Error("Configuration validation failed: $_")
            throw
        }

        # Execute based on mode
        if ($this.Config.Continuous) {
            $this.InvokeContinuous()
            return $this.Statistics
        }
        else {
            $success = $this.InvokeWithRetry()
            $this.ShowFinalStatistics()
            
            return [PSCustomObject]@{
                Success    = $success
                Statistics = $this.Statistics
                Config     = $this.Config
            }
        }
    }

    # Reset statistics
    [void] ResetStatistics() {
        $this.Statistics.Reset()
        $this.Logger.Info("Statistics reset")
    }

    # Get current statistics
    [WebhookStatistics] GetStatistics() {
        return $this.Statistics
    }

    # Get configuration
    [WebhookConfiguration] GetConfiguration() {
        return $this.Config
    }

    # Update configuration
    [void] UpdateConfiguration([WebhookConfiguration]$newConfig) {
        $this.Config = $newConfig
        $this.Logger.Info("Configuration updated")
    }

    # Test webhook connectivity
    [bool] TestConnection() {
        $this.Logger.Info("Testing webhook connectivity...")
        
        try {
            $response = Invoke-WebRequest `
                -Uri $this.Config.WebhookUrl `
                -Method Head `
                -UseBasicParsing `
                -TimeoutSec 10 `
                -ErrorAction Stop
            
            $this.Logger.Info("Connection test successful (Status: $($response.StatusCode))")
            return $true
        }
        catch {
            $this.Logger.Error("Connection test failed: $_")
            return $false
        }
    }

    # Static factory method
    static [WebhookInvoker] Create([WebhookConfiguration]$config) {
        return [WebhookInvoker]::new($config)
    }

    # Static factory from environment
    static [WebhookInvoker] CreateFromEnvironment() {
        $envConfig = [WebhookConfiguration]::FromEnvironment()
        return [WebhookInvoker]::new($envConfig)
    }

    # String representation
    [string] ToString() {
        return "WebhookInvoker: URL=$($this.Config.WebhookUrl), Statistics=$($this.Statistics)"
    }
}

# Export the class
Export-ModuleMember -Variable WebhookInvoker
