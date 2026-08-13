# RulesCompiler.psm1 - AdGuard Rules Compiler Module
# This module provides rules compilation functionality with OOP design

# Load Common module for shared classes (CompilerLogger, CompilerResult)
using module ..\Common\Common.psm1

# Load module-specific classes
using module .\Classes\CompilerConfiguration.psm1
using module .\Classes\RulesValidatorResult.psm1

# Dot-source public functions
$publicFunctions = Get-ChildItem -Path "$PSScriptRoot\Public\*.ps1" -ErrorAction SilentlyContinue
foreach ($function in $publicFunctions) {
    . $function.FullName
}

# Dot-source private functions
$privateFunctions = Get-ChildItem -Path "$PSScriptRoot\Private\*.ps1" -ErrorAction SilentlyContinue
foreach ($function in $privateFunctions) {
    . $function.FullName
}

# Export public functions
Export-ModuleMember -Function $publicFunctions.BaseName
