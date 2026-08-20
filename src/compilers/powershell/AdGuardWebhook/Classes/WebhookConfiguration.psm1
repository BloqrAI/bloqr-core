#Requires -Version 7.0

<#
.SYNOPSIS
    PowerShell class for managing webhook configuration.

.DESCRIPTION
    The WebhookConfiguration class encapsulates webhook settings including URL,
    timing parameters, and behavior flags. Supports loading from files and
    environment variables.

.NOTES
    Author: Jayson Knight
    GitHub: jaypatrick
#>

class WebhookConfiguration {
    # Properties
    [uri]$WebhookUrl
    [int]$WaitTime
    [int]$RetryCount
    [int]$RetryInterval
    [bool]$Continuous
    [string]$Format
    [bool]$ShowStatistics
    [bool]$Quiet
    [bool]$ShowBanner

    # Default Constructor
    WebhookConfiguration() {
        $this.WebhookUrl = $null
        $this.WaitTime = 200
        $this.RetryCount = 10
        $this.RetryInterval = 5
        $this.Continuous = $false
        $this.Format = 'Table'
        $this.ShowStatistics = $false
        $this.Quiet = $false
        $this.ShowBanner = $true
    }

    # Constructor with URL
    WebhookConfiguration([uri]$url) {
        $this.WebhookUrl = $url
        $this.WaitTime = 200
        $this.RetryCount = 10
        $this.RetryInterval = 5
        $this.Continuous = $false
        $this.Format = 'Table'
        $this.ShowStatistics = $false
        $this.Quiet = $false
        $this.ShowBanner = $true
    }

    # Constructor with all parameters
    WebhookConfiguration(
        [uri]$url,
        [int]$waitTime,
        [int]$retryCount,
        [int]$retryInterval
    ) {
        $this.WebhookUrl = $url
        $this.WaitTime = $waitTime
        $this.RetryCount = $retryCount
        $this.RetryInterval = $retryInterval
        $this.Continuous = $false
        $this.Format = 'Table'
        $this.ShowStatistics = $false
        $this.Quiet = $false
        $this.ShowBanner = $true
    }

    # Load from file (JSON or YAML)
    [void] LoadFromFile([string]$path) {
        if (-not (Test-Path $path)) {
            throw "Configuration file not found: $path"
        }

        $extension = [System.IO.Path]::GetExtension($path).ToLower()
        $content = Get-Content -Path $path -Raw -Encoding UTF8

        $config = $null
        switch ($extension) {
            '.json' {
                $config = $content | ConvertFrom-Json
            }
            { $_ -in '.yaml', '.yml' } {
                if (Get-Module -ListAvailable -Name 'powershell-yaml') {
                    Import-Module powershell-yaml -ErrorAction SilentlyContinue
                    $config = $content | ConvertFrom-Yaml
                }
                else {
                    throw "YAML files require powershell-yaml module. Install with: Install-Module powershell-yaml"
                }
            }
            default {
                throw "Unsupported configuration file format: $extension"
            }
        }

        # Apply configuration
        if ($config.WebhookUrl) { $this.WebhookUrl = $config.WebhookUrl }
        if ($config.WaitTime) { $this.WaitTime = $config.WaitTime }
        if ($config.RetryCount) { $this.RetryCount = $config.RetryCount }
        if ($config.RetryInterval) { $this.RetryInterval = $config.RetryInterval }
        if ($null -ne $config.Continuous) { $this.Continuous = $config.Continuous }
        if ($config.Format) { $this.Format = $config.Format }
        if ($null -ne $config.ShowStatistics) { $this.ShowStatistics = $config.ShowStatistics }
        if ($null -ne $config.Quiet) { $this.Quiet = $config.Quiet }
        if ($null -ne $config.ShowBanner) { $this.ShowBanner = $config.ShowBanner }
    }

    # Load from environment variables
    [void] LoadFromEnvironment() {
        if ($env:ADGUARD_WEBHOOK_URL) {
            $this.WebhookUrl = $env:ADGUARD_WEBHOOK_URL
        }

        if ($env:ADGUARD_WEBHOOK_WAIT_TIME) {
            $this.WaitTime = [int]$env:ADGUARD_WEBHOOK_WAIT_TIME
        }

        if ($env:ADGUARD_WEBHOOK_RETRY_COUNT) {
            $this.RetryCount = [int]$env:ADGUARD_WEBHOOK_RETRY_COUNT
        }

        if ($env:ADGUARD_WEBHOOK_RETRY_INTERVAL) {
            $this.RetryInterval = [int]$env:ADGUARD_WEBHOOK_RETRY_INTERVAL
        }

        if ($env:ADGUARD_WEBHOOK_FORMAT) {
            $this.Format = $env:ADGUARD_WEBHOOK_FORMAT
        }
    }

