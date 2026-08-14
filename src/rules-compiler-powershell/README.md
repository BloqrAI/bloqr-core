# PowerShell Modules

Consolidated location for all PowerShell modules in the ad-blocking repository.

## Structure

```
src/rules-compiler-powershell/
├── README.md          # This file
├── Common/            # Shared utilities and classes
│   ├── Common.psm1
│   ├── Common.psd1
│   ├── Classes/       # CompilerLogger, CompilerResult
│   └── Tests/
├── RulesCompiler/     # Rules compilation module
│   ├── RulesCompiler.psm1
│   ├── RulesCompiler.psd1
│   ├── Classes/       # CompilerConfiguration, etc.
│   └── Tests/
└── AdGuardWebhook/    # Webhook invocation module
    ├── AdGuardWebhook.psm1
    ├── AdGuardWebhook.psd1
    ├── Classes/       # WebhookConfiguration, etc.
    └── Tests/
```

## Modules

### Common
Shared utilities and base classes used by other modules.

**Features:**
- CompilerLogger class
- CompilerResult class
- Shared helper functions

**Usage:**
```powershell
Import-Module ./src/rules-compiler-powershell/Common/Common.psd1
```

### RulesCompiler
Modern OOP-based rules compiler module.

**Features:**
- CompilerConfiguration class
- `Invoke-RulesCompiler`: shells out to `hostlist-compiler` (or `npx @adguard/hostlist-compiler`), computes a SHA-384 hash, and runs `Invoke-RulesValidator`'s syntax check (informational findings only)
- `Invoke-RulesValidator`: shells out to the `rules-validate` CLI ([src/rules-validator/](../rules-validator/)) for standalone syntax validation
- Type-safe configuration
- Comprehensive error handling
- Environment variable support

**Usage:**
```powershell
Import-Module ./src/rules-compiler-powershell/RulesCompiler/RulesCompiler.psd1
Invoke-RulesCompiler -ConfigPath config.json
```

### AdGuardWebhook
Webhook invocation module with statistics tracking.

**Features:**
- WebhookConfiguration class
- WebhookStatistics tracking
- Retry logic with exponential backoff
- Multiple output formats

**Usage:**
```powershell
Import-Module ./src/rules-compiler-powershell/AdGuardWebhook/AdGuardWebhook.psd1
Invoke-AdGuardWebhook -WebhookUrl "https://api.adguard-dns.io/webhook/xxx"
```

## Environment Variables

| Variable | Module | Description |
|----------|--------|-------------|
| `ADGUARD_COMPILER_CONFIG` | RulesCompiler | Default config file path |
| `ADGUARD_COMPILER_OUTPUT` | RulesCompiler | Output directory |
| `ADGUARD_WEBHOOK_URL` | AdGuardWebhook | Webhook endpoint URL |
| `ADGUARD_WEBHOOK_WAIT_TIME` | AdGuardWebhook | Wait time between calls (ms) |
| `DEBUG` | All | Enable debug logging |

## Testing

Run tests with Pester:

```powershell
# Test all modules
Invoke-Pester -Path ./src/rules-compiler-powershell/*/Tests/

# Test specific module
Invoke-Pester -Path ./src/rules-compiler-powershell/RulesCompiler/Tests/
```

## Migration Notes

**Current location:** `src/rules-compiler-powershell/` ✅

**Previous locations (deprecated):**
- `src/powershell-modules/` - Interim modern location
- `src/adguard-api-powershell/` - Contains legacy monolithic modules + auto-generated API client

**Note:** The `src/adguard-api-powershell/` directory remains for the auto-generated PowerShell API client and legacy compatibility. New development should use the modular structure in `src/rules-compiler-powershell/`.

## Architecture

These modules follow modern PowerShell best practices:
- **OOP Design**: Class-based architecture
- **Dependency Injection**: Module dependencies clearly defined
- **Type Safety**: Strongly typed parameters and classes
- **Testability**: Comprehensive Pester test coverage
- **Documentation**: Inline help and examples

## Related Documentation

- [PowerShell API Client](../adguard-api-powershell/README.md) - Auto-generated API wrapper
- [Shell Scripts](../shell/README.md) - Shell script alternatives
- [Main README](../../README.md) - General usage

## Support

For issues or questions:
- Check module help: `Get-Help Invoke-RulesCompiler -Full`
- Review tests for usage examples
- Open an issue with error details
