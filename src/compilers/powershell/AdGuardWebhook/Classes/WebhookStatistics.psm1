#Requires -Version 7.0

<#
.SYNOPSIS
    PowerShell class for tracking webhook invocation statistics.

.DESCRIPTION
    The WebhookStatistics class tracks and calculates statistics for webhook
    invocations, including success/failure rates, response times, and timing information.

.NOTES
    Author: Jayson Knight
    GitHub: jaypatrick
#>

class WebhookStatistics {
    # Properties
    [int]$TotalAttempts
    [int]$SuccessfulAttempts
    [int]$FailedAttempts
    [double]$SuccessRate
    [long]$TotalElapsedMs
    [long]$AverageResponseMs
    [long]$MinResponseMs
    [long]$MaxResponseMs
    [datetime]$StartTime
    [datetime]$EndTime
    [System.Collections.Generic.List[long]]$ResponseTimes

    # Default Constructor
    WebhookStatistics() {
        $this.TotalAttempts = 0
        $this.SuccessfulAttempts = 0
        $this.FailedAttempts = 0
        $this.SuccessRate = 0.0
        $this.TotalElapsedMs = 0
        $this.AverageResponseMs = 0
        $this.MinResponseMs = [long]::MaxValue
        $this.MaxResponseMs = 0
        $this.StartTime = Get-Date
        $this.EndTime = Get-Date
        $this.ResponseTimes = [System.Collections.Generic.List[long]]::new()
    }

    # Record a successful invocation
    [void] RecordSuccess([long]$responseTimeMs) {
        $this.TotalAttempts++
        $this.SuccessfulAttempts++
        $this.ResponseTimes.Add($responseTimeMs)
        
        if ($responseTimeMs -lt $this.MinResponseMs) {
            $this.MinResponseMs = $responseTimeMs
        }
        if ($responseTimeMs -gt $this.MaxResponseMs) {
            $this.MaxResponseMs = $responseTimeMs
        }
        
        $this.EndTime = Get-Date
        $this.Calculate()
    }

    # Record a failed invocation
    [void] RecordFailure() {
        $this.TotalAttempts++
        $this.FailedAttempts++
        $this.EndTime = Get-Date
        $this.Calculate()
    }

    # Calculate statistics
    [void] Calculate() {
        # Calculate success rate
        if ($this.TotalAttempts -gt 0) {
            $this.SuccessRate = ($this.SuccessfulAttempts / $this.TotalAttempts) * 100
        }
        else {
            $this.SuccessRate = 0.0
        }

        # Calculate average response time
        if ($this.ResponseTimes.Count -gt 0) {
            $sum = 0
            foreach ($time in $this.ResponseTimes) {
                $sum += $time
            }
            $this.AverageResponseMs = [long]($sum / $this.ResponseTimes.Count)
        }
        else {
            $this.AverageResponseMs = 0
        }

        # Calculate total elapsed time
        $this.TotalElapsedMs = ([long]($this.EndTime - $this.StartTime).TotalMilliseconds)
    }

    # Reset statistics
    [void] Reset() {
        $this.TotalAttempts = 0
        $this.SuccessfulAttempts = 0
        $this.FailedAttempts = 0
        $this.SuccessRate = 0.0
        $this.TotalElapsedMs = 0
        $this.AverageResponseMs = 0
        $this.MinResponseMs = [long]::MaxValue
        $this.MaxResponseMs = 0
        $this.StartTime = Get-Date
        $this.EndTime = Get-Date
        $this.ResponseTimes.Clear()
    }

    # Convert to hashtable
    [hashtable] ToHashtable() {
        return @{
            TotalAttempts       = $this.TotalAttempts
            SuccessfulAttempts  = $this.SuccessfulAttempts
            FailedAttempts      = $this.FailedAttempts
            SuccessRate         = [Math]::Round($this.SuccessRate, 2)
            TotalElapsedMs      = $this.TotalElapsedMs
            AverageResponseMs   = $this.AverageResponseMs
            MinResponseMs       = if ($this.MinResponseMs -eq [long]::MaxValue) { 0 } else { $this.MinResponseMs }
            MaxResponseMs       = $this.MaxResponseMs
            StartTime           = $this.StartTime
            EndTime             = $this.EndTime
            TotalElapsedSeconds = [Math]::Round($this.TotalElapsedMs / 1000, 2)
        }
    }

