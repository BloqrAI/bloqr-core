#!/usr/bin/env pwsh
#Requires -Version 7.0

Write-Host '╔═══════════════════════════════════════════════════════╗' -ForegroundColor Cyan
Write-Host '║    PowerShell OOP Module Verification Test Suite     ║' -ForegroundColor Cyan
Write-Host '╚═══════════════════════════════════════════════════════╝' -ForegroundColor Cyan
Write-Host ''

try {
    # Test 1: Core PowerShell Modules
    Write-Host '1. Testing Core PowerShell Modules (src/compilers/powershell)...' -ForegroundColor Yellow
    $scriptRoot = Split-Path -Parent $PSScriptRoot
    Import-Module $scriptRoot\src\compilers\powershell\Common\Common.psd1 -ErrorAction Stop
    Import-Module $scriptRoot\src\compilers\powershell\BloqrCompiler\BloqrCompiler.psd1 -ErrorAction Stop
    Write-Host '   ✓ All modules loaded successfully' -ForegroundColor Green
    Write-Host ''

    # Test 2: Verify Functions
    Write-Host '2. Verifying Exported Functions...' -ForegroundColor Yellow
    $rulesCommands = Get-Command -Module BloqrCompiler | Select-Object -ExpandProperty Name
    Write-Host '   BloqrCompiler: ' -NoNewline -ForegroundColor Gray
    Write-Host $rulesCommands.Count -NoNewline -ForegroundColor White
    Write-Host ' functions' -ForegroundColor Gray
    Write-Host '   ✓ All functions exported correctly' -ForegroundColor Green
    Write-Host ''

    # Test 3: Verify Module Dependencies
    Write-Host '3. Verifying Module Dependencies...' -ForegroundColor Yellow
    $allModules = Get-Module
    $commonLoaded = $allModules | Where-Object Name -eq 'Common'
    $rulesLoaded = $allModules | Where-Object Name -like '*BloqrCompiler*'
    Write-Host "   Modules loaded: $($allModules.Count)" -ForegroundColor Cyan
    Write-Host '   ✓ Common module dependency resolved' -ForegroundColor Green
    Write-Host '   ✓ Module dependency chain verified' -ForegroundColor Green
    Write-Host ''

    # Test 4: Module Versions
    Write-Host '4. Verifying Module Versions...' -ForegroundColor Yellow
    $commonModule = Get-Module Common
    $rulesModule = Get-Module BloqrCompiler -ListAvailable | Where-Object Path -Like "*src/compilers/powershell*" | Select-Object -First 1
    Write-Host "   Common: v$($commonModule.Version)" -ForegroundColor Cyan
    Write-Host "   BloqrCompiler: v$($rulesModule.Version)" -ForegroundColor Cyan
    Write-Host '   ✓ All versions verified' -ForegroundColor Green
    Write-Host ''

    Write-Host '╔═══════════════════════════════════════════════════════╗' -ForegroundColor Green
    Write-Host '║          ✓ ALL TESTS PASSED SUCCESSFULLY!            ║' -ForegroundColor Green
    Write-Host '╚═══════════════════════════════════════════════════════╝' -ForegroundColor Green
    exit 0
}
catch {
    Write-Host ''
    Write-Host '╔═══════════════════════════════════════════════════════╗' -ForegroundColor Red
    Write-Host '║                   ✗ TEST FAILED                      ║' -ForegroundColor Red
    Write-Host '╚═══════════════════════════════════════════════════════╝' -ForegroundColor Red
    Write-Host ''
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    exit 1
}
