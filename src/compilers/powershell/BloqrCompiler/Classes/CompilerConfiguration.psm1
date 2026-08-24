using namespace System.Collections.Generic

<#
.SYNOPSIS
    Represents a compiler configuration for AdGuard filter rules.

.DESCRIPTION
    This class encapsulates all configuration data needed for compiling
    AdGuard filter rules, including sources, transformations, and metadata.
    Supports loading from JSON, YAML, and TOML formats.

.NOTES
    Author: Jayson Knight
    Version: 1.0.0
#>

class CompilerConfiguration {
    # Properties
    [string]$Name
    [string]$Version
    [string]$Description
    [string]$Homepage
    [string]$License
    [PSCustomObject[]]$Sources
    # Default compilation engine/grammar for sources that don't set their own
    # 'engine' explicitly and whose content can't be confidently auto-detected.
    # $null (the default) leaves engine resolution to @bloqr/compiler-core itself,
    # which falls back to 'dns' - existing configurations are unaffected. Wire key:
    # camelCase 'defaultEngine', matching the shared JSON Schema
    # (schemas/compiler-config.schema.json) and the Rust/.NET/Python/TypeScript
    # wrappers' defaultEngine/DefaultEngine fields.
    [string]$DefaultEngine
    [string[]]$Transformations
    [string[]]$Inclusions
    [string[]]$Exclusions
    [string]$ConfigPath
    [string]$Format
    
    # Constructor - Default
    CompilerConfiguration() {
        $this.Sources = @()
        $this.Transformations = @()
        $this.Inclusions = @()
        $this.Exclusions = @()
    }
    
    # Constructor - From file path
    CompilerConfiguration([string]$configPath) {
        $this.Sources = @()
        $this.Transformations = @()
        $this.Inclusions = @()
        $this.Exclusions = @()
        $this.LoadFromFile($configPath)
    }
    
    # Constructor - From environment variables
    static [CompilerConfiguration] FromEnvironment() {
        $config = [CompilerConfiguration]::new()
        $config.LoadFromEnvironment()
        return $config
    }
    
    # Load configuration from file
    [void]LoadFromFile([string]$path) {
        if (-not (Test-Path $path)) {
            throw "Configuration file not found: $path"
        }
        
        $this.ConfigPath = [System.IO.Path]::GetFullPath($path)
        $this.Format = $this.DetectFormat($path)
        
        $content = Get-Content -Path $path -Raw -Encoding UTF8
        
        $data = switch ($this.Format) {
            'json' {
                $content | ConvertFrom-Json
            }
            'yaml' {
                if (Get-Module -ListAvailable -Name 'powershell-yaml') {
                    Import-Module powershell-yaml -ErrorAction SilentlyContinue
                    $content | ConvertFrom-Yaml
                }
                else {
                    throw "YAML format requires powershell-yaml module. Install with: Install-Module powershell-yaml"
                }
            }
            'toml' {
                # Basic TOML parser would go here
                throw "TOML format not yet implemented in class-based version"
            }
            default {
                throw "Unsupported format: $($this.Format)"
            }
        }
        
        # Populate properties
        $this.Name = $data.name
        $this.Version = $data.version
        $this.Description = $data.description
        $this.Homepage = $data.homepage
        $this.License = $data.license
        $this.Sources = $data.sources
        $this.DefaultEngine = $data.defaultEngine
        $this.Transformations = $data.transformations
        $this.Inclusions = $data.inclusions
        $this.Exclusions = $data.exclusions
    }
    
    # Load configuration from environment variables
    [void]LoadFromEnvironment() {
        if ($env:ADGUARD_COMPILER_CONFIG) {
            $this.LoadFromFile($env:ADGUARD_COMPILER_CONFIG)
        }
        
        # Override with specific environment variables
        if ($env:ADGUARD_COMPILER_OUTPUT) {
            # This would be handled by the caller
        }
        
        if ($env:ADGUARD_COMPILER_FORMAT) {
            $this.Format = $env:ADGUARD_COMPILER_FORMAT
        }
    }
    
    # Detect format from file extension
    hidden [string]DetectFormat([string]$path) {
        $extension = [System.IO.Path]::GetExtension($path).ToLower()
        
        $detectedFormat = switch ($extension) {
            '.json' { 'json' }
            '.yaml' { 'yaml' }
            '.yml'  { 'yaml' }
            '.toml' { 'toml' }
            default { throw "Unknown configuration file extension: $extension" }
        }
        
        return $detectedFormat
    }
    
    # Validate configuration
    [void]Validate() {
        $errors = [List[string]]::new()
        
        if ([string]::IsNullOrWhiteSpace($this.Name)) {
            $errors.Add("Configuration must have a name")
        }
        
        if ($null -eq $this.Sources -or $this.Sources.Count -eq 0) {
            $errors.Add("Configuration must have at least one source")
        }
        
        $validEngines = @('dns', 'browser')

        if (-not [string]::IsNullOrWhiteSpace($this.DefaultEngine) -and $this.DefaultEngine -notin $validEngines) {
            $errors.Add("defaultEngine must be one of: $($validEngines -join ', ') (got '$($this.DefaultEngine)')")
        }

        # Validate each source
        foreach ($source in $this.Sources) {
            if ([string]::IsNullOrWhiteSpace($source.source)) {
                $errors.Add("Each source must have a 'source' property")
            }

            if ($source.PSObject.Properties.Match('engine').Count -gt 0 -and
                -not [string]::IsNullOrWhiteSpace($source.engine) -and
                $source.engine -notin $validEngines) {
                $errors.Add("Source '$($source.name)': engine must be one of: $($validEngines -join ', ') (got '$($source.engine)')")
            }
        }

        if ($errors.Count -gt 0) {
            throw "Configuration validation failed:`n" + ($errors -join "`n")
        }
    }
    
    # Convert to hashtable
    [hashtable]ToHashtable() {
        return @{
            Name            = $this.Name
            Version         = $this.Version
            Description     = $this.Description
            Homepage        = $this.Homepage
            License         = $this.License
            Sources         = $this.Sources
            DefaultEngine   = $this.DefaultEngine
            Transformations = $this.Transformations
            Inclusions      = $this.Inclusions
            Exclusions      = $this.Exclusions
            ConfigPath      = $this.ConfigPath
            Format          = $this.Format
        }
    }
    
    # Convert to JSON string
    [string]ToJson() {
        return $this.ToHashtable() | ConvertTo-Json -Depth 10
    }
    
    # String representation
    [string]ToString() {
        return "$($this.Name) v$($this.Version) ($($this.Sources.Count) sources)"
    }
}

# Export the class
Export-ModuleMember -Variable CompilerConfiguration
Export-ModuleMember -Function @()