    # Validate configuration
    [bool] Validate() {
        # Webhook URL is required
        if ($null -eq $this.WebhookUrl -or [string]::IsNullOrEmpty($this.WebhookUrl.ToString())) {
            throw "WebhookUrl is required"
        }

        # Validate URL scheme
        if ($this.WebhookUrl.Scheme -notin @('http', 'https')) {
            throw "WebhookUrl must use http or https scheme"
        }

        # Validate WaitTime
        if ($this.WaitTime -lt 200) {
            throw "WaitTime must be at least 200ms"
        }

        # Validate RetryCount
        if ($this.RetryCount -lt 0 -or $this.RetryCount -gt 100) {
            throw "RetryCount must be between 0 and 100"
        }

        # Validate RetryInterval
        if ($this.RetryInterval -lt 1 -or $this.RetryInterval -gt 60) {
            throw "RetryInterval must be between 1 and 60 seconds"
        }

        # Validate Format
        if ($this.Format -notin @('Table', 'List', 'Json')) {
            throw "Format must be one of: Table, List, Json"
        }

        return $true
    }

    # Convert to hashtable
    [hashtable] ToHashtable() {
        return @{
            WebhookUrl      = $this.WebhookUrl.ToString()
            WaitTime        = $this.WaitTime
            RetryCount      = $this.RetryCount
            RetryInterval   = $this.RetryInterval
            Continuous      = $this.Continuous
            Format          = $this.Format
            ShowStatistics  = $this.ShowStatistics
            Quiet           = $this.Quiet
            ShowBanner      = $this.ShowBanner
        }
    }

    # Save to file
    [void] SaveToFile([string]$path) {
        $directory = Split-Path -Path $path -Parent
        if ($directory -and -not (Test-Path $directory)) {
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
        }

        $this.ToHashtable() | ConvertTo-Json -Depth 10 | Set-Content -Path $path -Encoding UTF8
    }

    # Convert to JSON
    [string] ToJson() {
        return $this.ToHashtable() | ConvertTo-Json -Depth 10
    }

    # String representation
    [string] ToString() {
        return "WebhookConfiguration: URL=$($this.WebhookUrl), RetryCount=$($this.RetryCount), WaitTime=$($this.WaitTime)ms"
    }

    # Clone configuration
    [WebhookConfiguration] Clone() {
        $clone = [WebhookConfiguration]::new()
        $clone.WebhookUrl = $this.WebhookUrl
        $clone.WaitTime = $this.WaitTime
        $clone.RetryCount = $this.RetryCount
        $clone.RetryInterval = $this.RetryInterval
        $clone.Continuous = $this.Continuous
        $clone.Format = $this.Format
        $clone.ShowStatistics = $this.ShowStatistics
        $clone.Quiet = $this.Quiet
        $clone.ShowBanner = $this.ShowBanner
        return $clone
    }

    # Apply defaults for missing values
    [void] ApplyDefaults() {
        if ($this.WaitTime -eq 0) { $this.WaitTime = 200 }
        if ($this.RetryCount -eq 0) { $this.RetryCount = 10 }
        if ($this.RetryInterval -eq 0) { $this.RetryInterval = 5 }
        if ([string]::IsNullOrEmpty($this.Format)) { $this.Format = 'Table' }
    }

    # Merge with another configuration (other takes precedence)
    [void] MergeWith([WebhookConfiguration]$other) {
        if ($null -ne $other.WebhookUrl) { $this.WebhookUrl = $other.WebhookUrl }
        if ($other.WaitTime -ne 200) { $this.WaitTime = $other.WaitTime }
        if ($other.RetryCount -ne 10) { $this.RetryCount = $other.RetryCount }
        if ($other.RetryInterval -ne 5) { $this.RetryInterval = $other.RetryInterval }
        if ($other.Continuous) { $this.Continuous = $other.Continuous }
        if ($other.Format -ne 'Table') { $this.Format = $other.Format }
        if ($other.ShowStatistics) { $this.ShowStatistics = $other.ShowStatistics }
        if ($other.Quiet) { $this.Quiet = $other.Quiet }
        if (-not $other.ShowBanner) { $this.ShowBanner = $other.ShowBanner }
    }

    # Create from environment variables
    static [WebhookConfiguration] FromEnvironment() {
        $config = [WebhookConfiguration]::new()
        $config.LoadFromEnvironment()
        return $config
    }

    # Create from file
    static [WebhookConfiguration] FromFile([string]$path) {
        $config = [WebhookConfiguration]::new()
        $config.LoadFromFile($path)
        return $config
    }

    # Create from hashtable
    static [WebhookConfiguration] FromHashtable([hashtable]$hash) {
        $config = [WebhookConfiguration]::new()
        
        if ($hash.WebhookUrl) { $config.WebhookUrl = $hash.WebhookUrl }
        if ($hash.WaitTime) { $config.WaitTime = $hash.WaitTime }
        if ($hash.RetryCount) { $config.RetryCount = $hash.RetryCount }
        if ($hash.RetryInterval) { $config.RetryInterval = $hash.RetryInterval }
        if ($null -ne $hash.Continuous) { $config.Continuous = $hash.Continuous }
        if ($hash.Format) { $config.Format = $hash.Format }
        if ($null -ne $hash.ShowStatistics) { $config.ShowStatistics = $hash.ShowStatistics }
        if ($null -ne $hash.Quiet) { $config.Quiet = $hash.Quiet }
        if ($null -ne $hash.ShowBanner) { $config.ShowBanner = $hash.ShowBanner }
        
        return $config
    }
}

# Export the class
Export-ModuleMember -Variable WebhookConfiguration
