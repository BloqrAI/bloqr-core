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

# Bundled copy of the canonical schema (schemas/compiler-config.schema.json at the repo root) -
# see issue #518 (follow-up to #502). Kept in sync by a drift-guard Pester test that diffs the
# two files. Resolved once at module-load time via $PSScriptRoot (available here because this is
# top-level module script code, not a class method body) and read lazily by
# Test-BloqrCompilerConfigSchema so a missing/unreadable file fails with a clear message instead
# of a null-schema Test-Json call.
$script:BloqrCompilerConfigSchemaPath = Join-Path $PSScriptRoot '..' 'schemas' 'compiler-config.schema.json'

# JSON Schema (draft-07) validation for parsed/constructed configuration data, chained ahead of
# CompilerConfiguration's own hand-written Validate() checks - matches the .NET, TypeScript,
# Python, Rust, and Swift compilers, which all validate against the same canonical schema.
# Uses PowerShell 7's built-in Test-Json cmdlet rather than an external module, so there's no new
# dependency to install.
#
# $data is whatever ConvertFrom-Json/ConvertFrom-Yaml/a hand-built hashtable produced - it is
# converted to a JSON string and validated as-is, so (unlike a typed Decodable/dataclass
# round-trip) every property actually present in the source document is checked, including ones
# this class doesn't model.
function Test-BloqrCompilerConfigSchema {
    param(
        [Parameter(Mandatory = $true)]
        $Data
    )

    if (-not (Test-Path -LiteralPath $script:BloqrCompilerConfigSchemaPath)) {
        throw "Bundled compiler-config.schema.json is missing at $script:BloqrCompilerConfigSchemaPath"
    }

    $schemaText = Get-Content -LiteralPath $script:BloqrCompilerConfigSchemaPath -Raw -Encoding UTF8
    $jsonText = $Data | ConvertTo-Json -Depth 20 -Compress

    $schemaErrors = $null
    $isValid = Test-Json -Json $jsonText -Schema $schemaText -ErrorVariable schemaErrors -ErrorAction SilentlyContinue

    if (-not $isValid) {
        $messages = @($schemaErrors | ForEach-Object { $_.Exception.Message })
        if ($messages.Count -eq 0) {
            $messages = @('configuration does not conform to the schema')
        }
        throw "Configuration schema validation failed:`n" + ($messages -join "`n")
    }
}

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
        
        # Schema validation runs on the raw parsed document, ahead of populating this class's
        # typed properties, so it catches every property actually present in the file -
        # including ones this class doesn't model (output, chunking, extensions, etc.) - not
        # just the subset ToSchemaHashtable() below re-derives from populated properties.
        Test-BloqrCompilerConfigSchema -Data $data

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
    
    # Transformations this OSS toolkit actually implements. Mirrors the
    # RemoveComments..ConvertToAscii set documented in CLAUDE.md and the
    # shared schemas/compiler-config.schema.json enum. Deliberately excludes
    # ConflictDetection/RuleOptimizer, which are commercial-only browser-engine
    # transformations not implemented anywhere in this repo (see issue #502).
    static [string[]] $ValidTransformations = @(
        'RemoveComments',
        'Compress',
        'RemoveModifiers',
        'Validate',
        'ValidateAllowIp',
        'Deduplicate',
        'InvertAllow',
        'RemoveEmptyLines',
        'TrimLines',
        'InsertFinalNewLine',
        'ConvertToAscii'
    )

    # Schema-shaped hashtable built from this instance's populated properties, for schema
    # validation of a directly-constructed CompilerConfiguration (one never loaded from a file,
    # so LoadFromFile's Test-BloqrCompilerConfigSchema call never ran). Deliberately narrower
    # than ToHashtable(): ConfigPath/Format aren't schema properties (the schema's root
    # additionalProperties is false), and null/empty optional fields are omitted so an unset
    # Version, say, doesn't fail the schema's "version" string pattern.
    [hashtable]ToSchemaHashtable() {
        $result = @{ name = $this.Name; sources = @($this.Sources) }
        if (-not [string]::IsNullOrWhiteSpace($this.Description)) { $result.description = $this.Description }
        if (-not [string]::IsNullOrWhiteSpace($this.Homepage)) { $result.homepage = $this.Homepage }
        if (-not [string]::IsNullOrWhiteSpace($this.License)) { $result.license = $this.License }
        if (-not [string]::IsNullOrWhiteSpace($this.Version)) { $result.version = $this.Version }
        if (-not [string]::IsNullOrWhiteSpace($this.DefaultEngine)) { $result.defaultEngine = $this.DefaultEngine }
        if ($this.Transformations -and $this.Transformations.Count -gt 0) { $result.transformations = @($this.Transformations) }
        if ($this.Inclusions -and $this.Inclusions.Count -gt 0) { $result.inclusions = @($this.Inclusions) }
        if ($this.Exclusions -and $this.Exclusions.Count -gt 0) { $result.exclusions = @($this.Exclusions) }
        return $result
    }

    # Validate configuration
    [void]Validate() {
        # Runs first so this covers every caller of Validate() - not just LoadFromFile's
        # file-based path, which schema-validates the raw document independently - including a
        # directly constructed CompilerConfiguration that never went through a file at all.
        Test-BloqrCompilerConfigSchema -Data $this.ToSchemaHashtable()

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

        foreach ($transformation in $this.Transformations) {
            if ($transformation -cnotin [CompilerConfiguration]::ValidTransformations) {
                $errors.Add("transformations: invalid transformation '$transformation'. Valid: $([CompilerConfiguration]::ValidTransformations -join ', ')")
            }
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

            if ($source.PSObject.Properties.Match('transformations').Count -gt 0) {
                foreach ($transformation in $source.transformations) {
                    if ($transformation -cnotin [CompilerConfiguration]::ValidTransformations) {
                        $errors.Add("Source '$($source.name)': invalid transformation '$transformation'. Valid: $([CompilerConfiguration]::ValidTransformations -join ', ')")
                    }
                }
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
