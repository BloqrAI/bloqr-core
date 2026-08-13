#Requires -Version 7.0

<#
.SYNOPSIS
    PowerShell class for representing rules-validator syntax-validation results.

.DESCRIPTION
    The RulesValidatorResult class encapsulates the parsed `--json file` output of the
    `rules-validate` CLI (from src/rules-validator/), mirroring the same fields returned
    by the Rust/Python/TypeScript wrappers' rules-validator wiring for #361.

.NOTES
    Author: Jayson Knight
    GitHub: jaypatrick
#>

class RulesValidatorResult {
    # Properties
    [bool]$IsValid
    [string]$Format
    [int]$ValidRules
    [int]$InvalidRules
    [string[]]$Messages
    [datetime]$Timestamp

    # Default Constructor
    RulesValidatorResult() {
        $this.IsValid = $false
        $this.Format = 'unknown'
        $this.ValidRules = 0
        $this.InvalidRules = 0
        $this.Messages = @()
        $this.Timestamp = Get-Date
    }

    # Convert to hashtable
    [hashtable] ToHashtable() {
        return @{
            IsValid      = $this.IsValid
            Format       = $this.Format
            ValidRules   = $this.ValidRules
            InvalidRules = $this.InvalidRules
            Messages     = $this.Messages
            Timestamp    = $this.Timestamp
        }
    }

    # Convert to JSON
    [string] ToJson() {
        return $this.ToHashtable() | ConvertTo-Json -Depth 10
    }

    # String representation
    [string] ToString() {
        if ($this.IsValid) {
            return "RulesValidatorResult: Valid - $($this.ValidRules) valid rules, $($this.InvalidRules) invalid"
        }
        else {
            return "RulesValidatorResult: Invalid - $($this.ValidRules) valid rules, $($this.InvalidRules) invalid"
        }
    }

    # Build a result from the CLI's parsed `--json file` output
    static [RulesValidatorResult] FromCliOutput([PSCustomObject]$parsed) {
        $result = [RulesValidatorResult]::new()
        $result.IsValid = [bool]$parsed.is_valid
        $result.Format = $parsed.format
        $result.ValidRules = [int]$parsed.valid_rules
        $result.InvalidRules = [int]$parsed.invalid_rules
        $result.Messages = @($parsed.messages)
        return $result
    }
}

# Export the class
Export-ModuleMember -Variable RulesValidatorResult
