# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This repository is a comprehensive multi-language toolkit for ad-blocking, network protection, and AdGuard DNS management:

### Rules Compilers (4 languages)
- **TypeScript** (`src/adblock-compiler-core/`) - Deno 2.0+ with npm compatibility
- **C#/.NET 10** (`src/rules-compiler-dotnet/`) - Library and Spectre.Console CLI with DI support
- **Python 3.9+** (`src/rules-compiler-python/`) - pip-installable package with CLI and API
- **Rust** (`src/rules-compiler-rust/`) - High-performance single binary with zero runtime deps

### Shell Scripts (`src/rules-compiler-shell/`)
- **Bash** (`src/rules-compiler-shell/bash/compile-rules.sh`) - Linux/macOS
- **Zsh** (`src/rules-compiler-shell/zsh/compile-rules.zsh`) - macOS/Linux with zsh-specific features

### PowerShell Modules
- **RulesCompiler Toolkit** (`src/rules-compiler-powershell/`) - Canonical, actively-developed modular PowerShell toolkit (class-based `Common`, `RulesCompiler`, `AdGuardWebhook` modules with Pester tests)
- **AdGuard API PowerShell Client** (`src/adguard-api-powershell/`) - Auto-generated AdGuard DNS API client plus the legacy monolithic `Invoke-RulesCompiler` module, kept for compatibility

### Rust API Client & Validation Tools
- **AdGuard API Client - Rust** (`src/adguard-api-rust/`) - OpenAPI-generated library (`adguard-api-lib`) plus a hand-written interactive CLI (`adguard-api-cli`)
- **Rules Validator** (`src/rules-validator/`) - Rust validation library (`rules-validator-core`) and CLI (`rules-validator-cli`) for filter/config validation

### API Client & Tools
- **AdGuard API Client - .NET** (`src/adguard-api-dotnet/`) - C# SDK for AdGuard DNS API v1.15
- **AdGuard API Client - TypeScript** (`src/adguard-api-typescript/`) - TypeScript SDK with Deno support
- **Console UI** (`src/adguard-api-dotnet/src/AdGuard.ConsoleUI/`) - Spectre.Console interactive interface
- **Linear Import Tool** (`src/linear/`) - TypeScript tool with Deno support

### Documentation Site
- **Website** (`src/website/`) - Gatsby 5 documentation site covering guides, API reference, and security docs

### Configuration Support
All compilers support JSON, YAML, and TOML configuration formats with full @bloqr/compiler-core compatibility.

## Docker Development Environment

A fully-featured Docker environment with all compilers and tools:

```dockerfile
# Dockerfile.warp
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble
# Includes: .NET 10 SDK, Deno 2.x, Python 3.12, Rust stable, PowerShell 7
# Pre-installed: hostlist-compiler (via Deno), yq, pytest, ruff, clippy, Pester
```

Build and run:
```bash
docker build -f Dockerfile.warp -t ad-blocking-dev .
docker run -it -v $(pwd):/workspace ad-blocking-dev
```

Docker Compose (recommended):
```bash
docker compose up -d dev           # Start dev environment
docker compose exec dev bash       # Enter container
docker compose --profile test run --rm test  # Run all tests
```

Warp Environment: `jaysonknight/warp-env:ad-blocking` (ID: `Egji4sZU4TNIOwNasFU73A`)

## Common Commands

### TypeScript Rules Compiler (`src/adblock-compiler-core/`)
```bash
cd src/adblock-compiler-core

# Deno tasks
deno task start                # Start (auto-detect interactive or CLI mode)
deno task interactive          # Interactive menu mode
deno task compile              # CLI compile mode
deno task compile:yaml         # Compile using YAML config
deno task compile:toml         # Compile using TOML config
deno task dev                  # Run with watch mode
deno task test                 # Run Deno tests
deno task test:coverage        # Run tests with coverage
deno task lint                 # Lint source files
deno task fmt                  # Format source files
deno task check                # Type check
deno task version              # Show version

# CLI with options
deno task start -- -c config.yaml -r -d     # CLI mode with config
deno task start -- --interactive            # Force interactive mode
deno task start -- --validate -c config.yaml  # Validate only
deno run --allow-read --allow-write --allow-env --allow-run src/mod.ts --help
deno run --allow-read --allow-write --allow-env --allow-run src/mod.ts --version
```

