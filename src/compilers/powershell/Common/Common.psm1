# Common.psm1 - Shared common module for AdGuard PowerShell modules
# This module provides shared classes and utilities used across multiple AdGuard PowerShell modules

# Load shared classes using relative paths
using module .\Classes\CompilerLogger.psm1
using module .\Classes\CompilerResult.psm1

# Export classes (PowerShell 7+ style)
Export-ModuleMember -Function @()
