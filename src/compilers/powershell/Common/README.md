# Common Module

The Common module provides shared classes and utilities used across multiple AdGuard PowerShell modules. This module promotes code reusability and maintains consistency across the PowerShell ecosystem.

## Purpose

The Common module serves as a centralized location for:
- Shared OOP classes used by multiple modules
- Common utilities and helper functions
- Consistent logging and result handling patterns

## Shared Classes

### CompilerLogger
Structured logging class with multiple output levels and file support.

**Features:**
- 4 log levels: DEBUG, INFO, WARN, ERROR
- Dual output: colored console + optional file logging
- Environment variable support (ADGUARD_COMPILER_LOG_LEVEL, ADGUARD_COMPILER_LOG_FILE)
- Formatted output with timestamps and log levels
- Console logging can be disabled for quiet mode

**Usage:**
```powershell
using module Common

# Create logger from environment
$logger = [CompilerLogger]::FromEnvironment()

# Create logger with specific level
$logger = [CompilerLogger]::new('INFO')

# Log messages
$logger.Info("Operation started")
$logger.Warn("Configuration file not found, using defaults")
$logger.Error("Compilation failed: $errorMessage")
$logger.Debug("Processing source: $sourceName")
```

### CompilerResult
Result encapsulation class for operation outcomes.

**Features:**
- Success/failure tracking with error messages
- Statistics collection (rules processed, sources compiled)
- Timing information with formatted duration
- Multiple constructors and static factory methods
- JSON/hashtable serialization support

**Usage:**
```powershell
using module Common

# Create success result
$result = [CompilerResult]::CreateSuccess(
    "Compilation completed successfully",
    1500,  # rulesProcessed
    3,     # sourcesCompiled
    [timespan]::FromSeconds(5)
)

# Create failure result
$result = [CompilerResult]::CreateFailure("Configuration file not found")

# Check result
if ($result.Success) {
    Write-Host "Success: $($result.Message)"
    Write-Host "Duration: $($result.GetFormattedDuration())"
}

# Convert to JSON
$json = $result.ToJson()

# Convert to hashtable
$hashtable = $result.ToHashtable()
```

## Module Structure

```
Common/
├── Common.psm1              # Root module file
├── Common.psd1              # Module manifest
├── README.md                # This file
└── Classes/                 # Shared class definitions
    ├── CompilerLogger.psm1  # Structured logging class
    └── CompilerResult.psm1  # Result encapsulation class
```

## Dependencies

- PowerShell 7.0 or later
- No external module dependencies

## Dependent Modules

The following modules depend on the Common module:
- **BloqrCompiler**: Uses CompilerLogger and CompilerResult for compilation operations
- **AdGuardWebhook**: Uses CompilerLogger for webhook invocation logging
- **adguard-api-powershell**: (Future) Will use shared classes for API operations

## Environment Variables

### CompilerLogger Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ADGUARD_COMPILER_LOG_LEVEL` | Logging level (DEBUG, INFO, WARN, ERROR) | INFO |
| `ADGUARD_COMPILER_LOG_FILE` | Path to log file (optional) | None |
| `DEBUG` | Global debug flag (sets LOG_LEVEL to DEBUG if set) | Not set |

## Adding New Shared Classes

To add a new shared class to the Common module:

1. Create the class file in `Classes/` directory:
   ```powershell
   # Classes/MySharedClass.psm1
   class MySharedClass {
       # Class implementation
   }
   ```

2. Update `Common.psm1` to load the new class:
   ```powershell
   using module .\Classes\MySharedClass.psm1
   ```

3. Update `Common.psd1` ScriptsToProcess:
   ```powershell
   ScriptsToProcess = @(
       'Classes\CompilerLogger.psm1'
       'Classes\CompilerResult.psm1'
       'Classes\MySharedClass.psm1'  # Add new class
   )
   ```

4. Update this README to document the new class

## Best Practices

1. **Reusability First**: Only add classes that are truly shared across multiple modules
2. **Minimal Dependencies**: Keep Common module dependency-free to avoid circular dependencies
3. **Backward Compatibility**: Maintain API stability to prevent breaking dependent modules
4. **Documentation**: Document all public classes, methods, and properties
5. **Testing**: Create comprehensive tests for all shared classes
6. **Versioning**: Follow semantic versioning (Major.Minor.Patch)

## Version History

### Version 1.0.0
- Initial release
- CompilerLogger class with structured logging
- CompilerResult class for result encapsulation
- Environment variable support
- Cross-platform compatibility (Windows, Linux, macOS)

## License

Copyright (c) 2025 Jayson Knight. All rights reserved.

See [LICENSE](https://github.com/BloqrAI/bloqr-core/blob/main/LICENSE) for details.
