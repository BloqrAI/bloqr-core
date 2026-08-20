# PowerShell Modules

Consolidated location for all PowerShell modules in the ad-blocking repository.

## Structure

```
src/compilers/powershell/
├── README.md          # This file
├── Common/            # Shared utilities and classes
│   ├── Common.psm1
│   ├── Common.psd1
│   ├── Classes/       # CompilerLogger, CompilerResult
│   └── Tests/
├── BloqrCompiler/      # Rules compilation module
│   ├── BloqrCompiler.psm1
│   ├── BloqrCompiler.psd1
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
Import-Module ./src/compilers/powershell/Common/Common.psd1
```

### BloqrCompiler
Modern OOP-based rules compiler module.

**Features:**
- CompilerConfiguration class
- `Invoke-BloqrCompiler`: shells out to `hostlist-compiler` (or `npx @adguard/hostlist-compiler`), computes a SHA-384 hash, and runs `Invoke-RulesValidator`'s syntax check (informational findings only)
- `Invoke-RulesValidator`: shells out to the `bloqr-validate` CLI ([src/validation/](../../validation/)) for standalone syntax validation
- Type-safe configuration
- Comprehensive error handling
- Environment variable support

**Usage:**
```powershell
Import-Module ./src/compilers/powershell/BloqrCompiler/BloqrCompiler.psd1
Invoke-BloqrCompiler -ConfigPath config.json
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
Import-Module ./src/compilers/powershell/AdGuardWebhook/AdGuardWebhook.psd1
Invoke-AdGuardWebhook -WebhookUrl "https://api.adguard-dns.io/webhook/xxx"
```

## Environment Variables

| Variable | Module | Description |
|----------|--------|-------------|
| `ADGUARD_COMPILER_CONFIG` | BloqrCompiler | Default config file path |
| `ADGUARD_COMPILER_OUTPUT` | BloqrCompiler | Output directory |
| `ADGUARD_WEBHOOK_URL` | AdGuardWebhook | Webhook endpoint URL |
| `ADGUARD_WEBHOOK_WAIT_TIME` | AdGuardWebhook | Wait time between calls (ms) |
| `DEBUG` | All | Enable debug logging |

## Testing

Run tests with Pester:

```powershell
# Test all modules
Invoke-Pester -Path ./src/compilers/powershell/*/Tests/

# Test specific module
Invoke-Pester -Path ./src/compilers/powershell/BloqrCompiler/Tests/
```

## Migration Notes

**Current location:** `src/compilers/powershell/` ✅

**Previous locations (deprecated):**
- `src/rules-compiler-powershell/` - Prior location before the `src/` reorg (#331/#372)
- `src/powershell-modules/` - Interim modern location
- `src/adguard-api-powershell/` - Formerly held legacy monolithic modules + an auto-generated API client; moved out entirely to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) and no longer part of this repo.

## Architecture

These modules follow modern PowerShell best practices:
- **OOP Design**: Class-based architecture
- **Dependency Injection**: Module dependencies clearly defined
- **Type Safety**: Strongly typed parameters and classes
- **Testability**: Comprehensive Pester test coverage
- **Documentation**: Inline help and examples

## Related Documentation

- [Shell Scripts](../../rules-compiler-shell/README.md) - Shell script alternatives
- [Main README](../../../README.md) - General usage

## Support

For issues or questions:
- Check module help: `Get-Help Invoke-BloqrCompiler -Full`
- Review tests for usage examples
- Open an issue with error details
