#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Ad-Blocking Repository Launcher - Interactive Menu System

.DESCRIPTION
    Feature-rich interactive CLI launcher for all tools and tasks in the repository.
    Provides an intuitive interface to build projects, compile filters, run tests, and more.

.EXAMPLE
    .\launcher.ps1
    Launch the interactive menu system

.NOTES
    Requires PowerShell 7.0 or later
#>

#Requires -Version 7.0

$ErrorActionPreference = 'Continue'
$Script:RootDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Function to show banner
function Show-Banner {
    Clear-Host
    Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║                                                                ║" -ForegroundColor Cyan
    Write-Host "║           " -NoNewline -ForegroundColor Cyan
    Write-Host "Ad-Blocking Repository Launcher" -NoNewline -ForegroundColor Magenta
    Write-Host "                  ║" -ForegroundColor Cyan
    Write-Host "║                                                                ║" -ForegroundColor Cyan
    Write-Host "║     " -NoNewline -ForegroundColor Cyan
    Write-Host "Multi-Language Toolkit for Ad-Blocking & DNS Management" -NoNewline -ForegroundColor Green
    Write-Host "   ║" -ForegroundColor Cyan
    Write-Host "║                                                                ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
}

# Function to show menu
function Show-Menu {
    param(
        [string]$Title,
        [string[]]$Options
    )
    
    Write-Host "═══ $Title ═══" -ForegroundColor Blue
    Write-Host ""
    
    for ($i = 0; $i -lt $Options.Count; $i++) {
        Write-Host "  " -NoNewline
        Write-Host "$($i + 1)." -NoNewline -ForegroundColor Green
        Write-Host " $($Options[$i])"
    }
    
    Write-Host ""
    $choice = Read-Host "Enter your choice [1-$($Options.Count)]"
    return $choice
}

# Function to pause
function Pause {
    Write-Host ""
    Read-Host "Press Enter to continue"
}

# Function to check tool availability
function Test-Tool {
    param([string]$Command)
    
    if (Get-Command $Command -ErrorAction SilentlyContinue) {
        return "✓"
    }
    return "✗"
}

