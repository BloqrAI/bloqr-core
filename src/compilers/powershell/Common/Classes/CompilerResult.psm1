#Requires -Version 7.0

<#
.SYNOPSIS
    PowerShell class for representing compilation results.

.DESCRIPTION
    The CompilerResult class encapsulates the results of a filter rules compilation,
    including success status, rule count, file paths, hashes, and timing information.

.NOTES
    Author: Jayson Knight
    GitHub: jaypatrick
#>

class CompilerResult {
    # Properties
    [bool]$Success
    [int]$RuleCount
    [string]$OutputPath
    [string]$Hash
    [long]$ElapsedMs
    [string]$ErrorMessage
    [string]$CompilerOutput
    [string]$ConfigFormat
    [datetime]$Timestamp

    # Default Constructor
    CompilerResult() {
        $this.Success = $false
        $this.RuleCount = 0
        $this.OutputPath = $null
        $this.Hash = $null
        $this.ElapsedMs = 0
        $this.ErrorMessage = $null
        $this.CompilerOutput = $null
        $this.ConfigFormat = 'json'
        $this.Timestamp = Get-Date
    }

    # Constructor with basic parameters
    CompilerResult([bool]$success, [int]$ruleCount) {
        $this.Success = $success
        $this.RuleCount = $ruleCount
        $this.OutputPath = $null
        $this.Hash = $null
        $this.ElapsedMs = 0
        $this.ErrorMessage = $null
        $this.CompilerOutput = $null
        $this.ConfigFormat = 'json'
        $this.Timestamp = Get-Date
    }

    # Constructor with all parameters
    CompilerResult(
        [bool]$success,
        [int]$ruleCount,
        [string]$outputPath,
        [string]$hash,
        [long]$elapsedMs,
        [string]$configFormat
    ) {
        $this.Success = $success
        $this.RuleCount = $ruleCount
        $this.OutputPath = $outputPath
        $this.Hash = $hash
        $this.ElapsedMs = $elapsedMs
        $this.ErrorMessage = $null
        $this.CompilerOutput = $null
        $this.ConfigFormat = $configFormat
        $this.Timestamp = Get-Date
    }

    # Convert to hashtable (for backward compatibility)
    [hashtable] ToHashtable() {
        return @{
            Success        = $this.Success
            RuleCount      = $this.RuleCount
            OutputPath     = $this.OutputPath
            Hash           = $this.Hash
            ElapsedMs      = $this.ElapsedMs
            ErrorMessage   = $this.ErrorMessage
            CompilerOutput = $this.CompilerOutput
            ConfigFormat   = $this.ConfigFormat
            Timestamp      = $this.Timestamp
        }
    }

    # Convert to JSON
    [string] ToJson() {
        return $this.ToHashtable() | ConvertTo-Json -Depth 10
    }

    # String representation
    [string] ToString() {
        if ($this.Success) {
            return "CompilerResult: Success - $($this.RuleCount) rules compiled in $($this.ElapsedMs)ms"
        }
        else {
            return "CompilerResult: Failed - $($this.ErrorMessage)"
        }
    }

    # Check if result is valid
    [bool] IsValid() {
        if ($this.Success) {
            return (-not [string]::IsNullOrEmpty($this.OutputPath) -and $this.RuleCount -ge 0)
        }
        return $true  # Failed results are valid if they have an error message
    }

    # Get formatted duration
    [string] GetFormattedDuration() {
        $seconds = $this.ElapsedMs / 1000
        if ($seconds -lt 1) {
            return "$($this.ElapsedMs)ms"
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

    # Create a failed result
    static [CompilerResult] CreateFailure([string]$errorMessage) {
        $result = [CompilerResult]::new()
        $result.Success = $false
        $result.ErrorMessage = $errorMessage
        return $result
    }

    # Create a successful result
    static [CompilerResult] CreateSuccess([int]$ruleCount, [string]$outputPath, [string]$hash, [long]$elapsedMs) {
        $result = [CompilerResult]::new()
        $result.Success = $true
        $result.RuleCount = $ruleCount
        $result.OutputPath = $outputPath
        $result.Hash = $hash
        $result.ElapsedMs = $elapsedMs
        return $result
    }
}

# Export the class
Export-ModuleMember -Variable CompilerResult
