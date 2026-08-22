# PowerShell Modules

Consolidated location for all PowerShell modules in the ad-blocking repository — the sole cross-platform scripting-language compiler (PowerShell 7+ runs on Windows/Linux/macOS); the earlier separate bash/zsh scripts have been retired.

## Structure

```
src/compilers/powershell/
├── README.md          # This file
├── Common/            # Shared utilities and classes
│   ├── Common.psm1
│   ├── Common.psd1
│   ├── Classes/       # CompilerLogger, CompilerResult
│   └── Tests/
└── BloqrCompiler/      # Rules compilation module
    ├── BloqrCompiler.psm1
    ├── BloqrCompiler.psd1
    ├── Classes/       # CompilerConfiguration, etc.
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
- `Invoke-BloqrCompilerChunked`: splits a configuration's sources into up to `-MaxParallel` chunks and compiles them in parallel via `ForEach-Object -Parallel`, merging/deduplicating the results - see [Benchmarking](#benchmarking)
- `Invoke-BloqrCompilerBenchmark`: benchmarks real compilation performance, chunked vs unchunked - see [Benchmarking](#benchmarking)
- `Invoke-RulesValidator`: shells out to the `bloqr-validate` CLI ([src/validation/](../../validation/)) for standalone syntax validation
- Type-safe configuration
- Comprehensive error handling
- Environment variable support

**Usage:**
```powershell
Import-Module ./src/compilers/powershell/BloqrCompiler/BloqrCompiler.psd1
Invoke-BloqrCompiler -ConfigPath config.json
```

### Benchmarking

`Invoke-BloqrCompilerBenchmark` compiles the canned `benchmarks/data/{small,medium,large,xlarge}.txt`
datasets through the real `Invoke-BloqrCompiler` (unchunked) and `Invoke-BloqrCompilerChunked`
(chunked) pipelines - not a simulation - and reports the actual elapsed time for both. Part of
[epic #415](https://github.com/BloqrAI/bloqr-core/issues/415)'s per-compiler benchmark work;
see that issue's other sub-issues for the equivalent subcommand/switch in each of the other
four language wrappers.

Unlike the Rust/.NET/Python wrappers (see [#424](https://github.com/BloqrAI/bloqr-core/issues/424)),
both paths here shell out to the exact same `hostlist-compiler`/`npx` binary
(`Invoke-BloqrCompilerChunked` was built alongside the benchmark, deliberately reusing the
same compiler as `Invoke-BloqrCompiler` rather than a second tool), so there is no
divergent-compiler risk - any timing delta reflects chunking overhead alone.

```powershell
Import-Module ./src/compilers/powershell/BloqrCompiler/BloqrCompiler.psd1

# Benchmark all four canned dataset sizes, chunked vs unchunked (auto-discovers benchmarks/data)
Invoke-BloqrCompilerBenchmark

# Just one size, with 8 duplicated sources and 8 parallel workers for the chunked run
Invoke-BloqrCompilerBenchmark -Size large -Sources 8 -MaxParallel 8

# Machine-readable output for the root comparison script (see benchmarks/)
Invoke-BloqrCompilerBenchmark -AsJson

# Point at a benchmarks/data directory explicitly (e.g. when not run from a repo checkout)
Invoke-BloqrCompilerBenchmark -DataDirectory /path/to/benchmarks/data
```

| Parameter | Description |
|-----------|-------------|
| `-Size` | Dataset size to benchmark: `small`, `medium`, `large`, `xlarge`, or `all` (default: `all`) |
| `-DataDirectory` | Directory containing the canned benchmark data (default: auto-discovered) |
| `-Sources` | Number of identical duplicated sources for the chunked run (default: 4) |
| `-MaxParallel` | Max parallel workers for the chunked run (default: CPU count, max 8) |
| `-AsJson` | Emit a JSON string (camelCase keys, matching the other four language wrappers) instead of returning result objects |

Both runs cover the same total workload (`-Sources` identical copies of the dataset file, one
per chunk), so chunking strategy is the only intended variable. `Invoke-BloqrCompiler`'s
mandatory `bloqr-validate` syntax check still applies to the unchunked run by default - a
missing `bloqr-validate` binary or an invalid dataset fails that run closed, same as any other
`Invoke-BloqrCompiler` invocation.

## Environment Variables

| Variable | Module | Description |
|----------|--------|-------------|
| `ADGUARD_COMPILER_CONFIG` | BloqrCompiler | Default config file path |
| `ADGUARD_COMPILER_OUTPUT` | BloqrCompiler | Output directory |
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

- [Main README](../../../README.md) - General usage

## Support

For issues or questions:
- Check module help: `Get-Help Invoke-BloqrCompiler -Full`
- Review tests for usage examples
- Open an issue with error details