# Function to run command with error handling
function Invoke-SafeCommand {
    param(
        [scriptblock]$Command,
        [string]$Description
    )
    
    Write-Host ""
    Write-Host "→ $Description" -ForegroundColor Cyan
    Write-Host ""
    
    try {
        & $Command
        Write-Host ""
        Write-Host "✓ Completed successfully" -ForegroundColor Green
    }
    catch {
        Write-Host ""
        Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Main Menu
function Show-MainMenu {
    while ($true) {
        Show-Banner
        
        $choice = Show-Menu -Title "Main Menu" -Options @(
            "🔨 Build Tools"
            "⚙️  Compile Filter Rules"
            "🔍 Validation & Testing"
            "📦 Project Management"
            "ℹ️  System Information"
            "🚪 Exit"
        )

        switch ($choice) {
            "1" { Show-BuildMenu }
            "2" { Show-RulesMenu }
            "3" { Show-ValidationMenu }
            "4" { Show-ProjectMenu }
            "5" { Show-SystemInfo }
            "6" { exit 0 }
            default { Write-Host "Invalid choice" -ForegroundColor Red; Start-Sleep -Seconds 1 }
        }
    }
}

# Build Tools Menu
function Show-BuildMenu {
    while ($true) {
        Show-Banner
        Write-Host "Build Tools" -ForegroundColor Magenta
        Write-Host ""
        
        $choice = Show-Menu -Title "Build Tools" -Options @(
            "Build All Projects (Debug)"
            "Build All Projects (Release)"
            "Build Rust Projects"
            "Build .NET Projects"
            "Build TypeScript Projects"
            "Build Python Projects"
            "Run Build Tests"
            "← Back to Main Menu"
        )
        
        switch ($choice) {
            "1" { Invoke-SafeCommand { & "$Script:RootDir\build.ps1" -All } "Building all projects (debug)"; Pause }
            "2" { Invoke-SafeCommand { & "$Script:RootDir\build.ps1" -All -Profile release } "Building all projects (release)"; Pause }
            "3" {
                $profile = Show-Menu -Title "Rust Build Profile" -Options @("Debug", "Release", "← Cancel")
                switch ($profile) {
                    "1" { Invoke-SafeCommand { & "$Script:RootDir\build.ps1" -Rust } "Building Rust (debug)"; Pause }
                    "2" { Invoke-SafeCommand { & "$Script:RootDir\build.ps1" -Rust -Profile release } "Building Rust (release)"; Pause }
                }
            }
            "4" {
                $profile = Show-Menu -Title ".NET Build Profile" -Options @("Debug", "Release", "← Cancel")
                switch ($profile) {
                    "1" { Invoke-SafeCommand { & "$Script:RootDir\build.ps1" -DotNet } "Building .NET (debug)"; Pause }
                    "2" { Invoke-SafeCommand { & "$Script:RootDir\build.ps1" -DotNet -Profile release } "Building .NET (release)"; Pause }
                }
            }
            "5" { Invoke-SafeCommand { & "$Script:RootDir\build.ps1" -TypeScript } "Building TypeScript"; Pause }
            "6" { Invoke-SafeCommand { & "$Script:RootDir\build.ps1" -Python } "Building Python"; Pause }
            "7" { Invoke-SafeCommand { & "$Script:RootDir\tools\test-build-scripts.ps1" } "Running build script tests"; Pause }
            "8" { return }
            default { Write-Host "Invalid choice" -ForegroundColor Red; Start-Sleep -Seconds 1 }
        }
    }
}

# Filter Rules Compilation Menu
function Show-RulesMenu {
    while ($true) {
        Show-Banner
        Write-Host "Filter Rules Compilation" -ForegroundColor Magenta
        Write-Host ""
        
        $choice = Show-Menu -Title "Rules Compiler" -Options @(
            "Compile with TypeScript (Deno)"
            "Compile with .NET"
            "Compile with Rust"
            "Compile with Python"
            "Run Compiler Tests"
            "← Back to Main Menu"
        )
        
        switch ($choice) {
            "1" {
                if (Get-Command deno -ErrorAction SilentlyContinue) {
                    Push-Location "$Script:RootDir\src\adblock-compiler-core"
                    try {
                        deno task compile
                    }
                    finally {
                        Pop-Location
                    }
                }
                else {
                    Write-Host "✗ Deno is not installed" -ForegroundColor Red
                }
                Pause
            }
            "2" {
                Invoke-SafeCommand {
                    Push-Location "$Script:RootDir\src\rules-compiler-dotnet"
                    try {
                        dotnet run --project src\RulesCompiler.Console
                    }
                    finally {
                        Pop-Location
                    }
                } "Compiling with .NET"
                Pause
            }
            "3" {
                Invoke-SafeCommand {
                    Push-Location "$Script:RootDir\src\rules-compiler-rust"
                    try {
                        cargo run --release
                    }
                    finally {
                        Pop-Location
                    }
                } "Compiling with Rust"
                Pause
            }
            "4" {
                if (Get-Command python3 -ErrorAction SilentlyContinue) {
                    Push-Location "$Script:RootDir\src\rules-compiler-python"
                    try {
                        python3 -m rules_compiler
                    }
                    finally {
                        Pop-Location
                    }
                }
                elseif (Get-Command python -ErrorAction SilentlyContinue) {
                    Push-Location "$Script:RootDir\src\rules-compiler-python"
                    try {
                        python -m rules_compiler
                    }
                    finally {
                        Pop-Location
                    }
                }
                else {
                    Write-Host "✗ Python is not installed" -ForegroundColor Red
                }
                Pause
            }
            "5" {
                $testChoice = Show-Menu -Title "Test Which Compiler?" -Options @("TypeScript", "Rust", ".NET", "Python", "← Cancel")
                switch ($testChoice) {
                    "1" {
                        Push-Location "$Script:RootDir\src\adblock-compiler-core"
                        try { deno task test } finally { Pop-Location }
                    }
                    "2" { cargo test -p rules-compiler }
                    "3" {
                        Push-Location "$Script:RootDir\src\rules-compiler-dotnet"
                        try { dotnet test RulesCompiler.slnx } finally { Pop-Location }
                    }
                    "4" {
                        Push-Location "$Script:RootDir\src\rules-compiler-python"
                        try {
                            if (Get-Command python3 -ErrorAction SilentlyContinue) {
                                python3 -m pytest
                            }
                            else {
                                python -m pytest
                            }
                        }
                        finally { Pop-Location }
                    }
                }
                Pause
            }
            "6" { return }
            default { Write-Host "Invalid choice" -ForegroundColor Red; Start-Sleep -Seconds 1 }
        }
    }
}

# AdGuard API Clients have moved to BloqrAI/bloqr-apiclients; this launcher
# no longer manages them.

# Validation & Testing Menu
function Show-ValidationMenu {
    while ($true) {
        Show-Banner
        Write-Host "Validation & Testing" -ForegroundColor Magenta
        Write-Host ""
        
        $choice = Show-Menu -Title "Validation & Testing" -Options @(
            "Run Validation Library Tests"
            "Run All Rust Tests"
            "Run All .NET Tests"
            "Run Build Script Tests"
            "Check Validation Compliance"
            "Run Clippy (Rust Linter)"
            "← Back to Main Menu"
        )
        
        switch ($choice) {
            "1" { Invoke-SafeCommand { cargo test -p rules-validator-core -p rules-validator-cli } "Running validation tests"; Pause }
            "2" { Invoke-SafeCommand { cargo test --workspace } "Running all Rust tests"; Pause }
            "3" {
                Write-Host "Testing .NET Rules Compiler..." -ForegroundColor Cyan
                Push-Location "$Script:RootDir\src\rules-compiler-dotnet"
                try {
                    dotnet test RulesCompiler.slnx
                }
                finally {
                    Pop-Location
                }
                Pause
            }
            "4" { Invoke-SafeCommand { & "$Script:RootDir\tools\test-build-scripts.ps1" } "Running build script tests"; Pause }
            "5" {
                if (Test-Path "$Script:RootDir\tools\check-validation-compliance.sh") {
                    bash "$Script:RootDir\tools\check-validation-compliance.sh"
                }
                else {
                    Write-Host "Compliance script not found" -ForegroundColor Red
                }
                Pause
            }
            "6" { Invoke-SafeCommand { cargo clippy --workspace --all-features -- -W clippy::all } "Running clippy"; Pause }
            "7" { return }
            default { Write-Host "Invalid choice" -ForegroundColor Red; Start-Sleep -Seconds 1 }
        }
    }
}

# Project Management Menu
function Show-ProjectMenu {
    while ($true) {
        Show-Banner
        Write-Host "Project Management" -ForegroundColor Magenta
        Write-Host ""
        
        $choice = Show-Menu -Title "Project Management" -Options @(
            "View Project Structure"
            "Clean Build Artifacts"
            "Update Dependencies (Rust)"
            "Update Dependencies (.NET)"
            "Run PowerShell Module Tests"
            "View Git Status"
            "← Back to Main Menu"
        )
        
        switch ($choice) {
            "1" {
                Write-Host "Project Structure:" -ForegroundColor Cyan
                Write-Host ""
                Get-ChildItem -Path "$Script:RootDir\src" -Directory | ForEach-Object {
                    Write-Host "  📁 $($_.Name)" -ForegroundColor Yellow
                }
                Pause
            }
            "2" {
                Write-Host "Cleaning build artifacts..." -ForegroundColor Yellow
                cargo clean
                Get-ChildItem -Path $Script:RootDir -Recurse -Directory -Filter "bin" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
                Get-ChildItem -Path $Script:RootDir -Recurse -Directory -Filter "obj" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
                Write-Host "✓ Clean complete" -ForegroundColor Green
                Pause
            }
            "3" { Invoke-SafeCommand { cargo update } "Updating Rust dependencies"; Pause }
            "4" {
                Write-Host "Updating .NET tools..." -ForegroundColor Cyan
                dotnet tool update --global dotnet-format
                Pause
            }
            "5" { Invoke-SafeCommand { & "$Script:RootDir\tools\test-modules.ps1" } "Running PowerShell module tests"; Pause }
            "6" {
                git status
                Write-Host ""
                git log --oneline -10
                Pause
            }
            "7" { return }
            default { Write-Host "Invalid choice" -ForegroundColor Red; Start-Sleep -Seconds 1 }
        }
    }
}

# System Information
function Show-SystemInfo {
    Show-Banner
    Write-Host "System Information" -ForegroundColor Magenta
    Write-Host ""
    
    Write-Host "Available Tools:" -ForegroundColor Cyan
    $rustVersion = if (Get-Command cargo -ErrorAction SilentlyContinue) { cargo --version } else { "Not installed" }
    $dotnetVersion = if (Get-Command dotnet -ErrorAction SilentlyContinue) { dotnet --version } else { "Not installed" }
    $denoVersion = if (Get-Command deno -ErrorAction SilentlyContinue) { (deno --version | Select-Object -First 1) } else { "Not installed" }
    $pythonVersion = if (Get-Command python3 -ErrorAction SilentlyContinue) { python3 --version } elseif (Get-Command python -ErrorAction SilentlyContinue) { python --version } else { "Not installed" }
    $pwshVersion = $PSVersionTable.PSVersion.ToString()
    $gitVersion = if (Get-Command git -ErrorAction SilentlyContinue) { git --version } else { "Not installed" }
    
    Write-Host "  Rust (cargo):      $(Test-Tool cargo)  $rustVersion"
    Write-Host "  .NET:              $(Test-Tool dotnet)  $dotnetVersion"
    Write-Host "  Deno:              $(Test-Tool deno)  $denoVersion"
    Write-Host "  Python:            $(Test-Tool python3)  $pythonVersion"
    Write-Host "  PowerShell:        ✓  $pwshVersion"
    Write-Host "  Git:               $(Test-Tool git)  $gitVersion"
    Write-Host ""
    
    Write-Host "Repository Information:" -ForegroundColor Cyan
    $branch = git branch --show-current 2>$null
    $lastCommit = git log -1 --pretty=format:'%h - %s' 2>$null
    
    Write-Host "  Branch:            $branch"
    Write-Host "  Last Commit:       $lastCommit"
    Write-Host "  Working Directory: $Script:RootDir"
    Write-Host ""
    
    Write-Host "Projects Available:" -ForegroundColor Cyan
    $rustProjects = (Get-ChildItem -Path "$Script:RootDir\src" -Recurse -Filter "Cargo.toml" | Measure-Object).Count
    $dotnetProjects = (Get-ChildItem -Path "$Script:RootDir\src" -Recurse -Filter "*.csproj" | Measure-Object).Count
    $tsProjects = (Get-ChildItem -Path "$Script:RootDir\src" -Recurse -Filter "deno.json" | Measure-Object).Count
    $pyProjects = (Get-ChildItem -Path "$Script:RootDir\src" -Recurse -Filter "pyproject.toml" | Measure-Object).Count
    
    Write-Host "  Rust Projects:     $rustProjects packages"
    Write-Host "  .NET Projects:     $dotnetProjects projects"
    Write-Host "  TypeScript:        $tsProjects projects"
    Write-Host "  Python:            $pyProjects projects"
    Write-Host ""
    
    Pause
}

# Start the launcher
Show-MainMenu