    # Convert to JSON
    [string] ToJson() {
        return $this.ToHashtable() | ConvertTo-Json -Depth 10
    }

    # String representation
    [string] ToString() {
        $duration = $this.GetFormattedDuration()
        return "WebhookStatistics: $($this.SuccessfulAttempts)/$($this.TotalAttempts) successful ({0:N2}%) in $duration" -f $this.SuccessRate
    }

    # Get formatted duration
    [string] GetFormattedDuration() {
        $seconds = $this.TotalElapsedMs / 1000
        if ($seconds -lt 1) {
            return "$($this.TotalElapsedMs)ms"
        }
        elseif ($seconds -lt 60) {
            return "{0:N2}s" -f $seconds
        }
        else {
            $minutes = [Math]::Floor($seconds / 60)
            $remainingSeconds = $seconds % 60
            return "${minutes}m {0:N0}s" -f $remainingSeconds
        }
    }

    # Get formatted success rate
    [string] GetFormattedSuccessRate() {
        return "{0:N2}%" -f $this.SuccessRate
    }

    # Get average response time in seconds
    [double] GetAverageResponseSeconds() {
        return [Math]::Round($this.AverageResponseMs / 1000, 3)
    }

    # Check if there were any failures
    [bool] HasFailures() {
        return $this.FailedAttempts -gt 0
    }

    # Check if all attempts were successful
    [bool] AllSuccessful() {
        return $this.TotalAttempts -gt 0 -and $this.FailedAttempts -eq 0
    }

    # Get detailed summary
    [hashtable] GetDetailedSummary() {
        $summary = $this.ToHashtable()
        $summary['FormattedDuration'] = $this.GetFormattedDuration()
        $summary['FormattedSuccessRate'] = $this.GetFormattedSuccessRate()
        $summary['AverageResponseSeconds'] = $this.GetAverageResponseSeconds()
        $summary['HasFailures'] = $this.HasFailures()
        $summary['AllSuccessful'] = $this.AllSuccessful()
        return $summary
    }

    # Format as table-friendly object
    [PSCustomObject] ToTableObject() {
        return [PSCustomObject]@{
            'Total Attempts'    = $this.TotalAttempts
            'Successful'        = $this.SuccessfulAttempts
            'Failed'            = $this.FailedAttempts
            'Success Rate'      = $this.GetFormattedSuccessRate()
            'Avg Response'      = "$($this.AverageResponseMs)ms"
            'Min Response'      = if ($this.MinResponseMs -eq [long]::MaxValue) { 'N/A' } else { "$($this.MinResponseMs)ms" }
            'Max Response'      = "$($this.MaxResponseMs)ms"
            'Total Duration'    = $this.GetFormattedDuration()
        }
    }

    # Merge with another WebhookStatistics instance
    [void] Merge([WebhookStatistics]$other) {
        $this.TotalAttempts += $other.TotalAttempts
        $this.SuccessfulAttempts += $other.SuccessfulAttempts
        $this.FailedAttempts += $other.FailedAttempts
        
        foreach ($responseTime in $other.ResponseTimes) {
            $this.ResponseTimes.Add($responseTime)
            if ($responseTime -lt $this.MinResponseMs) {
                $this.MinResponseMs = $responseTime
            }
            if ($responseTime -gt $this.MaxResponseMs) {
                $this.MaxResponseMs = $responseTime
            }
        }
        
        if ($other.StartTime -lt $this.StartTime) {
            $this.StartTime = $other.StartTime
        }
        if ($other.EndTime -gt $this.EndTime) {
            $this.EndTime = $other.EndTime
        }
        
        $this.Calculate()
    }
}

# Export the class
Export-ModuleMember -Variable WebhookStatistics