### Shell Scripts (`src/rules-compiler-shell/`)
```bash
# Bash (Linux/macOS)
./src/rules-compiler-shell/bash/compile-rules.sh                    # Use default config
./src/rules-compiler-shell/bash/compile-rules.sh -c config.yaml -r  # YAML config, copy to rules
./src/rules-compiler-shell/bash/compile-rules.sh -v                 # Show version

# Zsh (macOS/Linux)
./src/rules-compiler-shell/zsh/compile-rules.zsh                    # Use default config
./src/rules-compiler-shell/zsh/compile-rules.zsh -c config.yaml -r  # YAML config, copy to rules
./src/rules-compiler-shell/zsh/compile-rules.zsh -v                 # Show version
```

### .NET Rules Compiler (`src/rules-compiler-dotnet/`)
```bash
cd src/rules-compiler-dotnet
dotnet restore RulesCompiler.slnx
dotnet build RulesCompiler.slnx
dotnet test RulesCompiler.slnx
dotnet run --project src/RulesCompiler.Console/RulesCompiler.Console.csproj

# Command-line options
dotnet run --project src/RulesCompiler.Console -- --config path/to/config.yaml
dotnet run --project src/RulesCompiler.Console -- --config config.json --copy
dotnet run --project src/RulesCompiler.Console -- --config config.yaml --verbose
dotnet run --project src/RulesCompiler.Console -- --config config.yaml --validate
dotnet run --project src/RulesCompiler.Console -- --version
```

### Python Rules Compiler (`src/rules-compiler-python/`)
```bash
cd src/rules-compiler-python

# Install in development mode
pip install -e .

# Install with dev dependencies
pip install -e ".[dev]"

# Run tests
pytest
pytest -v                    # Verbose output
pytest --cov=rules_compiler  # With coverage

# CLI usage
rules-compiler                           # Use default config
rules-compiler -c config.yaml            # Specific config
rules-compiler -c config.json -r         # Compile and copy to rules
rules-compiler -c config.toml -o out.txt # Custom output
rules-compiler -V                        # Show version info
rules-compiler -d                        # Debug output
rules-compiler --help                    # Show help
```

### Rust Rules Compiler (`src/rules-compiler-rust/`)
```bash
cd src/rules-compiler-rust

# Build
cargo build              # Debug build
cargo build --release    # Release build (optimized)

# Run tests
cargo test
cargo test -- --nocapture  # With output

# CLI usage
cargo run -- -c config.yaml              # Specific config
cargo run -- -c config.json -r           # Compile and copy to rules
cargo run -- -c config.toml -o out.txt   # Custom output
cargo run -- -V                          # Show version info
cargo run -- -d                          # Debug output
cargo run -- --help                      # Show help

# Release binary
./target/release/rules-compiler -c config.yaml
```

### .NET API Client + Console UI (`src/adguard-api-dotnet/`)
```bash
cd src/adguard-api-dotnet
dotnet restore src/AdGuard.ApiClient.sln
dotnet build src/AdGuard.ApiClient.sln
dotnet test src/AdGuard.ApiClient.sln
dotnet run --project src/AdGuard.ConsoleUI/AdGuard.ConsoleUI.csproj

# Run benchmarks
dotnet run --project src/AdGuard.ApiClient.Benchmarks -c Release
```

### TypeScript API Client (`src/adguard-api-typescript/`)
```bash
cd src/adguard-api-typescript

# Deno tasks
deno task start            # Start CLI
deno task sync             # Sync rules
deno task cli              # Run CLI directly
deno task test             # Run tests
deno task test:coverage    # Run tests with coverage
deno task lint             # Lint source files
deno task fmt              # Format source files
deno task check            # Type check
```

### Linear Import Tool (`src/linear/`)
```bash
cd src/linear

# Deno tasks
deno task import           # Run import tool
deno task import:docs      # Import documentation
deno task import:dry-run   # Preview import
deno task cli              # Run CLI directly
deno task test             # Run tests
deno task lint             # Lint source files
deno task fmt              # Format source files
deno task check            # Type check
```

