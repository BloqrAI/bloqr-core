@{
    # Module manifest for Common shared module
    RootModule = 'Common.psm1'
    ModuleVersion = '1.0.0'
    GUID = '9f8c4d2e-5b3a-4f1e-8c9d-2a6b7e4f3c1d'
    Author = 'Jayson Knight'
    CompanyName = 'jaypatrick'
    Copyright = '(c) 2025 Jayson Knight. All rights reserved.'
    Description = 'Shared common classes and utilities for AdGuard PowerShell modules'
    
    PowerShellVersion = '7.0'
    
    # Nested modules to load
    NestedModules = @(
        'Classes\CompilerLogger.psm1'
        'Classes\CompilerResult.psm1'
    )
    
    # Functions to export
    FunctionsToExport = @()
    CmdletsToExport = @()
    VariablesToExport = @()
    AliasesToExport = @()
    
    PrivateData = @{
        PSData = @{
            Tags = @('AdGuard', 'PowerShell', 'Common', 'Shared', 'OOP')
            LicenseUri = 'https://github.com/jaypatrick/ad-blocking/blob/main/LICENSE'
            ProjectUri = 'https://github.com/jaypatrick/ad-blocking'
            ReleaseNotes = 'Initial release of shared common module with CompilerLogger and CompilerResult classes'
        }
    }
}
