# Documentation

This directory contains comprehensive documentation for the ad-blocking repository.

## Quick Links

| Document | Description |
|----------|-------------|
| [**Why Validation Matters**](WHY_VALIDATION_MATTERS.md) | 🔒 **Start Here** - Understand why security validation is essential |
| [**Dashboard User Guide**](guides/dashboard-guide.md) | Single pane of glass: compiler configs, compilations, profiles, and operations |
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
| [**Dashboard User Guide**](guides/dashboard-guide.md) | Console application for compiler configs, profiles, compilations, and diagnostics |
| [**@bloqr/compiler-core Guide**](guides/compiler-core-guide.md) | **Core compilation package with CI/CD examples** |
| [Docker Guide](docker-guide.md) | Using Docker for development |
| [Configuration Reference](configuration-reference.md) | Full configuration schema documentation |
| [Compiler Comparison](compiler-comparison.md) | Feature comparison of all compilers |
| [Release Guide](release-guide.md) | Creating releases with automatic binary builds |
| [ConsoleUI Architecture](guides/consoleui-architecture.md) | Console UI design documentation (the Dashboard's architecture template — see `src/apps/dashboard/ARCHITECTURE.md`) |

### Architecture & Restructuring

| Document | Description |
|----------|--------------|
| [Dual-Engine Compilation](architecture/dual-engine-compilation.md) | Server-side/DNS vs client-side/browser compilation: how `EngineDetector` routes sources, why the two artifacts are never merged, browser-safe vs. DNS-only transformations, and the ownership split with the commercial compiler |
| [Versioning Strategy](architecture/versioning-strategy.md) | Org-wide per-package JSR versioning standard, with `@bloqr/compiler-core` as the reference implementation |
| [NuGet Distribution Strategy](architecture/nuget-distribution-strategy.md) | Decision record for publishing `Bloqr.Compiler.Abstractions`/`Core` to GitHub Packages, and why in-repo consumers keep `<ProjectReference>` |
| [Release Packaging Strategy](architecture/release-packaging-strategy.md) | Source vs. binary-only release shapes, self-contained-vs-Native-AOT evaluation, and launcher dependency preflight checks |
| [Restructuring Retrospective](RESTRUCTURING_RETROSPECTIVE.md) | How this repo got split into `bloqr-core`/`bloqr-blocklists`/`bloqr-apiclients`, the JSR publishing story, and lessons learned |
| [Epic #256 Retrospective](EPIC_256_RETROSPECTIVE.md) | Dashboard, common library, validation-library (`rules-validator`, since renamed `src/validation/`)/crates.io publishing, and the documentation rewrite — what shipped, obstacles overcome, and what's still open for #331/#372 |
| [Compiler Comparison](compiler-comparison.md) | Feature comparison of all compilers |

### AdGuard DNS API Reference

The AdGuard DNS API clients (and their generated API reference docs) moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) and are no longer part of this repo.

## Project Documentation

### Main README Files

| Project | Location |
|---------|----------|
| Repository Overview | [README.md](../README.md) |
| TypeScript Compiler (`@bloqr/compiler-core`) | [src/compilers/typescript/README.md](../src/compilers/typescript/README.md) |
| Common .NET Library (`Bloqr.Compiler.Abstractions`/`Core`) | [src/common/dotnet/README.md](../src/common/dotnet/README.md) |
| .NET Compiler | [src/compilers/dotnet/README.md](../src/compilers/dotnet/README.md) |
| Python Compiler | [src/compilers/python/README.md](../src/compilers/python/README.md) |
| Rust Compiler | [src/compilers/rust/README.md](../src/compilers/rust/README.md) |
| PowerShell Toolkit | [src/compilers/powershell/README.md](../src/compilers/powershell/README.md) |
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