### PowerShell RulesCompiler Toolkit (`src/rules-compiler-powershell/`) - canonical
```powershell
# Import the modules
Import-Module ./src/rules-compiler-powershell/Common/Common.psd1
Import-Module ./src/rules-compiler-powershell/RulesCompiler/RulesCompiler.psd1
Import-Module ./src/rules-compiler-powershell/AdGuardWebhook/AdGuardWebhook.psd1

# Compile filter rules
Invoke-RulesCompiler

# Run Pester tests
Invoke-Pester -Path ./src/rules-compiler-powershell -Recurse

# Lint with PSScriptAnalyzer
Invoke-ScriptAnalyzer -Path src/rules-compiler-powershell -Recurse
```

### PowerShell RulesCompiler Module (`src/adguard-api-powershell/`) - legacy, kept for compatibility
```powershell
# Import the module
Import-Module ./src/adguard-api-powershell/Invoke-RulesCompiler.psm1

# Check versions and platform info
Get-CompilerVersion | Format-List

# Compile filter rules
Invoke-RulesCompiler

# Compile and copy to rules directory
Invoke-RulesCompiler -CopyToRules

# Run interactive harness
./src/adguard-api-powershell/RulesCompiler-Harness.ps1

# Run Pester tests
Invoke-Pester -Path ./src/adguard-api-powershell/Tests/RulesCompiler-Tests.ps1

# Lint with PSScriptAnalyzer
Invoke-ScriptAnalyzer -Path src/adguard-api-powershell -Recurse
```

## Running Individual Tests

### TypeScript (Deno)
```bash
cd src/adblock-compiler-core
deno test src/cli.test.ts                  # By file
deno test --filter "parseArgs"             # By test name
deno task test:coverage                    # With coverage
```

### .NET (xUnit)
```bash
cd src/adguard-api-dotnet
dotnet test src/AdGuard.ApiClient.sln --filter "FullyQualifiedName~DevicesApiTests"   # By class
dotnet test src/AdGuard.ApiClient.sln --filter "Name~GetAccountLimits"                # By method

cd ../rules-compiler-dotnet
dotnet test RulesCompiler.slnx --filter "FullyQualifiedName~ConfigurationValidatorTests"
dotnet test RulesCompiler.slnx --filter "FullyQualifiedName~TransformationTests"
```

### PowerShell (Pester)
```powershell
# Run all PowerShell tests
Invoke-Pester -Path ./src/adguard-api-powershell/Tests/

# Run specific test file
Invoke-Pester -Path ./src/adguard-api-powershell/Tests/RulesCompiler-Tests.ps1

# Run with detailed output
Invoke-Pester -Path ./src/adguard-api-powershell/Tests/ -Output Detailed
```

### Python (pytest)
```bash
cd src/rules-compiler-python
pytest                                    # Run all tests
pytest -v                                 # Verbose output
pytest tests/test_config.py               # Specific file
pytest -k "test_read_yaml"                # By test name
pytest --cov=rules_compiler               # With coverage
```

### Rust (cargo test)
```bash
cd src/rules-compiler-rust
cargo test                                # Run all tests
cargo test -- --nocapture                 # With output
cargo test test_count_rules               # Specific test
cargo test config::                       # Tests in module
```

## Architecture

### Filter Rules (`data/output/`)
- `data/output/adguard_user_filter.txt` - Main tracked filter list consumed by AdGuard DNS

### Rules Compiler - TypeScript (`src/adblock-compiler-core/`)
- TypeScript compiler using @bloqr/compiler-core
- Deno 2.0+ runtime with npm compatibility
- Supports JSON, YAML, and TOML configuration formats
- **Library API** (`src/lib/`):
  - `RulesCompiler` - Main service class with fluent builder pattern
  - `ConfigurationBuilder` - Programmatic configuration creation
  - Separate library export: `@rules-compiler/typescript/lib`
- **Dual Mode Support**:
  - Interactive menu mode (default when no args)
  - CLI mode (when config path or action flags provided)
