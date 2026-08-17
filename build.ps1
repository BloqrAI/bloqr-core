<#
.SYNOPSIS
    Root-level build script for ad-blocking repository

.DESCRIPTION
    Builds all projects or specific ones with debug/release profiles.
    Default profile is debug.

.PARAMETER All
    Build all projects (default if no specific project selected)

.PARAMETER Rust
    Build Rust projects

.PARAMETER DotNet
    Build .NET projects

.PARAMETER TypeScript
    Build TypeScript/Deno projects

.PARAMETER Python
    Build Python projects

.PARAMETER Profile
    Build profile: 'debug' (default) or 'release'

.EXAMPLE
    .\build.ps1
    Build all projects in debug mode

.EXAMPLE
    .\build.ps1 -Rust
    Build only Rust projects in debug mode

.EXAMPLE
    .\build.ps1 -DotNet -Profile release
    Build only .NET projects in release mode

.EXAMPLE
    .\build.ps1 -All -Profile release
    Build all projects in release mode
#>

[CmdletBinding()]
param(
    [Parameter(HelpMessage = "Build all projects")]
    [switch]$All,
    
    [Parameter(HelpMessage = "Build Rust projects")]
    [switch]$Rust,
    
    [Parameter(HelpMessage = "Build .NET projects")]
    [switch]$DotNet,
    
    [Parameter(HelpMessage = "Build TypeScript/Deno projects")]
    [switch]$TypeScript,
    
    [Parameter(HelpMessage = "Build Python projects")]
    [switch]$Python,
    
    [Parameter(HelpMessage = "Build profile: 'debug' (default) or 'release'")]
    [ValidateSet('debug', 'release')]
    [string]$Profile = 'debug'
)

$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

# Use the profile parameter
$BuildProfile = $Profile

# If no specific project selected, build all
if (-not $All -and -not $Rust -and -not $DotNet -and -not $TypeScript -and -not $Python) {
    $All = $true
}

# If --all is specified, enable all projects
if ($All) {
    $Rust = $true
    $DotNet = $true
    $TypeScript = $true
    $Python = $true
}

Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   Ad-Blocking Repository Build Script                    ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "Build Profile: $BuildProfile" -ForegroundColor Blue
Write-Host ""

$BuildFailed = $false

# Function to build Rust projects
function Build-RustProjects {
    Write-Host "Building Rust projects..." -ForegroundColor Blue
    
    # Build the entire workspace
    Write-Host "→ Building Rust workspace..."
    try {
        if ($BuildProfile -eq "release") {
            cargo build --release --workspace
        } else {
            cargo build --workspace
        }
        Write-Host "✓ Rust workspace built successfully" -ForegroundColor Green
    }
    catch {
        Write-Host "✗ Rust workspace build failed" -ForegroundColor Red
        $script:BuildFailed = $true
    }
    
    Write-Host ""
}

# Function to build .NET projects
function Build-DotNetProjects {
    Write-Host "Building .NET projects..." -ForegroundColor Blue
    
    $configuration = if ($BuildProfile -eq "release") { "Release" } else { "Debug" }

    # Build Compiler Common (.NET) - shared library, standalone solution
    Write-Host "→ Building Compiler Common (.NET)..."
    try {
        Push-Location src/common/dotnet
        try {
            dotnet restore CompilerCommon.slnx
            dotnet build CompilerCommon.slnx --no-restore --configuration $configuration
            Write-Host "✓ Compiler Common (.NET) built successfully" -ForegroundColor Green
        }
        finally {
            Pop-Location
        }
    }
    catch {
        Write-Host "✗ Compiler Common (.NET) build failed" -ForegroundColor Red
        $script:BuildFailed = $true
    }

    # Build Compiler Dotnet
    Write-Host "→ Building Compiler Dotnet..."
    try {
        Push-Location src/compilers/dotnet
        try {
            dotnet restore CompilerDotnet.slnx
            dotnet build CompilerDotnet.slnx --no-restore --configuration $configuration
            Write-Host "✓ Compiler Dotnet built successfully" -ForegroundColor Green
        }
        finally {
            Pop-Location
        }
    }
    catch {
        Write-Host "✗ Compiler Dotnet build failed" -ForegroundColor Red
        $script:BuildFailed = $true
    }

    # Build Dashboard (.NET)
    Write-Host "→ Building Dashboard (.NET)..."
    try {
        Push-Location src/bloqr-dashboard
        try {
            dotnet restore BloqrDashboard.slnx
            dotnet build BloqrDashboard.slnx --no-restore --configuration $configuration
            Write-Host "✓ Dashboard (.NET) built successfully" -ForegroundColor Green
        }
        finally {
            Pop-Location
        }
    }
    catch {
        Write-Host "✗ Dashboard (.NET) build failed" -ForegroundColor Red
        $script:BuildFailed = $true
    }

    Write-Host ""
}

# Function to build TypeScript/Deno projects
function Build-TypeScriptProjects {
    Write-Host "Building TypeScript/Deno projects..." -ForegroundColor Blue
    
    # Check if Deno is installed
    if (-not (Get-Command deno -ErrorAction SilentlyContinue)) {
        Write-Host "✗ Deno is not installed. Please install Deno to build TypeScript projects." -ForegroundColor Red
        $script:BuildFailed = $true
        return
    }
    
    # Build Rules Compiler TypeScript
    Write-Host "→ Building Rules Compiler (TypeScript)..."
    try {
        Push-Location src/adblock-compiler-core
        try {
            deno task generate:types
            deno task check
            Write-Host "✓ Rules Compiler (TypeScript) built successfully" -ForegroundColor Green
        }
        finally {
            Pop-Location
        }
    }
    catch {
        Write-Host "✗ Rules Compiler (TypeScript) build failed" -ForegroundColor Red
        $script:BuildFailed = $true
    }
    
    Write-Host ""
}

# Function to build Python projects
function Build-PythonProjects {
    Write-Host "Building Python projects..." -ForegroundColor Blue
    
    # Check if Python is installed
    if (-not (Get-Command python -ErrorAction SilentlyContinue) -and -not (Get-Command python3 -ErrorAction SilentlyContinue)) {
        Write-Host "✗ Python 3 is not installed. Please install Python 3 to build Python projects." -ForegroundColor Red
        $script:BuildFailed = $true
        return
    }
    
    $pythonCmd = if (Get-Command python3 -ErrorAction SilentlyContinue) { "python3" } else { "python" }
    
    # Build Rules Compiler Python
    Write-Host "→ Building Rules Compiler (Python)..."
    try {
        Push-Location src/rules-compiler-python
        try {
            & $pythonCmd -m pip install --quiet -e ".[dev]"
            & $pythonCmd -m mypy rules_compiler/
            Write-Host "✓ Rules Compiler (Python) built successfully" -ForegroundColor Green
        }
        finally {
            Pop-Location
        }
    }
    catch {
        Write-Host "✗ Rules Compiler (Python) build failed" -ForegroundColor Red
        $script:BuildFailed = $true
    }
    
    Write-Host ""
}

# Build projects based on flags
if ($Rust) {
    Build-RustProjects
}

if ($DotNet) {
    Build-DotNetProjects
}

if ($TypeScript) {
    Build-TypeScriptProjects
}

if ($Python) {
    Build-PythonProjects
}

# Summary
Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   Build Summary                                           ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

if ($BuildFailed) {
    Write-Host "✗ Some builds failed. Please check the output above." -ForegroundColor Red
    exit 1
}
else {
    Write-Host "✓ All builds completed successfully!" -ForegroundColor Green
    exit 0
}
