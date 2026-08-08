# Bloqr List Utils
A comprehensive multi-language toolkit for ad-blocking, network protection, and AdGuard DNS management. Features filter rule compilers in **4 core languages** (TypeScript, .NET, Python, Rust) plus **PowerShell modules**, complete **API SDKs** in C#, TypeScript, and Rust with interactive console interfaces, validation tools, and shell script wrappers.

🚀 **Active Development** - Multi-language support, Docker development environment, comprehensive CI/CD integration, and extensive test coverage across all components.

## Table of Contents

- [Features](#features)
- [Project Structure](#project-structure)
- [Quick Start](#quick-start)
- [Docker Development Environment](#docker-development-environment)
- [Rules Compilers](#rules-compilers)
  - [TypeScript](#typescript-compiler)
  - [.NET](#net-compiler)
  - [Python](#python-compiler)
  - [Rust](#rust-compiler)
  - [Shell Scripts](#shell-scripts)
  - [PowerShell Module](#powershell-module)
- [AdGuard API Clients](#adguard-api-clients)
  - [C# SDK](#c-sdk)
  - [Rust SDK](#rust-sdk)
  - [TypeScript SDK](#typescript-sdk)
- [Validation & Tools](#validation--tools)
- [Console Applications](#console-applications)
  - [.NET Console UI](#net-console-ui)
  - [Rust CLI](#rust-cli)
  - [TypeScript CLI](#typescript-cli)
- [Configuration](#configuration)
- [Testing](#testing)
- [CI/CD](#cicd)
- [Documentation](#documentation)
- [Contributing](#contributing)
- [License](#license)

## Features

> 🔒 **Security First**: All compilers include **mandatory validation** to protect against malicious filter lists, tampering, and man-in-the-middle attacks. [Learn why validation matters →](docs/WHY_VALIDATION_MATTERS.md)

### Rules Compilers (4 Languages + PowerShell)

| Language | Runtime | Distribution | Key Features |
|----------|---------|--------------|--------------|
| **TypeScript** | Deno 2.0+ | Deno | Secure by default, built-in TypeScript, interactive CLI |
| **C#/.NET** | .NET 10 | Binary/NuGet | Interactive CLI, config validation, DI support, Spectre.Console UI |
| **Python** | Python 3.9+ | pip | Type hints, PyPI-ready packaging, full library API |
| **Rust** | Native binary | Cargo/Binary | Zero-runtime deps, LTO optimization, workspace unified |
| **PowerShell** | PowerShell 7+ | Modules | Modern class-based modules, Pester tests, webhook support |

All compilers dogfood **[`@bloqr/compiler-core`](https://jsr.io/@bloqr/compiler-core)** — this repo's own open-source, dependency-free compilation engine (`src/adblock-compiler-core/`), published to JSR at v1.0.0. [📘 See comprehensive guide →](docs/guides/adblock-compiler-guide.md)

**Why @bloqr/compiler-core?**
- 📦 **We own it**: canonical source lives in this repo at `src/adblock-compiler-core/`, not a third-party dependency
- 🪶 **Dependency-free core**: no `@adguard/agtree` or other third-party AdGuard library, no Cloudflare-specific code — see [ADR 0001](docs/adr/0001-canonical-rules-compilation-engine.md)
- 🔧 **Dependency Injection**: Full DI support for testability and customization
- 📘 **Superior Type Safety**: Complete TypeScript interfaces with JSDoc coverage
- ⚡ **Chunked parallel compilation**: for large rule lists (10M+ entries)
- 📦 **JSR Distribution**: `deno add jsr:@bloqr/compiler-core`

This is deliberately kept separate from Bloqr's commercial `@bloqr/compiler` product ([`bloqr-compiler`](https://github.com/BloqrAI/bloqr-compiler) repo), which adds AST tooling, linting, plugins, and Cloudflare Workers deployment on top of AdGuard libraries. See [`src/adblock-compiler-core/README.md`](src/adblock-compiler-core/README.md#architecture) for the full split and the backporting relationship between the two.

**Compilation Features:**
- **All 11 transformations**: Deduplicate, Validate, RemoveComments, Compress, RemoveModifiers, etc.
- **JSON config**: schema-validated; YAML/TOML remain supported for backward compatibility
- **Source-specific settings**: Per-source transformations, inclusions, exclusions
- **Pattern matching**: Wildcards, regex, file-based patterns
- **🔒 SHA-384 hash verification**: Automatic tamper detection for all sources
- **🔒 URL security validation**: HTTPS enforcement, domain validation, content verification
- **🔒 Runtime enforcement**: Cryptographic proof that validation occurred

### AdGuard DNS API SDKs

| SDK | Language | Features |
|-----|----------|----------|
| **C# SDK** | .NET 10 | Full async/await, Polly resilience (retry on 408/429/5xx), DI support |
| **TypeScript SDK** | Deno 2.0+ | Full API coverage, repository pattern, retry policies, interactive CLI |
| **Rust SDK** | Rust 2024 | Auto-generated from OpenAPI, Tokio async runtime, single binary |

Both SDKs provide complete coverage of AdGuard DNS API v1.15 including devices, DNS servers, query logs, statistics, filter lists, web services, and dedicated IP management.

### Interactive Console Applications

- **C# Console UI** - Spectre.Console menu-driven interface with rich formatting
- **Rust CLI** - dialoguer-based interactive menus with TOML config persistence

### Additional Features

- **Shell Scripts**: Bash, Zsh, PowerShell Core, and Windows Batch wrappers
- **Docker Environment**: Pre-configured container with .NET 10, Deno, PowerShell 7
- **Comprehensive Testing**: Deno test, xUnit, pytest, cargo test, Pester across all components
- **CI/CD Integration**: GitHub Actions for build, test, security scanning, and releases

## Project Structure

```
bloqr-lists/
├── .github/                           # GitHub configuration
│   ├── workflows/                     # CI/CD pipelines
│   │   ├── dotnet.yml                 # .NET build and test
│   │   ├── typescript.yml             # TypeScript lint and build
│   │   ├── powershell.yml             # PowerShell linting
│   │   ├── release.yml                # Build and publish binaries
│   │   ├── codeql.yml                 # CodeQL security scanning
│   │   ├── devskim.yml                # DevSkim security analysis
│   │   └── claude*.yml                # Claude AI integration
│   └── ISSUE_TEMPLATE/                # Issue templates
├── api/                               # OpenAPI specifications (centralized)
│   ├── README.md                      # API spec documentation
│   ├── openapi.json                   # AdGuard DNS API v1.15 (primary)
│   └── openapi.yaml                   # AdGuard DNS API v1.15 (optional)
├── docs/                              # Documentation
│   ├── api/                           # Auto-generated API reference
│   ├── guides/                        # Usage guides and tutorials
│   ├── getting-started.md             # Quick start guide
│   ├── compiler-comparison.md         # Compiler comparison matrix
│   ├── configuration-reference.md     # Configuration schema reference
│   ├── docker-guide.md                # Docker development guide
│   ├── AGENTS.md                      # AI agent documentation
│   ├── RUST_WORKSPACE.md              # Rust workspace documentation
│   └── WARP.md                        # Warp terminal integration
├── data/                              # Filter rules and data
│   ├── input/                         # Source filter lists (local & remote refs)
│   │   ├── README.md                  # Input directory documentation
│   │   ├── example-custom-rules.txt   # Example local rules
│   │   ├── internet-sources.txt.example # Example remote sources config
│   │   └── .gitignore                 # Ignore large/sensitive files
│   ├── output/                        # Compiled filter output
│   │   └── adguard_user_filter.txt    # Main tracked filter list (adblock format)
│   ├── archive/                       # Archived processed input files
│   │   ├── README.md                  # Archive directory documentation
│   │   └── .gitignore                 # Ignore archive contents
│   └── Config/                        # Compiler configurations (optional)
├── src/                               # Source code
│   ├── adblock-compiler-core/         # TypeScript/Deno compiler (source of @bloqr/compiler-core on JSR)
│   ├── rules-compiler-dotnet/         # C#/.NET 10 compiler
│   ├── rules-compiler-python/         # Python 3.9+ compiler
│   ├── rules-compiler-rust/           # Rust compiler (single binary)
│   ├── shell/                         # Shell script wrappers
│   │   ├── bash/                      # Bash scripts
│   │   └── zsh/                       # Zsh scripts
│   ├── adguard-api-dotnet/            # C# API SDK + Console UI
│   │   ├── src/AdGuard.ApiClient/     # C# SDK library
│   │   ├── src/AdGuard.ConsoleUI/     # Spectre.Console interface
│   │   └── src/AdGuard.ApiClient.Tests/ # xUnit tests
│   ├── adguard-api-rust/              # Rust API SDK + CLI
│   │   ├── adguard-api-lib/           # Rust SDK library
│   │   └── adguard-api-cli/           # Interactive CLI application
│   ├── adguard-api-typescript/        # TypeScript API SDK + CLI
│   │   ├── src/api/                   # API client implementations
│   │   ├── src/cli/                   # Interactive CLI application
│   │   └── tests/                     # Deno test suite
│   ├── adguard-api-powershell/        # PowerShell API client (legacy)
│   ├── powershell/                    # PowerShell modules (modern)
│   │   ├── Common/                    # Shared utilities
│   │   ├── RulesCompiler/             # Rules compiler module
│   │   └── AdGuardWebhook/            # Webhook module
│   ├── rules-validator/            # Rust validation library
│   │   ├── rules-validator-core/   # Core validation logic
│   │   └── rules-validator-cli/    # CLI tool
│   ├── website/                       # Gatsby documentation website
│   │   ├── src/pages/                 # Static pages (home, getting started)
│   │   ├── src/templates/             # Dynamic page templates
│   │   ├── src/components/            # React components
│   │   └── gatsby-config.js           # Gatsby configuration
│   └── linear/                        # Linear integration scripts
├── tools/                             # Utility and build scripts
│   ├── README.md                      # Tools documentation
│   ├── test-build-scripts.sh          # Bash build script tests
│   ├── test-build-scripts.ps1         # PowerShell build script tests
│   ├── test-modules.ps1               # PowerShell module tests
│   ├── check-validation-compliance.sh # Validation compliance check
│   └── Migrate-To-NewStructure.ps1    # Structure migration script
├── Dockerfile.warp                    # Docker dev environment
├── CLAUDE.md                          # AI assistant instructions
├── CONTRIBUTING.md                    # Contribution guidelines
├── SECURITY.md                        # Security policy
├── README.md                          # This file
├── LICENSE                            # GPL-3.0 license
├── build.sh                           # Multi-language build script (Bash)
├── build.ps1                          # Multi-language build script (PowerShell)
├── launcher.sh                        # Interactive launcher (Bash)
└── launcher.ps1                       # Interactive launcher (PowerShell)
```

## Quick Start

### 🚀 Interactive Launcher (Easiest Way)

The repository includes feature-rich interactive launchers that provide an intuitive menu system for all tools and tasks:

**Bash Launcher (Linux/macOS):**
```bash
./launcher.sh
```

**PowerShell Launcher (Windows/Cross-platform):**
```powershell
.\launcher.ps1
```

**Features:**
- 🔨 **Build Tools** - Build projects with debug/release profiles
- ⚙️ **Compile Filter Rules** - Run compilers in any language
- 🌐 **AdGuard API Clients** - Launch interactive API tools
- 🔍 **Validation & Testing** - Run tests and compliance checks
- 📦 **Project Management** - Clean builds, update dependencies
- ℹ️ **System Information** - Check installed tools and project status

The launcher provides guided navigation with numbered menus, colored output, and automatic tool detection. Perfect for newcomers and experienced users alike!

### Prerequisites

| Requirement | Version | Required For |
|-------------|---------|--------------|
| [Deno](https://deno.land/) | 2.0+ | TypeScript compiler, TypeScript API client, Linear import tool |
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | 10.0+ | .NET compiler, .NET API client, Console UI |
| [Rust](https://rustup.rs/) | 1.85+ | Rust workspace (rules compiler, API client, validation tools) |
| [Python](https://www.python.org/) | 3.9+ | Python rules compiler |
| [PowerShell](https://github.com/PowerShell/PowerShell) | 7+ | PowerShell modules and scripts |
| [Docker](https://www.docker.com/) | 24.0+ | Development environment (optional but recommended) |

> **Note**: All Rust projects are unified in a single workspace at the repository root. See [docs/RUST_WORKSPACE.md](docs/RUST_WORKSPACE.md) for details.

### Install Deno

```bash
# macOS/Linux
curl -fsSL https://deno.land/install.sh | sh

# Windows (PowerShell)
irm https://deno.land/install.ps1 | iex
```

The `@bloqr/compiler-core` package is accessed via Deno's JSR integration.

### Clone and Setup

```bash
git clone https://github.com/BloqrAI/bloqr-lists.git
cd bloqr-lists

# TypeScript compiler
cd src/adblock-compiler-core && deno cache src/mod.ts

# .NET projects
cd ../rules-compiler-dotnet && dotnet restore RulesCompiler.slnx
cd ../adguard-api-dotnet && dotnet restore src/AdGuard.ApiClient.sln

# Python compiler
cd ../rules-compiler-python && pip install -e ".[dev]"

# Rust workspace (builds all Rust projects)
cd .. && cargo build --release
```

> **Rust Workspace**: All Rust projects (rules-validator, adguard-api-rust, rules-compiler-rust) are now unified in a single workspace at the repository root. Run `cargo build` from the root to build all Rust projects together. See [RUST_WORKSPACE.md](RUST_WORKSPACE.md) for more details.

### Build All Projects

Root-level build scripts are available to build all projects or specific language ecosystems:

```bash
# Build all projects (debug mode - default)
./build.sh

# Build all projects in release mode
./build.sh --release

# Build specific language ecosystems
./build.sh --rust              # Build all Rust projects
./build.sh --dotnet            # Build all .NET projects
./build.sh --typescript        # Build all TypeScript/Deno projects
./build.sh --python            # Build Python projects

# Combine options
./build.sh --rust --dotnet --release   # Build Rust and .NET in release mode
```

**PowerShell (Windows/Cross-platform)**:

```powershell
# Build all projects (debug mode - default)
.\build.ps1

# Build all projects in release mode
.\build.ps1 -Profile release

# Build specific language ecosystems
.\build.ps1 -Rust              # Build all Rust projects
.\build.ps1 -DotNet            # Build all .NET projects
.\build.ps1 -TypeScript        # Build all TypeScript/Deno projects
.\build.ps1 -Python            # Build Python projects

# Combine options
.\build.ps1 -Rust -DotNet -Profile release
```

**Available Options**:
- `--all` / `-All`: Build all projects (default if no specific project selected)
- `--rust` / `-Rust`: Build Rust workspace (validation library, API clients, compilers)
- `--dotnet` / `-DotNet`: Build .NET solutions (API client, rules compiler)
- `--typescript` / `-TypeScript`: Build TypeScript/Deno projects (requires Deno)
- `--python` / `-Python`: Build Python projects (requires Python 3.9+)
- `--debug`: Use debug profile (default)
- `--release` / `-Profile release`: Use release/optimized profile

The build scripts automatically:
- Check for required tools (Rust, .NET, Deno, Python)
- Restore dependencies
- Build projects with appropriate configuration
- Report build status with colored output
- Exit with appropriate status codes for CI integration

**Testing the Build Scripts**:

Comprehensive test suites are available to validate build script functionality:

```bash
# Run Bash script tests (25+ unit and integration tests)
./tools/test-build-scripts.sh

# Run PowerShell script tests
pwsh -File tools/test-build-scripts.ps1
```

The test suites include:
- **Unit tests**: Help output, argument parsing, error handling
- **Integration tests**: Rust, .NET, TypeScript, Python builds
- **Combined tests**: Multiple language ecosystems together
- **Profile tests**: Debug and release build configurations

Tests run automatically in CI via the **Build Scripts Tests** workflow.

### Compile Filter Rules (Any Language)

```bash
# TypeScript
cd src/adblock-compiler-core && deno task compile

# .NET
cd src/rules-compiler-dotnet && dotnet run --project src/RulesCompiler.Console

# Python
cd src/rules-compiler-python && rules-compiler

# Rust
cd src/rules-compiler-rust && cargo run --release

# Or from repository root (using workspace)
cargo run --release -p rules-compiler

# PowerShell
Import-Module ./src/adguard-api-powershell/Invoke-RulesCompiler.psm1
Invoke-RulesCompiler

# Bash
./src/rules-compiler-shell/bash/compile-rules.sh
```

## Docker Development Environment

A pre-configured Docker environment is available with all dependencies installed.

### Dockerfile.warp

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble

# Includes:
# - .NET 10 SDK
# - Deno 2.0+
# - PowerShell 7
# - Git

WORKSPACE /workspace
```

### Build and Run

```bash
# Build the image
docker build -f Dockerfile.warp -t ad-blocking-dev .

# Run interactive container
docker run -it -v $(pwd):/workspace ad-blocking-dev

# Inside container, cache Deno dependencies
cd /workspace/src/adblock-compiler-core && deno cache src/mod.ts
cd /workspace/src/rules-compiler-dotnet && dotnet restore RulesCompiler.slnx
```

### Warp Environment

For Warp terminal users, a pre-built environment is available:

| Property | Value |
|----------|-------|
| Docker Image | `jaysonknight/warp-env:ad-blocking` |
| Environment ID | `Egji4sZU4TNIOwNasFU73A` |

```bash
# Create Warp integrations
warp integration create slack --environment Egji4sZU4TNIOwNasFU73A
warp integration create linear --environment Egji4sZU4TNIOwNasFU73A
```

## Data Directory Structure

The `data/` directory organizes all filter-related files with a clear separation between inputs and outputs:

### Input Directory (`data/input/`)

Source location for filter rules to be compiled:

- **Local rule files**: Place custom filter lists in adblock or hosts format
  - Examples: `custom-rules.txt`, `company-blocklist.txt`
  - Supports `.txt`, `.hosts` extensions
  - Automatic format detection (adblock vs hosts)

- **Internet source references**: File containing URLs to remote filter lists
  - Create `internet-sources.txt` with one URL per line
  - Example sources: EasyList, StevenBlack hosts, AdGuard filters
  - Lines starting with `#` are comments
  - **Security**: Only HTTPS URLs allowed, content validated before use

**Features:**
- ✅ **Hash verification**: SHA-384 integrity checking for tampering detection
- ✅ **Syntax validation**: Automatic linting before compilation
- ✅ **Multi-format support**: Both adblock and hosts file formats
- ✅ **Remote list fetching**: Download and verify internet sources
- ✅ **Error reporting**: Clear messages with line numbers for invalid rules
- ✅ **URL security**: HTTPS enforcement, domain validation, content verification

**Example structure:**
```
data/input/
├── README.md                    # Documentation
├── custom-rules.txt             # Your custom adblock rules
├── internet-sources.txt         # URLs to remote lists
└── .gitignore                   # Ignore large/sensitive files
```

See [`data/input/README.md`](data/input/README.md) for detailed usage instructions.

### Output Directory (`data/output/`)

Contains the final compiled filter list:

- **`adguard_user_filter.txt`**: Main filter list in **adblock format**
  - Merged from all input sources
  - Deduplicated and validated
  - Ready for use with AdGuard DNS or other blockers
  - Tracked in version control

**Compilation guarantees:**
- ✅ Output is always in adblock syntax (not hosts format)
- ✅ Comments and metadata preserved from sources
- ✅ SHA-384 hash computed for verification
- ✅ Rule count validation

### Archive Directory (`data/archive/`)

Stores processed input files after successful compilation for audit and rollback purposes:

- **Automatic archiving**: Configurable via environment variables or CLI flags
- **Timestamp-based organization**: Each compilation creates a dated subdirectory
- **Manifest tracking**: JSON metadata with hashes, file info, and compilation stats
- **Retention policy**: Automatic cleanup of archives older than 90 days (configurable)

**Archiving modes:**
- 🤖 **Automatic** (default): Archive after every successful compilation
- 🤔 **Interactive**: Prompt user whether to archive
- 🚫 **Disabled**: No archiving

**Example structure:**
```
data/archive/
├── 2024-12-27_14-30-45/
│   ├── manifest.json              # Compilation metadata
│   ├── custom-rules.txt           # Input file snapshot
│   └── internet-sources.txt
└── 2024-12-26_09-15-22/
    ├── manifest.json
    └── custom-rules.txt
```

**Configuration:**
```bash
# Environment variables
export ADGUARD_ARCHIVE_ENABLED=true
export ADGUARD_ARCHIVE_MODE=automatic  # or interactive, disabled
export ADGUARD_ARCHIVE_RETENTION_DAYS=90

# CLI flags (all compilers)
npm run compile -- --no-archive              # Disable
npm run compile -- --archive-interactive     # Prompt
npm run compile -- --archive-retention 365   # Custom retention

# Or configure in JSON/YAML/TOML config files
```

**Config file example (JSON):**
```json
{
  "name": "My Filter",
  "output": {
    "path": "data/output/my-filter.txt",
    "conflictStrategy": "rename"
  },
  "archiving": {
    "enabled": true,
    "mode": "automatic",
    "retentionDays": 90
  },
  "sources": [...]
}
```

**Use cases:**
- Track historical changes to filter rules
- Rollback to previous working configuration
- Audit what was compiled and when
- Meet compliance requirements for data retention

See [`data/archive/README.md`](data/archive/README.md) for detailed usage and restoration procedures.

### Compilation Workflow

```
┌─────────────────────────────────────────────────────┐
│ 1. Discover all files in data/input/               │
│    - Scan for .txt, .hosts files                   │
│    - Parse internet-sources.txt                    │
└─────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────┐
│ 2. Validate & Hash Check                           │
│    - Syntax validation for each file               │
│    - Compute SHA-384 hashes                        │
│    - Detect tampering/modifications                │
└─────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────┐
│ 3. Fetch Internet Sources (if configured)          │
│    - Download remote lists                         │
│    - Verify with hashes                            │
│    - Cache for performance                         │
└─────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────┐
│ 4. Compile with @bloqr/compiler-core         │
│    - Merge all sources                             │
│    - Apply transformations (dedupe, validate, etc) │
│    - Convert hosts format to adblock if needed     │
└─────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────┐
│ 5. Output to data/output/adguard_user_filter.txt   │
│    - Write final adblock-format list               │
│    - Compute output hash                           │
│    - Log statistics (rule count, hash)             │
└─────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────┐
│ 6. Archive Input Files (optional)                  │
│    - Create timestamped archive directory          │
│    - Copy all input files to archive               │
│    - Generate manifest.json with metadata          │
│    - Cleanup old archives per retention policy     │
└─────────────────────────────────────────────────────┘
```

## Rules Compilers

All compilers dogfood [`@bloqr/compiler-core`](https://jsr.io/@bloqr/compiler-core) (`src/adblock-compiler-core/`, this repo) and support:

- **JSON config** (documented format; YAML/TOML remain supported by the underlying readers, see [Configuration Reference](docs/configuration-reference.md))
- **All 11 transformations**: Deduplicate, Validate, RemoveComments, Compress, etc.
- **Source-specific settings**: Per-source transformations, inclusions, exclusions
- **Pattern matching**: Wildcards, regex, file-based patterns
- **SOLID Architecture**: Dependency injection, single responsibility, better testing

📘 **[Complete @bloqr/compiler-core Guide](docs/guides/adblock-compiler-guide.md)** - Why it's better, CI/CD integration, API reference, migration guide
📘 **[adblock-compiler-core README](src/adblock-compiler-core/README.md)** - Package architecture, how it relates to the commercial `@bloqr/compiler`

### TypeScript Compiler

**Location**: `src/adblock-compiler-core/`

```bash
cd src/adblock-compiler-core

# Compile rules
deno task compile                   # Default config
deno task compile:yaml              # YAML config
deno task compile:toml              # TOML config

# CLI options
deno task compile -- -c config.yaml # Specific config
deno task compile -- -r             # Copy to data/
deno task compile -- -d             # Debug output
deno task compile -- --help         # Show help
deno task compile -- --version      # Show version

# Interactive console mode
deno task interactive               # Interactive menu

# Development
deno task dev                       # Run with watch mode
deno task lint                      # Deno lint
deno task test                      # Deno tests
deno task test:coverage             # With coverage
```

**Features**:
- Deno 2.0+ runtime with npm compatibility and secure-by-default permissions
- Built-in TypeScript support, no build step required
- Interactive console mode with menu-driven interface
- Dual-mode operation: CLI and interactive modes
- Library API export via `@bloqr/compiler-core/lib`
- Deno native testing and linting with coverage

### .NET Compiler

**Location**: `src/rules-compiler-dotnet/`

```bash
cd src/rules-compiler-dotnet

# Build
dotnet restore RulesCompiler.slnx
dotnet build RulesCompiler.slnx

# Interactive mode (menu-driven)
dotnet run --project src/RulesCompiler.Console

# CLI mode
dotnet run --project src/RulesCompiler.Console -- --config config.yaml
dotnet run --project src/RulesCompiler.Console -- -c config.json --copy
dotnet run --project src/RulesCompiler.Console -- -c config.yaml --verbose
dotnet run --project src/RulesCompiler.Console -- -c config.yaml --validate
dotnet run --project src/RulesCompiler.Console -- --version

# Tests
dotnet test RulesCompiler.slnx
```

**CLI Options**:

| Option | Short | Description |
|--------|-------|-------------|
| `--config` | `-c` | Configuration file path |
| `--output` | `-o` | Output file path |
| `--copy` | | Copy to rules directory |
| `--verbose` | | Detailed compiler output |
| `--validate` | | Validate config only |
| `--version` | `-v` | Show version info |

**Features**:
- Interactive Spectre.Console menu
- Configuration validation with error/warning reporting
- Dependency injection support
- Cross-platform (Windows, Linux, macOS)
- Shells out to `@bloqr/compiler-core` via `deno run jsr:@bloqr/compiler-core/cli` (requires Deno)

**Library architecture**: split into three assemblies — `Bloqr.Compiler.Abstractions` (interfaces, event-args, model/DTO types), `Bloqr.Compiler.Core` (config reading/validation, chunking, file-locking, plugin management, compilation pipeline), and `RulesCompiler` (compiler-specific services referencing both). See [.NET Compiler README](src/rules-compiler-dotnet/README.md) for library usage and the full architecture breakdown.

### Python Compiler

**Location**: `src/rules-compiler-python/`

```bash
cd src/rules-compiler-python

# Install
pip install -e .                    # Basic install
pip install -e ".[dev]"             # With dev dependencies

# CLI usage
rules-compiler                      # Default config
rules-compiler -c config.yaml       # Specific config
rules-compiler -c config.json -r    # Compile and copy
rules-compiler -o output.txt        # Custom output
rules-compiler -V                   # Version info
rules-compiler -d                   # Debug output
rules-compiler --help               # Show help

# Tests
pytest                              # Run tests
pytest -v                           # Verbose
pytest --cov=rules_compiler         # Coverage
```

**Features**:
- Python 3.9-3.12 support
- Type hints with mypy checking
- Ruff linting
- PyPI-ready packaging

**Python API**:

```python
from rules_compiler import RulesCompiler, compile_rules

# Simple usage
result = compile_rules("config.yaml")
print(f"Compiled {result.rule_count} rules")

# Class-based usage
compiler = RulesCompiler()
result = compiler.compile("config.yaml", output_path="output.txt")
```

### Rust Compiler

**Location**: `src/rules-compiler-rust/` (part of root workspace)

> **Rust Workspace**: This compiler is part of a unified Rust workspace. All Rust projects can be built together from the repository root. See [RUST_WORKSPACE.md](RUST_WORKSPACE.md) for details.

```bash
# From repository root (recommended)
cargo build --release -p rules-compiler
cargo run --release -p rules-compiler -- -c config.yaml

# Or from project directory
cd src/rules-compiler-rust
cargo build --release
cargo run --release -- -c config.yaml

# CLI usage
./target/release/rules-compiler -c config.yaml   # Specific config
./target/release/rules-compiler -c config.json -r # Compile and copy
./target/release/rules-compiler -o output.txt     # Custom output
./target/release/rules-compiler -V                # Version info
./target/release/rules-compiler -d                # Debug output
./target/release/rules-compiler --help            # Show help

# Tests
cargo test -p rules-compiler                      # Run tests
cargo test -p rules-compiler -- --nocapture       # With output
```

**Features**:
- Single statically-linked binary
- LTO optimization for small binary size
- Zero runtime dependencies (except Deno, required to run the `@bloqr/compiler-core` engine it shells out to)
- Cross-platform support
- Part of unified workspace with shared dependencies

**Rust API**:

```rust
use rules_compiler::{RulesCompiler, CompilerConfiguration};

let compiler = RulesCompiler::new();
let result = compiler.compile("config.yaml", None)?;
println!("Compiled {} rules", result.rule_count);
```

### Shell Scripts

**Location**: `src/rules-compiler-shell/`

Cross-platform shell scripts that use `@bloqr/compiler-core` for simple automation and CI/CD pipelines.

| Script | Platform | Shell | Features |
|--------|----------|-------|----------|
| `compile-rules.sh` | Linux, macOS | Bash | Full feature support, YAML/TOML via yq/Python |
| `compile-rules.zsh` | Linux, macOS | Zsh | Native zsh features (zparseopts, EPOCHREALTIME) |

#### Bash (Linux/macOS)

```bash
# Make executable (first time)
chmod +x src/rules-compiler-shell/bash/compile-rules.sh

# Run with defaults
./src/rules-compiler-shell/bash/compile-rules.sh

# Use specific configuration
./src/rules-compiler-shell/bash/compile-rules.sh -c config.yaml

# Compile and copy to rules directory
./src/rules-compiler-shell/bash/compile-rules.sh -c config.yaml -r

# Show version/help
./src/rules-compiler-shell/bash/compile-rules.sh -v
./src/rules-compiler-shell/bash/compile-rules.sh -h
```

#### Zsh (macOS/Linux)

```zsh
# Make executable (first time)
chmod +x src/rules-compiler-shell/zsh/compile-rules.zsh

# Run with defaults
./src/rules-compiler-shell/zsh/compile-rules.zsh

# Use YAML configuration
./src/rules-compiler-shell/zsh/compile-rules.zsh -c config.yaml

# Compile and copy to rules directory
./src/rules-compiler-shell/zsh/compile-rules.zsh -c config.yaml -r

# Debug mode
./src/rules-compiler-shell/zsh/compile-rules.zsh -c config.yaml -d
```

**CLI Options** (all scripts):

| Option | Short | Description |
|--------|-------|-------------|
| `--config PATH` | `-c` | Path to configuration file |
| `--output PATH` | `-o` | Path to output file |
| `--copy-to-rules` | `-r` | Copy output to rules directory |
| `--format FORMAT` | `-f` | Force format (json, yaml, toml) |
| `--version` | `-v` | Show version information |
| `--help` | `-h` | Show help message |
| `--debug` | `-d` | Enable debug output |

See [Shell Scripts README](src/rules-compiler-shell/README.md) for detailed documentation.

### PowerShell Modules

Two PowerShell module implementations are provided:

#### Modern Canonical Modules (`src/rules-compiler-powershell/`)

Class-based, actively-developed PowerShell 7+ modules following best practices:

- **Common** - Shared `CompilerLogger` and `CompilerResult` classes
- **RulesCompiler** - Rules compilation with class-based configuration
- **AdGuardWebhook** - Webhook invocation with statistics tracking

```powershell
# Import modules
Import-Module ./src/rules-compiler-powershell/Common/Common.psd1
Import-Module ./src/rules-compiler-powershell/RulesCompiler/RulesCompiler.psd1
Import-Module ./src/rules-compiler-powershell/AdGuardWebhook/AdGuardWebhook.psd1

# Run Pester tests
Invoke-Pester -Path ./src/rules-compiler-powershell -Recurse

# Lint with PSScriptAnalyzer
Invoke-ScriptAnalyzer -Path src/rules-compiler-powershell -Recurse
```

#### Legacy Compatibility Modules (`src/adguard-api-powershell/`)

Maintained for backward compatibility with existing scripts.

**RulesCompiler Module**:
```powershell
Import-Module ./src/adguard-api-powershell/Invoke-RulesCompiler.psm1
Invoke-RulesCompiler                      # Compile rules
Invoke-RulesCompiler -CopyToRules         # Compile and copy
Get-CompilerVersion | Format-List         # Version info
./src/adguard-api-powershell/RulesCompiler-Harness.ps1  # Interactive harness
```

**Webhook Module** (v1.0.0):
```powershell
Import-Module ./src/adguard-api-powershell/Invoke-WebHook.psm1
Invoke-AdGuardWebhook -WebhookUrl $url
Invoke-AdGuardWebhook -WebhookUrl $url -Continuous -ShowStatistics
Invoke-AdGuardWebhook -ConfigFile config.json -SaveConfig config.json
```

**Features**:
- ✅ Rich console output with colored output
- 📊 Progress bars for continuous operations
- 📊 Statistics tracking (success/failure rates, elapsed time)
- 💾 Configuration file support (JSON/YAML)
- 💾 Multiple output formats (Table, List, JSON)
- ⚙️ Quiet mode for scripting
- 🎯 Parameter validation with ranges
- 🔙 Backward compatible alias (`Invoke-Webhook`)

See individual module READMEs in `src/rules-compiler-powershell/` and `src/adguard-api-powershell/` for complete documentation.

## AdGuard API Clients

Complete SDK implementations for the [AdGuard DNS API v1.15](https://api.adguard-dns.io/static/swagger/swagger.json) in C#, TypeScript, and Rust.

### C# SDK

**Location**: `src/adguard-api-dotnet/`

```bash
cd src/adguard-api-dotnet

# Build
dotnet restore src/AdGuard.ApiClient.sln
dotnet build src/AdGuard.ApiClient.sln

# Test
dotnet test src/AdGuard.ApiClient.sln

# Run benchmarks
dotnet run --project src/AdGuard.ApiClient.Benchmarks -c Release
```

**Features**:
- Auto-generated from OpenAPI specification
- Full async/await support with cancellation tokens
- Polly resilience policies (automatic retry on 408/429/5xx with exponential backoff)
- Dependency injection with `ILogger` support
- Fluent configuration helpers for easy setup
- Newtonsoft.Json serialization with JsonSubTypes support

**Usage Example**:

```csharp
using AdGuard.ApiClient;
using AdGuard.ApiClient.Helpers;

// Configure client with fluent API
var config = new Configuration()
    .WithApiKey("your-api-key")
    .WithTimeout(TimeSpan.FromSeconds(30))
    .WithUserAgent("MyApp/1.0");

var apiClient = new ApiClient(config);
var devicesApi = new DevicesApi(apiClient);

// List all devices
var devices = await devicesApi.ListDevicesAsync();
foreach (var device in devices)
{
    Console.WriteLine($"{device.Name}: {device.Id}");
}

// Get account limits
var accountApi = new AccountApi(apiClient);
var limits = await accountApi.GetAccountLimitsAsync();
Console.WriteLine($"Devices: {limits.DevicesCount}/{limits.DevicesLimit}");
```

### Rust SDK

**Location**: `src/adguard-api-rust/` (part of root workspace)

> **Rust Workspace**: This SDK is part of a unified Rust workspace. All Rust projects can be built together from the repository root. See [RUST_WORKSPACE.md](RUST_WORKSPACE.md) for details.

```bash
# From repository root (recommended)
cargo build --release -p adguard-api-lib
cargo build --release -p adguard-api-cli
cargo run --release -p adguard-api-cli

# Or from project directory
cd src/adguard-api-rust
cargo build --release
cargo test
```

**Features**:
- Auto-generated from OpenAPI specification using OpenAPI Generator
- Async/await support with Tokio runtime
- Single statically-linked binary distribution
- Configurable TLS: rustls (default) or native-tls
- Memory-safe with zero-cost abstractions
- Part of unified workspace with shared dependencies

**Usage Example**:

```rust
use adguard_api_lib::apis::configuration::Configuration;
use adguard_api_lib::apis::devices_api;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Configure the API client
    let mut config = Configuration::new();
    config.base_path = "https://api.adguard-dns.io".to_string();
    config.bearer_access_token = Some("your-api-token".to_string());

    // List devices
    let devices = devices_api::list_devices(&config).await?;
    for device in devices {
        println!("{}: {}", device.name, device.id);
    }

    Ok(())
}
```

### TypeScript SDK

**Location**: `src/adguard-api-typescript/`

```bash
cd src/adguard-api-typescript

# Run tests
deno task test

# Run CLI
deno task start
```

**Features**:
- Auto-generated types from OpenAPI specification
- Repository pattern with high-level abstractions
- Automatic retry with exponential backoff using axios-retry
- Interactive CLI with inquirer prompts
- Deno 2.0+ runtime with npm compatibility and secure-by-default permissions
- Library API export via `@adguard/api-typescript/lib`
- Full test coverage with Deno test

**Usage Example**:

```typescript
import { AdGuardDnsClient } from './src/index.ts';

// Configure client
const client = AdGuardDnsClient.withApiKey('your-api-key');

// Or from environment variable
const client = AdGuardDnsClient.fromEnv('ADGUARD_API_KEY');

// List all devices
const devices = await client.devices.listDevices();
for (const device of devices) {
    console.log(`${device.name}: ${device.id}`);
}

// Get account limits
const limits = await client.account.getAccountLimits();
console.log(`Devices: ${limits.devices_count}/${limits.devices_limit}`);

// Use repositories for higher-level operations
const stats = await client.statisticsRepository.getSummary();
```

### API Coverage (All SDKs)

All three SDK implementations (C#, TypeScript, Rust) provide complete coverage of:

| API | Description |
|-----|-------------|
| **Account** | Account limits and information |
| **Authentication** | OAuth token generation |
| **Devices** | Device CRUD operations |
| **DNS Servers** | DNS server profile management |
| **Dedicated IPs** | Dedicated IPv4 address management |
| **Filter Lists** | Filter list retrieval and management |
| **Query Logs** | Query log operations and filtering |
| **Statistics** | DNS query statistics (24h, 7d, 30d) |
| **Web Services** | Web services for content blocking |

See [API Client Usage Guide](docs/guides/api-client-usage.md) for detailed examples across all SDKs.

## Validation & Tools

### Rules Validator

**Location**: `src/rules-validator/` (part of root Rust workspace)

Rust library and CLI for validating filter and configuration files.

- **rules-validator-core** - Core validation logic library, with a real `extern "C"` FFI surface (opaque handle + JSON-string boundary, `catch_unwind`-guarded) and a generated `cbindgen` header for embedding in .NET/Dashboard via P/Invoke
- **rules-validator-cli** - Command-line validation tool

```bash
# Build and run validation
cargo build --release -p rules-validator-cli
./target/release/rules-validate --help
```

### Linear Import Tool

**Location**: `src/linear/`

TypeScript tool for importing documentation into Linear project management using Deno 2.0+.

```bash
cd src/linear
deno task import              # Run import
deno task import:docs         # Import documentation
deno task import:dry-run      # Preview import
```

## Console Applications

Interactive terminal applications for managing AdGuard DNS.

### .NET Console UI

**Location**: `src/adguard-api-dotnet/src/AdGuard.ConsoleUI/`

```bash
cd src/adguard-api-dotnet
dotnet run --project src/AdGuard.ConsoleUI
```

**Features**:
- Menu-driven Spectre.Console interface with rich formatting
- Device and DNS server management with detailed views
- Query statistics and log viewing with filtering
- Filter list browsing and management
- Account limits with visual progress bars
- API key configuration (environment variable or interactive prompt)

### Rust CLI

**Location**: `src/adguard-api-rust/adguard-api-cli/`

```bash
cd src/adguard-api-rust

# Run directly
cargo run --bin adguard-api-cli

# Or build and run release binary
cargo build --release
./target/release/adguard-api-cli
```

**Features**:
- Interactive menu-driven interface using dialoguer
- Full feature parity with .NET Console UI
- TOML configuration file persistence (`~/.config/adguard-api-cli/config.toml`)
- Single binary distribution with no runtime dependencies
- Cross-platform (Linux, macOS, Windows)

**Configuration**:

```toml
# ~/.config/adguard-api-cli/config.toml
api_url = "https://api.adguard-dns.io"
api_token = "your-api-token-here"
```

Or use environment variables (both .NET-compatible and legacy formats supported by Rust CLI):
```bash
# Recommended cross-platform format
export ADGUARD_API_BASE_URL="https://api.adguard-dns.io"
export ADGUARD_API_KEY="your-api-key-here"

# Alternative: .NET hierarchical format
export ADGUARD_AdGuard__BaseUrl="https://api.adguard-dns.io"
export ADGUARD_AdGuard__ApiKey="your-api-key-here"

# Deprecated (backward compatibility only)
# export ADGUARD_API_URL="https://api.adguard-dns.io"
# export ADGUARD_API_TOKEN="your-token-here"
```

**Menu Options** (both applications):

| Menu | Description |
|------|-------------|
| Account Info | View account limits and usage statistics |
| Devices | List and view device details |
| DNS Servers | List and view DNS server configurations |
| User Rules | View and manage user rules |
| Query Log | View recent queries with time range filters |
| Statistics | View query statistics (24h, 7d, 30d) |
| Filter Lists | Browse available filter lists |
| Web Services | List blockable web services |
| Dedicated IPs | List and allocate dedicated IPv4 addresses |
| Settings | Configure API key, test connection |

**Environment Variables**:

Both applications support standardized environment variable names for cross-compatibility:

| Variable | Description |
|----------|-------------|
| `ADGUARD_API_KEY` | API credential (recommended cross-platform format) |
| `ADGUARD_API_BASE_URL` | API base URL (optional, cross-platform format) |
| `ADGUARD_AdGuard__ApiKey` | API credential (.NET hierarchical format) |
| `ADGUARD_AdGuard__BaseUrl` | API base URL (.NET hierarchical format) |

**Deprecated (backward compatibility)**:
- `ADGUARD_API_TOKEN` - Use `ADGUARD_API_KEY` instead
- `ADGUARD_API_URL` - Use `ADGUARD_API_BASE_URL` instead

**C# Console UI Configuration Example**:
```bash
# Linux/macOS
export ADGUARD_AdGuard__ApiKey="your-api-key-here"

# Windows PowerShell
$env:ADGUARD_AdGuard__ApiKey="your-api-key-here"
```

### TypeScript CLI

**Location**: `src/adguard-api-typescript/`

```bash
cd src/adguard-api-typescript

# Run interactively
deno task start

# Or with API key
deno task start -- --api-key your-key

# Sync rules from file
deno task start -- sync --file data/output/adguard_user_filter.txt
```

**Features**:
- Interactive menu-driven interface using inquirer and ora
- Full feature parity with .NET Console UI
- Repository pattern with high-level abstractions
- Automatic retry with exponential backoff
- TypeScript types from OpenAPI specification
- Deno's secure-by-default permission model

**Configuration**:

Set environment variable:
```bash
# Linux/macOS
export ADGUARD_API_KEY="your-api-key-here"

# Windows PowerShell
$env:ADGUARD_API_KEY="your-api-key-here"
```

## Configuration

All compilers validate against the same JSON Schema ([`schemas/compiler-config.schema.json`](schemas/compiler-config.schema.json)). JSON is the documented, recommended format; see **[Configuration Reference](docs/configuration-reference.md)** for the full property reference, all available transformations, pattern matching, and example configs.

Quick example:

```json
{
  "name": "My Filter List",
  "sources": [
    { "name": "EasyList", "source": "https://easylist.to/easylist/easylist.txt" }
  ],
  "transformations": ["Deduplicate", "InsertFinalNewLine"]
}
```

## Testing

### TypeScript (Deno)

```bash
cd src/adblock-compiler-core
deno task test                      # Run all tests
deno test src/cli.test.ts           # Specific file
deno task test:coverage             # With coverage
```

### .NET (xUnit)

```bash
# Rules Compiler
cd src/rules-compiler-dotnet
dotnet test RulesCompiler.slnx
dotnet test --filter "FullyQualifiedName~ConfigurationValidatorTests"
dotnet test --filter "FullyQualifiedName~TransformationTests"

# API Client
cd ../adguard-api-dotnet
dotnet test src/AdGuard.ApiClient.sln
dotnet test --filter "FullyQualifiedName~DevicesApiTests"
dotnet test --filter "Name~GetAccountLimits"
```

### Python (pytest)

```bash
cd src/rules-compiler-python
pytest                              # All tests
pytest -v                           # Verbose
pytest tests/test_config.py         # Specific file
pytest -k "test_read_yaml"          # By name
pytest --cov=rules_compiler         # Coverage
```

### Rust (cargo test)

> **Rust Workspace**: All Rust projects are now unified in a single workspace. Tests can be run from the repository root or individual project directories.

```bash
# From repository root (recommended) - runs all Rust tests
cargo test --workspace              # All tests in workspace
cargo test --workspace -- --nocapture  # With output

# Test specific packages
cargo test -p rules-compiler        # Rules compiler only
cargo test -p adguard-api-lib       # API library only
cargo test -p adguard-api-cli       # API CLI only
cargo test -p rules-validator-core  # Validation core only

# From individual project directories
cd src/rules-compiler-rust
cargo test                          # All tests
cargo test -- --nocapture           # With output
cargo test test_count_rules         # Specific test
cargo test config::                 # Module tests

cd ../adguard-api-rust
cargo test                          # All workspace tests
```

### PowerShell (Pester)

```powershell
# Run all tests
Invoke-Pester -Path ./src/adguard-api-powershell/Tests/

# Run with detailed output
Invoke-Pester -Path ./src/adguard-api-powershell/Tests/ -Output Detailed

# Lint with PSScriptAnalyzer
Invoke-ScriptAnalyzer -Path src/adguard-api-powershell -Recurse
```

### All Tests Summary

| Component | Framework | Command |
|-----------|-----------|---------|
| TypeScript Compiler | Deno test | `deno task test` |
| TypeScript API Client | Deno test | `deno task test` |
| .NET Compiler | xUnit | `dotnet test RulesCompiler.slnx` |
| .NET API Client | xUnit | `dotnet test src/AdGuard.ApiClient.sln` |
| Python Compiler | pytest | `pytest` |
| Rust Compiler | cargo test | `cargo test` |
| Rust API Client | cargo test | `cargo test` |
| PowerShell Module | Pester | `Invoke-Pester` |

## CI/CD

GitHub Actions workflows:

| Workflow | Description |
|----------|-------------|
| `dotnet.yml` | Build and test .NET projects with .NET 10 |
| `typescript.yml` | TypeScript build, lint, and test |
| `powershell.yml` | PSScriptAnalyzer linting |
| `release.yml` | Build and publish binaries on version tags |
| `publish-jsr.yml` | Publish `adblock-compiler-core` to `@bloqr/compiler-core` on JSR when the package changes |
| `codeql.yml` | CodeQL security scanning |
| `devskim.yml` | DevSkim security analysis |
| `claude.yml` | Claude AI integration |
| `claude-code-review.yml` | Automated code review |

### Releases

The repository can build and publish binaries for distribution. See [Release Guide](docs/release-guide.md) for build and distribution details.

Available binaries/packages:
- **AdGuard.ConsoleUI** (.NET Console application)
- **RulesCompiler.Console** (.NET rules compiler CLI)
- **rules-compiler** (Rust binary - single statically-linked executable)
- **rules-compiler** (Python wheel - pip-installable package)

## Documentation

### 📚 Documentation Resources

The repository includes comprehensive documentation:

- **[CLAUDE.md](CLAUDE.md)** - AI assistant development guidelines with complete project overview
- **[docs/](docs/)** - Getting started guides, configuration reference, Docker guide
- **[Compiler Comparison](docs/compiler-comparison.md)** - Choose the right compiler for your needs
- **[Configuration Reference](docs/configuration-reference.md)** - Complete schema documentation
- **[API Client Usage Guide](docs/guides/api-client-usage.md)** - SDK implementation examples
- **Per-project READMEs** - Detailed documentation in each src/ directory

### Getting Started

- [Getting Started Guide](docs/getting-started.md) - Quick installation and first compilation
- [Compiler Comparison](docs/compiler-comparison.md) - Choose the right compiler for your needs
- [Configuration Reference](docs/configuration-reference.md) - Complete configuration schema
- [Docker Guide](docs/docker-guide.md) - Development with Docker containers

### API Reference

- [C# API Client README](src/adguard-api-dotnet/README.md)
- [TypeScript API Client README](src/adguard-api-typescript/README.md)
- [Rust API Client README](src/adguard-api-rust/README.md)
- [API Client Usage Guide](docs/guides/api-client-usage.md)
- [API Client Examples](docs/guides/api-client-examples.md)
- [API Reference](docs/api/)
- [Console UI Architecture](docs/guides/consoleui-architecture.md)

### Rules Compilers

- **[@bloqr/compiler-core Guide](docs/guides/adblock-compiler-guide.md)** - Core package documentation with CI/CD examples
- [adblock-compiler-core README](src/adblock-compiler-core/README.md) - TypeScript/Deno compiler, JSR integration, and how `@bloqr/compiler-core` relates to Bloqr's commercial compiler
- [.NET Compiler README](src/rules-compiler-dotnet/README.md) - C# library and CLI, including the `Bloqr.Compiler.Abstractions`/`Bloqr.Compiler.Core` split
- [Python Compiler README](src/rules-compiler-python/README.md) - pip-installable package
- [Rust Compiler README](src/rules-compiler-rust/README.md) - Single binary distribution
- [Shell Scripts README](src/rules-compiler-shell/README.md) - Bash and Zsh wrappers
- [PowerShell Module](src/adguard-api-powershell/README.md) - Full-featured PowerShell API
- [ADR 0001: Canonical Rules Compilation Engine](docs/adr/0001-canonical-rules-compilation-engine.md) - Why `@bloqr/compiler-core` is an in-repo extraction, not a third-party or commercial dependency
- [Backporting Policy](docs/backporting-policy.md) - Criteria and process for porting fixes from the commercial `bloqr-compiler` into `adblock-compiler-core`

### Development

- [Claude Instructions](CLAUDE.md) - AI assistant development guidelines
- [Security Policy](SECURITY.md) - Vulnerability reporting
- [Release Guide](docs/release-guide.md) - Release process and binary publishing
- [Centralized Package Management](docs/centralized-package-management.md) - NuGet package management
- [Shared Deno Configuration](docs/DENO_CONFIG.md) - Deno configuration pattern and guidelines

### Test Your Ad Blocking

- [AdBlock Tester](https://adblock-tester.com/)
- [AdGuard Tester](https://d3ward.github.io/toolz/adblock.html)

## Environment Variables

### API Clients

Both C# and Rust implementations support standardized environment variable names:

| Variable | Description |
|----------|-------------|
| `ADGUARD_API_KEY` | AdGuard DNS API credential (recommended cross-platform format) |
| `ADGUARD_API_BASE_URL` | API base URL (optional, cross-platform format) |
| `ADGUARD_AdGuard__ApiKey` | AdGuard DNS API credential (.NET hierarchical format) |
| `ADGUARD_AdGuard__BaseUrl` | API base URL (.NET hierarchical format) |

**Deprecated (backward compatibility only)**:
- `ADGUARD_API_TOKEN` - Use `ADGUARD_API_KEY` instead
- `ADGUARD_API_URL` - Use `ADGUARD_API_BASE_URL` instead

**Cross-Compatibility**: All API clients (C#, TypeScript, Rust) support both `ADGUARD_API_KEY` (recommended) and the .NET hierarchical format `ADGUARD_AdGuard__ApiKey`.

**Note for .NET format**: The `ADGUARD_` prefix is required, and double underscore (`__`) represents colon (`:`) in configuration keys. Example: `ADGUARD_AdGuard__ApiKey` maps to `AdGuard:ApiKey` in configuration.

### Rules Compilers

| Variable | Application | Description |
|----------|-------------|-------------|
| `DEBUG` | All compilers | Enable debug logging |
| `RULESCOMPILER_config` | .NET compiler | Default config file path |
| `RULESCOMPILER_Logging__LogLevel__Default` | .NET compiler | Log level (Debug, Information, Warning, Error) |

### Linear Integration

| Variable | Description |
|----------|-------------|
| `ADGUARD_LINEAR_API_KEY` | Linear integration scripts (recommended) |
| `LINEAR_API_KEY` | Legacy format (deprecated, use ADGUARD_LINEAR_API_KEY) |

## Contributing

Please see [SECURITY.md](SECURITY.md) for security policy and vulnerability reporting.

## License

See [LICENSE](LICENSE) for details.


