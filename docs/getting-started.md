# Getting Started

This guide will help you get up and running with the ad-blocking toolkit.

## Prerequisites

### Core Requirements

| Requirement | Version | Purpose | Installation |
|-------------|---------|---------|--------------|
| Deno | 2.0+ | TypeScript compilers and tools | [deno.land](https://deno.land/) |
| adblock-compiler | 1.0.0 | Filter compilation | `deno run jsr:@bloqr/compiler-core/cli` |

### Language-Specific Requirements

| Language | Requirement | Version | Installation |
|----------|-------------|---------|--------------|
| TypeScript | Deno | 2.0+ | [deno.land](https://deno.land/) |
| .NET | .NET SDK | 10.0+ | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Python | Python | 3.9+ | [python.org](https://www.python.org/) |
| Rust | Rust | 1.70+ | [rustup.rs](https://rustup.rs/) |
| PowerShell | PowerShell | 7+ | [GitHub](https://github.com/PowerShell/PowerShell) |

## Quick Installation

### 1. Clone the Repository

```bash
git clone https://github.com/BloqrAI/bloqr-core.git
cd bloqr-core
```

### 2. Install Deno

All TypeScript compilers use Deno as their runtime:

```bash
# macOS/Linux
curl -fsSL https://deno.land/install.sh | sh

# Windows (PowerShell)
irm https://deno.land/install.ps1 | iex
```

Verify installation:

```bash
deno --version
```

The `@bloqr/compiler-core` package is accessed via Deno's JSR integration.

### 3. Choose Your Compiler

Pick the compiler that best fits your workflow:

#### TypeScript (Deno)

```bash
cd src/compilers/typescript
deno task compile
```

#### .NET

```bash
cd src/compilers/dotnet
dotnet restore CompilerDotnet.slnx
dotnet build CompilerDotnet.slnx
dotnet run --project src/Bloqr.Compiler.Dotnet.Console
```

#### Python

```bash
cd src/compilers/python
pip install -e .
bloqr-compiler
```

#### Rust

```bash
cd src/compilers/rust
cargo build --release
./target/release/bloqr-compiler
```

#### PowerShell

```powershell
Import-Module ./src/compilers/powershell/BloqrCompiler/BloqrCompiler.psd1
Invoke-BloqrCompiler
```

## First Compilation

### 1. Create a Configuration File

Create `my-config.json`:

```json
{
  "name": "My Ad-Blocking Filter",
  "description": "Custom filter list for blocking ads and trackers",
  "sources": [
    {
      "name": "EasyList",
      "source": "https://easylist.to/easylist/easylist.txt",
      "type": "adblock",
      "transformations": ["Validate", "RemoveModifiers"]
    },
    {
      "name": "AdGuard Base",
      "source": "https://raw.githubusercontent.com/AdguardTeam/FiltersRegistry/master/filters/filter_2_Base/filter.txt",
      "type": "adblock"
    }
  ],
  "transformations": ["Deduplicate", "RemoveEmptyLines", "TrimLines", "InsertFinalNewLine"]
}
```

See [Configuration Reference](configuration-reference.md#supported-formats) for JSONC (commented JSON) support.

### 2. Compile Your Filter

```bash
# TypeScript (Deno)
deno task compile -- -c my-config.json -o my-filter.txt

# .NET
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- -c my-config.json -o my-filter.txt

# Python
bloqr-compiler -c my-config.json -o my-filter.txt

# Rust
cargo run -- -c my-config.json -o my-filter.txt
```

### 3. Review the Output

Your compiled filter list is now in `my-filter.txt`. You can:

- Upload it to AdGuard DNS as a custom filter
- Use it with other ad-blocking software
- Host it on a web server for subscription

## Using the AdGuard API Client

The AdGuard DNS API clients (.NET, TypeScript, Rust, PowerShell) moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) and are no longer part of this repo. See that repo's documentation for getting an API key and choosing a client.

## Using Docker

For a pre-configured development environment:

```bash
# Build the Docker image
docker build -f Dockerfile.warp -t ad-blocking-dev .

# Run interactively
docker run -it -v $(pwd):/workspace ad-blocking-dev

# Inside the container
cd /workspace/src/compilers/typescript
deno task compile
```

See [Docker Guide](docker-guide.md) for more details.

## Next Steps

### Rules Compilers

- [@bloqr/compiler-core Guide](guides/compiler-core-guide.md) - Complete TypeScript/Deno guide
- [Compiler Comparison](compiler-comparison.md) - Choose the right compiler for your needs
- [Configuration Reference](configuration-reference.md) - Learn all configuration options

### Additional Resources

- [Testing Guide](guides/testing-guide.md) - Test all components
- [Deployment Guide](guides/deployment-guide.md) - Docker, Kubernetes, CI/CD
- [Troubleshooting Guide](guides/troubleshooting-guide.md) - Common issues and solutions
- [Migration Guide](guides/migration-guide.md) - Migrate between implementations
- [Docker Guide](docker-guide.md) - Docker development environment

## Common Issues

### @bloqr/compiler-core not found

The .NET, Python, and Rust compilers all shell out to `@bloqr/compiler-core` via Deno. Make sure Deno is installed:

```bash
deno --version
```

You can run the compiler CLI directly with:

```bash
deno run --allow-read --allow-write --allow-env --allow-net --allow-run jsr:@bloqr/compiler-core/cli --version
```

### Permission denied on Linux/macOS

Make the root build/launcher scripts executable:

```bash
chmod +x build.sh launcher.sh
```

### Python package not found

Install in development mode:

```bash
cd src/compilers/python
pip install -e .
```

### Rust build fails

Update Rust to the latest stable:

```bash
rustup update stable
```

## Getting Help

- [GitHub Issues](https://github.com/BloqrAI/bloqr-core/issues)
- [Security Policy](../SECURITY.md)