- `src/cli.ts` - Command-line interface with argument parsing and mode detection
- `src/config-reader.ts` - Multi-format configuration reader
- `src/compiler.ts` - Core compilation logic
- `src/console/` - Interactive console UI components:
  - `app.ts` - `ConsoleApplication` class with menu-driven interface
  - `utils.ts` - Console utilities (spinners, tables, colored output)
- `src/mod.ts` - Deno entry point
- `deno.json` - Deno configuration and tasks
- Key classes: `RulesCompiler`, `RulesCompilerBuilder`, `ConfigurationBuilder`, `ConsoleApplication`
- Uses Deno's built-in testing framework

### Shell Scripts (`src/rules-compiler-shell/`)
- Cross-platform shell scripts for filter compilation
- `bash/compile-rules.sh` - Bash script for Linux/macOS
- `zsh/compile-rules.zsh` - Zsh script with native zsh features (zparseopts, EPOCHREALTIME)
- Supports JSON, YAML, TOML via external tools (yq, Python)

### Rules Compiler - .NET (`src/rules-compiler-dotnet/`)
- .NET 10 library for filter compilation
- Supports JSON, YAML, and TOML configuration formats
- `Bloqr.Compiler.Abstractions` - Interfaces, event-args, and model/DTO types shared across the compiler stack
- `Bloqr.Compiler.Core` - Configuration reading/validation, chunking, file-locking, plugin management, and the compilation pipeline, built on `Bloqr.Compiler.Abstractions`
- `RulesCompiler` - Thin library referencing `Bloqr.Compiler.Abstractions`/`Bloqr.Compiler.Core`, plus compiler-specific services (e.g. `FilterCompiler`)
- `RulesCompiler.Console` - Spectre.Console interactive and CLI frontend
- `RulesCompiler.Tests` - xUnit tests
- Key interfaces: `IRulesCompilerService`, `IConfigurationReader`, `IFilterCompiler`
- Features: Configuration validation, verbose mode, dependency injection

### Rules Compiler - Python (`src/rules-compiler-python/`)
- Python 3.9+ package for filter compilation
- Supports JSON, YAML, and TOML configuration formats
- `rules_compiler/config.py` - Multi-format configuration reader
- `rules_compiler/compiler.py` - Core `RulesCompiler` class and `compile_rules()` function
- `rules_compiler/cli.py` - argparse-based CLI
- Install via `pip install -e .` for development
- Key classes: `RulesCompiler`, `CompilerConfiguration`, `CompilerResult`
- Tools: pytest, mypy, ruff

### Rules Compiler - Rust (`src/rules-compiler-rust/`)
- High-performance Rust library and CLI for filter compilation
- Supports JSON, YAML, and TOML configuration formats
- `src/config.rs` - Configuration structs and parsing
- `src/compiler.rs` - `RulesCompiler` struct and `compile_rules()` function
- `src/main.rs` - clap-based CLI with argument parsing
- `src/error.rs` - `CompilerError` enum with thiserror
- Single binary distribution with zero runtime dependencies (except hostlist-compiler)
- Key structs: `RulesCompiler`, `CompilerConfiguration`, `CompilerResult`, `VersionInfo`
- LTO optimization enabled for small binary size

### API Client - .NET (`src/adguard-api-dotnet/`)
- Auto-generated from `api/openapi.json` (primary) and `api/openapi.yaml` (optional) - AdGuard DNS API v1.15
- `Helpers/ConfigurationHelper.cs` - Fluent auth, timeouts, user agent
- `Helpers/RetryPolicyHelper.cs` - Polly-based retry for 408/429/5xx
- Uses Newtonsoft.Json and JsonSubTypes
- Benchmarks project for performance testing

### API Client - TypeScript (`src/adguard-api-typescript/`)
- TypeScript SDK for AdGuard DNS API v1.15 with feature parity to .NET version
- Deno 2.0+ runtime with npm compatibility
- **Library API** (`src/lib/`):
  - `AdGuardDnsClientBuilder` - Fluent builder for client configuration
  - `PagedListBuilder` - Pagination support for list operations
  - Separate library export: `@adguard/api-typescript/lib`
