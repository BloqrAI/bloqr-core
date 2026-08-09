# Documentation

This directory contains comprehensive documentation for the ad-blocking repository.

## Quick Links

| Document | Description |
|----------|-------------|
| [**Why Validation Matters**](WHY_VALIDATION_MATTERS.md) | 🔒 **Start Here** - Understand why security validation is essential |
| [Getting Started](getting-started.md) | Installation and first steps |
| [Configuration Reference](configuration-reference.md) | Complete configuration schema |
| [Docker Guide](docker-guide.md) | Docker development environment |
| [Compiler Comparison](compiler-comparison.md) | Compare TypeScript, .NET, Python, Rust compilers |
| [Release Guide](release-guide.md) | Creating releases with automatic binary builds |

## Security Documentation

| Document | Description |
|----------|-------------|
| [**Why Validation Matters**](WHY_VALIDATION_MATTERS.md) | User-friendly explanation of validation and security (everyone should read this!) |
| [Runtime Enforcement](RUNTIME_ENFORCEMENT.md) | Technical details on runtime validation enforcement |
| [Validation Enforcement](VALIDATION_ENFORCEMENT.md) | CI/CD enforcement mechanisms |
| [Security Policy](../SECURITY.md) | Security vulnerability reporting |

## Contents

### Guides

| Guide | Description |
|-------|-------------|
| [Getting Started](getting-started.md) | Installation, prerequisites, and quick start |
| [**@bloqr/compiler-core Guide**](guides/adblock-compiler-guide.md) | **Core compilation package with CI/CD examples** |
| [Docker Guide](docker-guide.md) | Using Docker for development |
| [Configuration Reference](configuration-reference.md) | Full configuration schema documentation |
| [Compiler Comparison](compiler-comparison.md) | Feature comparison of all compilers |
| [Release Guide](release-guide.md) | Creating releases with automatic binary builds |
| [API Client Usage Guide](guides/api-client-usage.md) | AdGuard DNS API client usage (C# examples; client now lives in [`bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients)) |
| [API Client Examples](guides/api-client-examples.md) | Code examples with helper classes (client now lives in [`bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients)) |
| [ConsoleUI Architecture](guides/consoleui-architecture.md) | Console UI design documentation (client now lives in [`bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients)) |

### AdGuard DNS API Reference

The AdGuard DNS API clients (and their generated API reference docs) moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) and are no longer part of this repo.

## Project Documentation

### Main README Files

| Project | Location |
|---------|----------|
| Repository Overview | [README.md](../README.md) |
| TypeScript Compiler (`@bloqr/compiler-core`) | [src/adblock-compiler-core/README.md](../src/adblock-compiler-core/README.md) |
| .NET Compiler | [src/rules-compiler-dotnet/README.md](../src/rules-compiler-dotnet/README.md) |
| Python Compiler | [src/rules-compiler-python/README.md](../src/rules-compiler-python/README.md) |
| Rust Compiler | [src/rules-compiler-rust/README.md](../src/rules-compiler-rust/README.md) |
| Shell Scripts | [src/rules-compiler-shell/README.md](../src/rules-compiler-shell/README.md) |
| AdGuard DNS API Clients (.NET, TypeScript, Rust, PowerShell) | [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) |

### Development

| Document | Location |
|----------|----------|
| Claude Code Instructions | [CLAUDE.md](../CLAUDE.md) |
| Copilot Instructions | [.github/copilot-instructions.md](../.github/copilot-instructions.md) |
| Security Policy | [SECURITY.md](../SECURITY.md) |
| Release Guide | [release-guide.md](release-guide.md) |

## External Resources

- [AdGuard DNS](https://adguard-dns.io/)
- [AdGuard DNS API Documentation](https://api.adguard-dns.io/static/swagger/swagger.json)
- [@bloqr/compiler-core on JSR](https://jsr.io/@bloqr/compiler-core)
- [AdBlock Tester](https://adblock-tester.com/)
- [AdGuard Tester](https://d3ward.github.io/toolz/adblock.html)
