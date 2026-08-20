@{
    # Module manifest for AdGuardWebhook module
    RootModule = 'AdGuardWebhook.psm1'
    ModuleVersion = '2.0.0'
    GUID = '5c2e9f1d-3b7a-4e8f-9c1d-6a4e2b8f7d3c'
    Author = 'Jayson Knight'
    CompanyName = 'jaypatrick'
    Copyright = '(c) 2025 Jayson Knight. All rights reserved.'
    Description = 'AdGuard webhook invocation with OOP design, retry logic, statistics tracking, and structured logging'
    
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
        'Classes\WebhookConfiguration.psm1',
        'Classes\WebhookStatistics.psm1',
        'Classes\WebhookInvoker.psm1'
    )
    
    # Functions to export
    FunctionsToExport = @('Invoke-AdGuardWebhook')
    CmdletsToExport = @()
    VariablesToExport = @()
    AliasesToExport = @()
    
    PrivateData = @{
        PSData = @{
            Tags = @('AdGuard', 'PowerShell', 'Webhook', 'API', 'Automation', 'OOP')
            LicenseUri = 'https://github.com/jaypatrick/ad-blocking/blob/main/LICENSE'
            ProjectUri = 'https://github.com/jaypatrick/ad-blocking'
            ReleaseNotes = @'
Version 2.0.0
- Complete OOP refactoring with classes: WebhookConfiguration, WebhookStatistics, WebhookInvoker, CompilerLogger
- Shared Common module for reusability across PowerShell projects
- Enhanced configuration management with JSON/YAML file support
- Statistics tracking with detailed metrics (success rate, response times)
- Retry logic with configurable attempts and intervals
- Continuous invocation mode with Ctrl+C support
- Structured logging with multiple output levels
- Comprehensive environment variable support
- Multiple output formats (JSON, List, Table)
- Improved error handling and validation
- Backward compatible with v1.x function signatures
'@
        }
    }
}