- `src/client.ts` - Main `AdGuardDnsClient` class with fluent API
- `src/api/` - API endpoint implementations (account, devices, DNS servers, statistics, etc.)
- `src/repositories/` - Higher-level repository pattern abstractions
- `src/cli/` - Interactive CLI with menu-driven interface
- `src/mod.ts` - Deno entry point
- Key classes: `AdGuardDnsClient`, `AdGuardDnsClientBuilder`, `DeviceRepository`, `DnsServerRepository`
- Dependencies (via npm:): axios, commander, inquirer, chalk

### Linear Import Tool (`src/linear/`)
- TypeScript tool for importing documentation into Linear project management
- Deno 2.0+ runtime with npm compatibility
- `src/linear-import.ts` - Main CLI entry point
- `src/mod.ts` - Deno entry point
- `src/parser.ts` - Markdown documentation parser
- `src/linear-client.ts` - Linear API client wrapper
- `src/types.ts` - TypeScript type definitions
- Dependencies (via npm:): @linear/sdk, commander, marked, dotenv

### Console UI (`src/adguard-api-dotnet/src/AdGuard.ConsoleUI/`)
- Spectre.Console menu-driven interface
- `ApiClientFactory` configures SDK from settings or interactive prompt
- Features: Device management, DNS servers, statistics, query logs, filter lists

### PowerShell Toolkit (`src/rules-compiler-powershell/`) - canonical
- **Common** (`Common/`) - Shared `CompilerLogger` and `CompilerResult` classes used by other modules
- **RulesCompiler** (`RulesCompiler/`) - Class-based rules compiler module (`CompilerConfiguration`, `CompilerResult`, `CompilerLogger`)
- **AdGuardWebhook** (`AdGuardWebhook/`) - Class-based webhook invocation module (`WebhookConfiguration`, `WebhookInvoker`, `WebhookStatistics`)
- Each module ships its own `.psd1` manifest and `Tests/` Pester suite

### PowerShell Modules (`src/adguard-api-powershell/`) - legacy, kept for compatibility
- **RulesCompiler Module** - Cross-platform PowerShell API mirroring TypeScript compiler
  - `Invoke-RulesCompiler.psm1` - Main module with exported functions
  - `RulesCompiler.psd1` - Module manifest
  - `RulesCompiler-Harness.ps1` - Interactive test harness
  - `Tests/` - Pester test suite
  - Functions: `Read-CompilerConfiguration`, `Invoke-FilterCompiler`, `Write-CompiledOutput`, `Invoke-RulesCompiler`, `Get-CompilerVersion`
- **AdGuard DNS API Client** - Auto-generated PowerShell client for the AdGuard DNS API, plus `Invoke-WebHook.psm1`

