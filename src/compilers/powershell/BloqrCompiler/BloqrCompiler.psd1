@{
    # Module manifest for BloqrCompiler module
    RootModule = 'BloqrCompiler.psm1'
    ModuleVersion = '2.0.0'
    GUID = '7e4f3c1d-2a6b-4f8c-9d5e-1b3a8f2e9c4d'
    Author = 'Jayson Knight'
    CompanyName = 'jaypatrick'
    Copyright = '(c) 2025 Jayson Knight. All rights reserved.'
    Description = 'AdGuard rules compiler with OOP design, configuration management, and structured logging'
    
    PowerShellVersion = '7.0'
    
    # Required modules - Common module must be loaded first
    RequiredModules = @(
        @{
            ModuleName = 'Common'
            ModuleVersion = '1.0.0'
            GUID = '9f8c4d2e-5b3a-4f1e-8c9d-2a6b7e4f3c1d'
        }
    )
    
    # Classes to load
    NestedModules = @(
        'Classes\CompilerConfiguration.psm1',
        'Classes\RulesValidatorResult.psm1'
    )

    # Functions to export
    FunctionsToExport = @('Invoke-BloqrCompiler', 'Invoke-RulesValidator', 'Invoke-BloqrCompilerChunked', 'Invoke-BloqrCompilerBenchmark')
    CmdletsToExport = @()
    VariablesToExport = @()
    AliasesToExport = @()
    
    PrivateData = @{
        PSData = @{
            Tags = @('AdGuard', 'PowerShell', 'BloqrCompiler', 'DNS', 'AdBlocking', 'OOP')
            LicenseUri = 'https://github.com/jaypatrick/ad-blocking/blob/main/LICENSE'
            ProjectUri = 'https://github.com/jaypatrick/ad-blocking'
            ReleaseNotes = @'
Version 2.0.0
- Complete OOP refactoring with classes: CompilerConfiguration, CompilerResult, CompilerLogger
- Shared Common module for reusability across PowerShell projects
- Enhanced configuration management with JSON/YAML/TOML support
- Structured logging with multiple output levels and file support
- Comprehensive environment variable support
- Improved error handling and validation
- Backward compatible with v1.x function signatures
'@
        }
    }
}
