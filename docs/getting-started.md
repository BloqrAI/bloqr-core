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
git clone https://github.com/BloqrAI/bloqr-lists.git
cd bloqr-lists
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
cd src/adblock-compiler-core
deno task compile
```

#### .NET

```bash
cd src/rules-compiler-dotnet
dotnet restore RulesCompiler.slnx
dotnet build RulesCompiler.slnx
dotnet run --project src/RulesCompiler.Console
```

#### Python

```bash
cd src/rules-compiler-python
pip install -e .
rules-compiler
```

#### Rust

```bash
cd src/rules-compiler-rust
cargo build --release
./target/release/rules-compiler
```

#### PowerShell

```powershell
Import-Module ./src/adguard-api-powershell/Invoke-RulesCompiler.psm1
Invoke-RulesCompiler
```

#### Bash/Shell

```bash
./src/rules-compiler-shell/bash/compile-rules.sh
```

## First Compilation

### 1. Create a Configuration File

Create `my-config.yaml`:

```yaml
name: My Ad-Blocking Filter
description: Custom filter list for blocking ads and trackers

sources:
  - name: EasyList
    source: https://easylist.to/easylist/easylist.txt
    type: adblock
    transformations:
      - Validate
      - RemoveModifiers

  - name: AdGuard Base
    source: https://raw.githubusercontent.com/AdguardTeam/FiltersRegistry/master/filters/filter_2_Base/filter.txt
    type: adblock

transformations:
  - Deduplicate
  - RemoveEmptyLines
  - TrimLines
  - InsertFinalNewLine
```

### 2. Compile Your Filter

```bash
# TypeScript (Deno)
deno task compile -- -c my-config.yaml -o my-filter.txt

# .NET
dotnet run --project src/RulesCompiler.Console -- -c my-config.yaml -o my-filter.txt

# Python
rules-compiler -c my-config.yaml -o my-filter.txt

# Rust
cargo run -- -c my-config.yaml -o my-filter.txt
```

### 3. Review the Output

Your compiled filter list is now in `my-filter.txt`. You can:

- Upload it to AdGuard DNS as a custom filter
- Use it with other ad-blocking software
- Host it on a web server for subscription

## Using the AdGuard API Client

### 1. Get Your API Key

1. Log in to [AdGuard DNS](https://adguard-dns.io/)
2. Go to Account Settings
3. Generate an API key

### 2. Set Environment Variable

```bash
# Linux/macOS
export ADGUARD_AdGuard__ApiKey="your-api-key-here"

# Windows PowerShell
$env:ADGUARD_AdGuard__ApiKey="your-api-key-here"

# Windows CMD
set ADGUARD_AdGuard__ApiKey=your-api-key-here
```

### 3. Choose Your API Client

#### TypeScript API Client (Deno)

```bash
cd src/adguard-api-typescript

# Set API key
export ADGUARD_API_KEY="your-api-key-here"

# Run interactive CLI
deno task start
```

**Quick TypeScript Example:**

```typescript
import { AdGuardDnsClient } from './src/index.ts';

// Create client
const client = AdGuardDnsClient.fromEnv('ADGUARD_API_KEY');

// List devices
const devices = await client.devices.listDevices();
for (const device of devices) {
  console.log(`${device.name}: ${device.id}`);
}

// Get account limits
const limits = await client.account.getAccountLimits();
console.log(`Devices: ${limits.devices.used}/${limits.devices.limit}`);
```

#### .NET Console UI

```bash
cd src/adguard-api-dotnet
dotnet run --project src/AdGuard.ConsoleUI
```

#### Rust CLI

```bash
cd src/adguard-api-rust
cargo run --bin adguard-api-cli
```

All API clients provide:
- View account limits
- Manage devices
- View DNS servers
- Check query logs
- View statistics
- Manage user rules

## Using Docker

For a pre-configured development environment:

```bash
# Build the Docker image
docker build -f Dockerfile.warp -t ad-blocking-dev .

# Run interactively
docker run -it -v $(pwd):/workspace ad-blocking-dev

# Inside the container
cd /workspace/src/adblock-compiler-core
deno task compile
```

See [Docker Guide](docker-guide.md) for more details.

## Next Steps

### Rules Compilers

- [TypeScript Rules Compiler Guide](guides/typescript-rules-compiler.md) - Complete TypeScript/Deno guide
- [Compiler Comparison](compiler-comparison.md) - Choose the right compiler for your needs
- [Configuration Reference](configuration-reference.md) - Learn all configuration options

### API Clients

- [TypeScript API SDK Guide](guides/typescript-api-sdk.md) - Complete TypeScript API documentation
- [API Client Usage Guide](guides/api-client-usage.md) - .NET API client documentation
- [API Client Examples](guides/api-client-examples.md) - Code examples for all SDKs

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

Make shell scripts executable:

```bash
chmod +x src/rules-compiler-shell/bash/compile-rules.sh
```

### Python package not found

Install in development mode:

```bash
cd src/rules-compiler-python
pip install -e .
```

### Rust build fails

Update Rust to the latest stable:

```bash
rustup update stable
```

## Getting Help

- [GitHub Issues](https://github.com/BloqrAI/bloqr-lists/issues)
- [Security Policy](../SECURITY.md)