### AdGuard API Client - Rust (`src/adguard-api-rust/`)
- `adguard-api-lib` - OpenAPI-generated client library for AdGuard DNS API v1.15 (excluded from clippy/lint CI since it's generated)
- `adguard-api-cli` - Hand-written interactive Spectre-style CLI built on top of `adguard-api-lib`

### Rules Validator (`src/rules-validator/`)
- `rules-validator-core` - Rust library for validating filter/config files
- `rules-validator-cli` - CLI frontend for the validation library

### Documentation Website (`src/website/`)
- Gatsby 5 static site with guides, API reference, and security documentation pages
- `npm install && npm run develop` to preview locally; `npm run build` to build for deploy

## Configuration Schema

All compilers support the same @bloqr/compiler-core configuration schema:

### Root-Level Properties
| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `name` | string | Yes | Filter list name |
| `description` | string | No | Description |
| `homepage` | string | No | Homepage URL |
| `license` | string | No | License identifier |
| `version` | string | No | Version number |
| `sources` | array | Yes | List of filter sources |
| `transformations` | array | No | Global transformations |
| `inclusions` | array | No | Global include patterns |
| `exclusions` | array | No | Global exclude patterns |

### Source Properties
| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `source` | string | Yes | URL or file path |
| `name` | string | No | Source identifier |
| `type` | string | No | `adblock` or `hosts` |
| `transformations` | array | No | Source-specific transforms |
| `inclusions` | array | No | Source-specific includes |
| `exclusions` | array | No | Source-specific excludes |

### Available Transformations
RemoveComments, Compress, RemoveModifiers, Validate, ValidateAllowIp, Deduplicate, InvertAllow, RemoveEmptyLines, TrimLines, InsertFinalNewLine, ConvertToAscii

## Environment Variables

| Variable | Description |
|----------|-------------|
| `ADGUARD_API_KEY` | Universal API credential (works with all languages - recommended) |
| `AdGuard:ApiKey` | .NET appsettings.json format |
| `ADGUARD_AdGuard__ApiKey` | .NET environment variable hierarchical format (legacy) |
| `ADGUARD_LINEAR_API_KEY` | Linear API key for Linear import scripts (`src/linear/`) |
| `ADGUARD_LINEAR_TEAM_ID` | Optional Linear team ID |
| `ADGUARD_LINEAR_PROJECT_NAME` | Optional Linear project name |
| `DEBUG` | Set to any value to enable debug logging |
| `LOG_LEVEL` | Log level (DEBUG, INFO, WARN, ERROR, SILENT) |
| `LOG_FORMAT` | Set to `json` for structured logging |
| `RULESCOMPILER_config` | Default configuration file path (.NET compiler) |
| `RULESCOMPILER_Logging__LogLevel__Default` | Log level for .NET compiler |

## CI/CD Alignment

GitHub Actions workflows validate:
- `.github/workflows/dotnet.yml` - Builds/tests .NET projects (API client and rules compiler) with .NET 10
- `.github/workflows/typescript.yml` - Deno 2.x for all TypeScript projects (rules compiler, API client, linear import)
- `.github/workflows/rust-clippy.yml` - Builds, tests, formats, and lints all Rust workspace members (rules compiler, validation, API client CLI)
- `.github/workflows/python.yml` - Builds and tests the Python rules compiler across supported Python versions
- `.github/workflows/powershell.yml` - Pester tests and PSScriptAnalyzer for both PowerShell trees
- `.github/workflows/build-scripts-tests.yml` - Exercises the root `build.sh`/`build.ps1` launcher scripts
- `.github/workflows/gatsby.yml` - Builds the `src/website` documentation site
- `.github/workflows/security.yml` - Consolidated security scanning (CodeQL, DevSkim, PSScriptAnalyzer)
- `.github/workflows/docker-image.yml` - Builds the `Dockerfile.warp` development image
- `.github/workflows/validation-compliance.yml` - Runs the Rust validation CLI against filter/config fixtures
- `.github/workflows/release.yml` - Builds and publishes release binaries (.NET, Rust, Python)
- `.github/workflows/claude.yml` - Claude AI integration for @claude mentions
- `.github/workflows/claude-code-review.yml` - Automated PR code review
- `.github/workflows/label.yml` / `.github/workflows/stale.yml` / `.github/workflows/summary.yml` - Repository housekeeping (labeling, stale-issue management, PR summaries)

## Prerequisites

| Requirement | Version | Required For |
|-------------|---------|--------------|
| .NET SDK | 10.0+ | .NET compiler, API client |
| Deno | 2.0+ | TypeScript projects (rules compiler, API client, linear) |
| PowerShell | 7+ | PowerShell scripts |
| Python | 3.9+ | Python compiler |
| Rust | 1.85+ | Rust compiler (install via rustup) |
| adblock-compiler | 0.6.0 | TypeScript compiler (via JSR: `deno add @bloqr/compiler-core`) |
| Docker | 24.0+ | Container development (optional but recommended) |

## Key File Locations

- **Main filter list**: `data/output/adguard_user_filter.txt`
- **Compiler configs**: `src/rules-compiler-*/`
- **Deno configs**: `src/*/deno.json`
- **OpenAPI spec**: `api/openapi.yaml`
- **Docker config**: `Dockerfile.warp`, `docker-compose.yml`, `.dockerignore`
- **Documentation**: `docs/`
- **Environment template**: `.env.example`
