# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This repository is a comprehensive multi-language toolkit for ad-blocking, network protection, and AdGuard DNS management:

### Rules Compilers (4 languages)
- **TypeScript** (`src/compilers/typescript/`) - Deno 2.0+ with npm compatibility
- **C#/.NET 10** (`src/compilers/dotnet/`) - Library and Spectre.Console CLI with DI support
- **Python 3.9+** (`src/compilers/python/`) - pip-installable package with CLI and API
- **Rust** (`src/compilers/rust/`) - High-performance single binary with zero runtime deps

### PowerShell Modules
- **BloqrCompiler Toolkit** (`src/compilers/powershell/`) - the sole cross-platform scripting-language compiler (PowerShell 7+ runs on Windows/Linux/macOS, so the earlier separate bash/zsh scripts under `src/compilers/shell/` were retired in favor of it) - canonical, actively-developed modular PowerShell toolkit (class-based `Common`/`BloqrCompiler` modules with Pester tests)

### Common .NET Library
- **`Bloqr.Compiler.Abstractions`/`Bloqr.Compiler.Core`** (`src/common/dotnet/`) - Shared .NET library (own solution, `CompilerCommon.slnx`) consumed by the .NET rules compiler and Dashboard via `<ProjectReference>`; not part of either consumer's solution

### Dashboard
- **Bloqr Dashboard** (`src/apps/dashboard/`) - flagship .NET console app (own solution, `BloqrDashboard.slnx`) for generating/editing/round-tripping compiler configs, running compilations with rich progress UI, and validation/diagnostics — menu-driven interactively, or automatable via CLI switches or as an embedded library (`IDashboardService`)

### Rules Validator
- **Validation Library** (`src/validation/`) - Rust validation library (`core/`, published to crates.io as [`bloqr-validator-core`](https://crates.io/crates/bloqr-validator-core)) and CLI (`cli/`, published as [`bloqr-validator-core-cli`](https://crates.io/crates/bloqr-validator-core-cli), installable via `cargo install`) for filter/config validation

### Documentation Site
- **Website** (`website/`) - Gatsby 5 documentation site covering guides, API reference, and security docs; lives at the repo root (not under `src/`) since it's slated for eventual extraction into its own repository

### Configuration Support
All compilers read the same JSON/JSONC configuration schema (`schemas/compiler-config.schema.json`), documented and IDE-autocompletable via first-party JSON Schemas. YAML/TOML remain functionally supported by the underlying config readers for backward compatibility but are undocumented — see `docs/configuration-reference.md`.

### API Clients (moved)
The AdGuard DNS API clients (.NET, TypeScript, Rust, PowerShell) and the Linear import tool moved to **[`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients)** (internal repo) — they're no longer part of this repository.

## Docker Development Environment

A fully-featured Docker environment with all compilers and tools:

```dockerfile
# Dockerfile.warp
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble
# Includes: .NET 10 SDK, Deno 2.x, Python 3.12, Rust stable, PowerShell 7
# Pre-installed: hostlist-compiler (via Deno), pytest, ruff, clippy, Pester
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

### TypeScript Rules Compiler (`src/compilers/typescript/`)
```bash
cd src/compilers/typescript

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

### Common .NET Library (`src/common/dotnet/`)
```bash
cd src/common/dotnet
dotnet restore CompilerCommon.slnx
dotnet build CompilerCommon.slnx
dotnet test CompilerCommon.slnx
dotnet pack src/Bloqr.Compiler.Abstractions/Bloqr.Compiler.Abstractions.csproj -c Release -o ./nuget-packages
dotnet pack src/Bloqr.Compiler.Core/Bloqr.Compiler.Core.csproj -c Release -o ./nuget-packages
```

### .NET Compiler (`src/compilers/dotnet/`)
```bash
cd src/compilers/dotnet
dotnet restore CompilerDotnet.slnx
dotnet build CompilerDotnet.slnx
dotnet test CompilerDotnet.slnx
dotnet run --project src/Bloqr.Compiler.Dotnet.Console/Bloqr.Compiler.Dotnet.Console.csproj

# Command-line options
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- --config path/to/config.yaml
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- --config config.json --copy
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- --config config.yaml --verbose
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- --config config.yaml --validate
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- --version
```

### Python Compiler (`src/compilers/python/`)
```bash
cd src/compilers/python

# Install in development mode
pip install -e .

# Install with dev dependencies
pip install -e ".[dev]"

# Run tests
pytest
pytest -v                    # Verbose output
pytest --cov=bloqr_compiler  # With coverage

# CLI usage
bloqr-compiler                           # Use default config
bloqr-compiler -c config.yaml            # Specific config
bloqr-compiler -c config.json -r         # Compile and copy to rules
bloqr-compiler -c config.toml -o out.txt # Custom output
bloqr-compiler -V                        # Show version info
bloqr-compiler -d                        # Debug output
bloqr-compiler --help                    # Show help
```

### Rust Rules Compiler (`src/compilers/rust/`)
Split into a library crate (`core/`, published as `bloqr-compiler-core`) and a
thin CLI crate (`cli/`, published as `bloqr-compiler`, unchanged binary name)
mirroring `src/validation`'s `core`/`cli` split (#173) - both are members of
the repo-root Cargo workspace, so build/test commands run from the repo root
with `-p`, or `cd` straight into `cli/` for CLI-only iteration.
```bash
# Build (from repo root)
cargo build -p bloqr-compiler -p bloqr-compiler-core              # Debug build
cargo build --release -p bloqr-compiler -p bloqr-compiler-core    # Release build (optimized)

# Run tests
cargo test -p bloqr-compiler -p bloqr-compiler-core
cargo test -p bloqr-compiler -p bloqr-compiler-core -- --nocapture  # With output

# CLI usage (from src/compilers/rust/cli, or `cargo run -p bloqr-compiler --` from the repo root)
cd src/compilers/rust/cli
cargo run -- -c config.yaml              # Specific config
cargo run -- -c config.json -r           # Compile and copy to rules
cargo run -- -c config.toml -o out.txt   # Custom output
cargo run -- -V                          # Show version info
cargo run -- -d                          # Debug output
cargo run -- --help                      # Show help

# Release binary (built to the workspace-root target/ dir regardless of which member you built)
./target/release/bloqr-compiler -c config.yaml
```

### PowerShell BloqrCompiler Toolkit (`src/compilers/powershell/`)
```powershell
# Import the modules
Import-Module ./src/compilers/powershell/Common/Common.psd1
Import-Module ./src/compilers/powershell/BloqrCompiler/BloqrCompiler.psd1

# Compile filter rules
Invoke-BloqrCompiler                                          # Use default config
Invoke-BloqrCompiler -ConfigPath config.yaml -CopyToRules      # YAML config, copy to rules

# Run Pester tests
Invoke-Pester -Path ./src/compilers/powershell -Recurse

# Lint with PSScriptAnalyzer
Invoke-ScriptAnalyzer -Path src/compilers/powershell -Recurse
```

## Running Individual Tests

### TypeScript (Deno)
```bash
cd src/compilers/typescript
deno test src/cli.test.ts                  # By file
deno test --filter "parseArgs"             # By test name
deno task test:coverage                    # With coverage
```

### .NET (xUnit)
```bash
cd src/compilers/dotnet
dotnet test CompilerDotnet.slnx --filter "FullyQualifiedName~BloqrCompilerServiceTests"

cd ../common/dotnet
dotnet test CompilerCommon.slnx --filter "FullyQualifiedName~ConfigurationValidatorTests"
dotnet test CompilerCommon.slnx --filter "FullyQualifiedName~TransformationTests"
```

### PowerShell (Pester)
```powershell
# Run all PowerShell tests
Invoke-Pester -Path ./src/compilers/powershell/

# Run with detailed output
Invoke-Pester -Path ./src/compilers/powershell/ -Output Detailed
```

### Python (pytest)
```bash
cd src/compilers/python
pytest                                    # Run all tests
pytest -v                                 # Verbose output
pytest tests/test_config.py               # Specific file
pytest -k "test_read_yaml"                # By test name
pytest --cov=bloqr_compiler               # With coverage
```

### Rust (cargo test)
```bash
# From the repo root - bloqr-compiler-core (src/compilers/rust/core) and
# bloqr-compiler (src/compilers/rust/cli) are separate workspace members.
cargo test -p bloqr-compiler-core                          # Run all library tests
cargo test -p bloqr-compiler-core -- --nocapture            # With output
cargo test -p bloqr-compiler-core test_count_rules          # Specific test
cargo test -p bloqr-compiler-core config::                  # Tests in module
cargo test -p bloqr-compiler                                # CLI crate's own tests (config discovery)
```

## Architecture

### Filter Rules
- Compiled filter lists live in [`BloqrAI/bloqr-blocklists`](https://github.com/BloqrAI/bloqr-blocklists), not this repo — see `output/adguard_dns_filter.txt` there.

### Bloqr Compiler - TypeScript (`src/compilers/typescript/`)
- TypeScript compiler published as `@bloqr/compiler-core` (JSR)
- Deno 2.0+ runtime, with Bun formally supported and Node.js compatibility following from Bun's own shims
- Supports JSON, YAML, and TOML configuration formats
- **Library API** (`src/lib/`, exported as `@bloqr/compiler-core/lib`):
  - `BloqrCompiler` / `BloqrCompilerBuilder` - Main service class with fluent builder pattern (`RulesCompiler`/`RulesCompilerBuilder` remain as deprecated aliases)
  - `ConfigurationBuilder` - Programmatic configuration creation
- **Dual Mode Support**:
  - Interactive menu mode (default when no args)
  - CLI mode (when config path or action flags provided)
- `src/orchestration/cli.ts` - Command-line interface with argument parsing and mode detection
- `src/orchestration/config-reader.ts` - Multi-format configuration reader
- `src/orchestration/compiler.ts` - Core compilation orchestration (chunking, parallel compilation, hashing)
- `src/compiler/` - The core compilation engine (`FilterCompiler`, `SourceCompiler`, transformations, downloader, formatters), exported as the package root (`@bloqr/compiler-core`)
- `src/console/` - Interactive console UI components, exported as `./console`:
  - `app.ts` - `ConsoleApplication` class with menu-driven interface
  - `utils.ts` - Console utilities (spinners, tables, colored output)
- `src/mod.ts` - Deno entry point; `src/mod.bun.ts` - Bun entry point, exported as `./bun`
- `deno.json` - Deno configuration and tasks
- Key classes: `BloqrCompiler`, `BloqrCompilerBuilder`, `ConfigurationBuilder`, `ConsoleApplication`
- Uses Deno's built-in testing framework

### Common .NET Library (`src/common/dotnet/`)
- Its own independent .NET solution (`CompilerCommon.slnx`), isolated from `CompilerDotnet.slnx` and `BloqrDashboard.slnx` — every in-repo .NET consumer reaches it via `<ProjectReference>` across directories, not via shared solution membership
- `Bloqr.Compiler.Abstractions` - Interfaces, event-args, and model/DTO types shared across the compiler stack
- `Bloqr.Compiler.Core` - Configuration reading/validation, chunking, file-locking, plugin management, and the compilation pipeline, built on `Bloqr.Compiler.Abstractions`
- Consumed by `Bloqr.Compiler.Dotnet` (`src/compilers/dotnet/`) and `Bloqr.Dashboard.Abstractions`/`Bloqr.Dashboard.Core` (`src/apps/dashboard/`)
- Published as NuGet packages to GitHub Packages — see `docs/architecture/nuget-distribution-strategy.md`

### Bloqr Compiler - .NET (`src/compilers/dotnet/`)
- .NET 10 library for filter compilation
- Supports JSON, YAML, and TOML configuration formats
- `Bloqr.Compiler.Dotnet` - Thin library referencing `Bloqr.Compiler.Abstractions`/`Bloqr.Compiler.Core` (from `src/common/dotnet/`), plus compiler-specific services (e.g. `FilterCompiler`)
- `Bloqr.Compiler.Dotnet.Console` - Spectre.Console interactive and CLI frontend
- `Bloqr.Compiler.Dotnet.Tests` - xUnit tests
- Key interfaces: `IBloqrCompilerService`, `IConfigurationReader`, `IFilterCompiler`
- Features: Configuration validation, verbose mode, dependency injection

### Bloqr Compiler - Python (`src/compilers/python/`)
- Python 3.9+ package for filter compilation
- Supports JSON, YAML, and TOML configuration formats
- `bloqr_compiler/config.py` - Multi-format configuration reader
- `bloqr_compiler/compiler.py` - Core `BloqrCompiler` class and `compile_rules()` function
- `bloqr_compiler/cli.py` - argparse-based CLI
- Install via `pip install -e .` for development
- Key classes: `BloqrCompiler`, `CompilerConfiguration`, `CompilerResult`
- Tools: pytest, mypy, ruff

### Bloqr Compiler - Rust (`src/compilers/rust/`)
- High-performance Rust library and CLI for filter compilation, split into a
  core lib crate + thin CLI crate (#173), mirroring `src/validation`'s own
  `core`/`cli` split — both are members of the repo-root Cargo workspace
- Supports JSON, YAML, and TOML configuration formats
- **`core/`** (published as `bloqr-compiler-core`, lib name `bloqr_compiler`) - the library:
  - `core/src/config.rs` - Configuration structs and parsing
  - `core/src/compiler.rs` - `BloqrCompiler` struct and `compile_rules()` function
  - `core/src/error.rs` - `CompilerError` enum with thiserror
  - `core/src/events.rs`, `core/src/chunking.rs` - event dispatch and chunked/parallel compilation
- **`cli/`** (published as `bloqr-compiler`, unchanged binary name/CLI surface) - the CLI:
  - `cli/src/main.rs` - clap-based CLI with argument parsing, depends on `core/` via a path+version dependency
- Single binary distribution with zero runtime dependencies (except hostlist-compiler)
- Key structs: `BloqrCompiler`, `CompilerConfiguration`, `CompilerResult`, `VersionInfo`
- LTO optimization enabled for small binary size

### PowerShell Toolkit (`src/compilers/powershell/`)
- The sole cross-platform scripting-language compiler (PowerShell 7+ runs on Windows/Linux/macOS) - the earlier separate bash/zsh scripts under `src/compilers/shell/` were retired in favor of it
- **Common** (`Common/`) - Shared `CompilerLogger` and `CompilerResult` classes used by other modules
- **BloqrCompiler** (`BloqrCompiler/`) - Class-based rules compiler module (`CompilerConfiguration`, `CompilerResult`, `CompilerLogger`)
- Each module ships its own `.psd1` manifest and `Tests/` Pester suite

### Validation Library (`src/validation/`)
- `core/` (`bloqr-validator-core`) - Rust library for validating filter/config files (crates.io)
- `cli/` (`bloqr-validator-core-cli`) - CLI frontend (`bloqr-validate`) for the validation library (crates.io, `cargo install`)

### Documentation Website (`website/`)
- Gatsby 5 static site with guides, API reference, and security documentation pages
- Lives at the repo root, not under `src/`, since it's slated for eventual extraction into its own repository
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
| `defaultEngine` | string | No | Default compilation engine (`dns`/`browser`) for sources without their own `engine` and that can't be content-sniffed — see [Dual-Engine Compilation](docs/architecture/dual-engine-compilation.md) |

### Source Properties
| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `source` | string | Yes | URL or file path |
| `name` | string | No | Source identifier |
| `type` | string | No | `adblock` or `hosts` |
| `transformations` | array | No | Source-specific transforms |
| `inclusions` | array | No | Source-specific includes |
| `exclusions` | array | No | Source-specific excludes |
| `engine` | string | No | Compilation engine/grammar for this source (`dns`/`browser`); overrides auto-detection when set — see [Dual-Engine Compilation](docs/architecture/dual-engine-compilation.md) |

### Available Transformations
RemoveComments, Compress, RemoveModifiers, Validate, ValidateAllowIp, Deduplicate, InvertAllow, RemoveEmptyLines, TrimLines, InsertFinalNewLine, ConvertToAscii, ConflictDetection, RuleOptimizer

Not every transformation is valid for every engine: `Compress`, `Validate`/`ValidateAllowIp`, and `InvertAllow` are DNS-only (they assume DNS/hosts-style grammar and are rejected on a browser-engine source); the rest are browser-safe. See [Dual-Engine Compilation](docs/architecture/dual-engine-compilation.md#which-transformations-are-browser-safe) for the full breakdown and why.

## Environment Variables

| Variable | Description |
|----------|-------------|
| `DEBUG` | Set to any value to enable debug logging |
| `LOG_LEVEL` | Log level (DEBUG, INFO, WARN, ERROR, SILENT) |
| `LOG_FORMAT` | Set to `json` for structured logging |
| `BLOQR_COMPILER_config` | Default configuration file path (.NET compiler) |
| `BLOQR_COMPILER_Logging__LogLevel__Default` | Log level for .NET compiler |

## CI/CD Alignment

GitHub Actions workflows validate:
- `.github/workflows/dotnet.yml` - Builds/tests the common .NET library, compiler, and Dashboard (three matrix entries, one per `.slnx`) with .NET 10
- `.github/workflows/typescript.yml` - Deno 2.x for the TypeScript rules compiler
- `.github/workflows/rust-clippy.yml` - Builds, tests, formats, and lints the Rust workspace (rules compiler, validation library)
- `.github/workflows/python.yml` - Builds and tests the Python rules compiler across supported Python versions
- `.github/workflows/powershell.yml` - Pester tests and PSScriptAnalyzer for the PowerShell toolkit
- `.github/workflows/build-scripts-tests.yml` - Exercises the root `build.sh`/`build.ps1` launcher scripts
- `.github/workflows/gatsby.yml` - Builds the `website` documentation site
- `.github/workflows/security.yml` - Consolidated security scanning (CodeQL, DevSkim, PSScriptAnalyzer)
- `.github/workflows/docker-image.yml` - Builds the `Dockerfile.warp` development image
- `.github/workflows/validation-compliance.yml` - Runs the Rust validation CLI against filter/config fixtures
- `.github/workflows/release.yml` - Builds and publishes release binaries (.NET, Rust, Python)
- `.github/workflows/publish-jsr.yml` - Publishes `@bloqr/compiler-core` to JSR on pushes to `main` touching `src/compilers/typescript/**`; idempotent (no-ops if the current `deno.json` version is already published). Token-authenticated (`JSR_WORKFLOW_TOKEN`) — see `docs/jsr-token-authentication.md`.
- `.github/workflows/compiler-core-version-bump.yml` / `.github/workflows/compiler-core-create-version-tag.yml` - Automated Conventional-Commits version bumping and tagging for `@bloqr/compiler-core`, scoped to that package only. Reference implementation for the org-wide per-package versioning standard — see `docs/architecture/versioning-strategy.md` before adding an equivalent pair for any future decomposed package.
- `.github/workflows/stale-reference-check.yml` - Greps for the pre-rename GitHub repo path and the retired JSR scope (see the workflow file itself for the exact patterns — this file can't repeat them literally without tripping the check).
- `.github/workflows/claude.yml` - Claude AI integration for @claude mentions
- `.github/workflows/claude-code-review.yml` - Automated PR code review
- `.github/workflows/label.yml` / `.github/workflows/stale.yml` / `.github/workflows/summary.yml` - Repository housekeeping (labeling, stale-issue management, PR summaries)

## Operational Notes for AI-Assisted Work

- **JSR and crates.io publishing both use OIDC ("trusted publishing") as of
  2026-08-14.** Both previously failed OIDC — JSR with `InvalidIssuer`
  (#291, reported upstream as [jsr-io/jsr#1485](https://github.com/jsr-io/jsr/issues/1485)
  before the real cause was found), crates.io with `Unsupported JWT issuer`.
  Root cause for both: this org's GitHub Enterprise account ("Bloqr
  Systems") had **"Use enterprise-specific issuer URL"** enabled (Enterprise
  Settings → Policies → Actions → OIDC Configuration), which appends the
  enterprise slug to every Actions OIDC token's issuer — neither registry's
  trusted-publishing backend accepts that non-standard issuer. Not a JSR
  bug, not a crates.io bug, not fixable in workflow YAML. That Enterprise
  setting has since been disabled, and OIDC is now live-verified working
  end-to-end for both (real version publishes, not just green CI). If
  `InvalidIssuer` / `Unsupported JWT issuer` reappears on *any* future
  OIDC-based integration (npm trusted publishing, PyPI, a cloud provider,
  etc.), check that Enterprise setting before assuming the new integration
  itself is broken. See `docs/jsr-token-authentication.md` and the auth
  comment block at the top of `.github/workflows/publish-crates.yml`.
- **Versioning is per-package, not per-repo.** Each independently-JSR-published
  package gets its own `VERSION` source-of-truth, its own `version:sync`
  script, its own `<package-slug>-v<semver>` tag prefix, and its own
  bump/tag workflow pair — never a single repo-wide version. See
  `docs/architecture/versioning-strategy.md` for the pattern and the
  checklist for onboarding a new package.
- **`@bloqr/compiler-core`'s JSR symbol-documentation score must stay as
  close to 100% as possible.** Any time you add, rename, or touch an
  exported symbol in `src/compilers/typescript/src/` — including enum
  members and public interface/class properties and methods, not just
  top-level declarations — add or update its JSDoc in the same change.
  `deno doc --lint` (run in every CI job) only catches top-level exported
  symbols with *zero* JSDoc; it does not see undocumented enum members or
  interface/class members, which is exactly the gap that let the score
  drop from ~88% to 61% in practice (PR #310's Bun-support work added
  several undocumented enum members that `deno doc --lint` never flagged).
  Run `deno task lint:docs` (`scripts/check-symbol-docs.ts`) — wired into
  both `typescript.yml` and `publish-jsr.yml` as a required CI step — to
  check the finer-grained coverage JSR itself scores against; it fails the
  build below a 98% threshold and prints exactly which symbols are
  missing docs.
- **`deno.json` may contain `//` comments (JSONC)** — Deno's own config
  loader tolerates them, but don't assume every script touching it does.
  Any code that reads/writes `deno.json` programmatically (like
  `scripts/sync-version.ts`) must be JSONC-safe (regex-based field edits,
  not a `JSON.parse`/`stringify` round-trip) or it will silently corrupt
  or reject the file the moment someone adds an explanatory comment.
- **Always use `isolation: "worktree"` when running more than one
  background agent against this repo in parallel.** Concurrent agents
  sharing one working directory has caused real branch-race contamination
  here before (commits landing on the wrong branch, stale uncommitted
  state). See `docs/RESTRUCTURING_RETROSPECTIVE.md` for the full incident.
- **After merging a batch of interdependent PRs, do one real
  end-to-end verification pass against the actual merged `main`** — not
  just trusting each PR's individually-green CI. A real bug
  (`sync-version.ts` breaking on JSONC comments introduced by a sibling
  PR) shipped past every individual PR's CI and was only caught this way.
- Full narrative, including the OIDC investigation timeline and the
  `bloqr-compiler` dependency-swap decisions, is in
  `docs/RESTRUCTURING_RETROSPECTIVE.md`. Its sequel, `docs/EPIC_256_RETROSPECTIVE.md`,
  covers the Dashboard/common-library/rules-validator/crates.io-publishing
  epic that followed it — read both before picking up #331/#372.
- **The `src/` reorg (#331/#372) has a written naming blueprint —
  `docs/adr/0004-src-reorg-naming-blueprint.md`** — read it before starting
  any further category migration (`compilers/*`, `common/dotnet`,
  `apps/dashboard`). It documents the completed `validation/` pilot, the
  rule for what to rename vs. leave alone (cross-language/FFI/CLI-invocation
  surfaces yes, a language's own internal-only identifiers no, each
  following that language's own idioms rather than one cross-language
  pattern), and that **this standard applies to `bloqr-core` and future FOSS
  libraries only — not to internal/commercial repos** like `bloqr-compiler`,
  which follow their own org-driven structure instead.
- **After a squash-merged PR, reset the branch onto fresh `origin/main`
  before adding more commits to it — don't assume it's still equivalent to
  `main`.** A squash merge creates a *new* commit object on `main` with the
  same content as (but a different SHA from) the branch's pre-merge commit.
  Continuing to build on the stale local branch makes the next PR show a
  real merge conflict even though nothing actually conflicts, because
  GitHub sees two independently-authored versions of the same change. Hit
  this twice during epic #256 (PRs #374 and #375) before it was fully
  internalized — see `docs/EPIC_256_RETROSPECTIVE.md`'s "Obstacles
  overcome" for the exact recovery sequence
  (`git rebase --onto origin/main <stale-commit> <branch>`).
- **TypeScript/JavaScript runtime and package-manager order of preference,
  for every project in this repo:**
  1. **Deno-native first.** Design new TypeScript code, and structure new
     projects, to run under Deno without a build step. Reach for Deno's own
     standard library and tasks before anything from the npm ecosystem.
  2. **JSR packages before npm packages.** When a dependency is needed,
     check JSR first; only fall back to `npm:` specifiers in `deno.json`
     when no JSR-native equivalent exists (and say so in a comment next to
     the import, matching the existing `ora`/`figlet`/`@inquirer/prompts`
     entries in `src/compilers/typescript/deno.json`).
  3. **Bun is a formally supported runtime target, not a fallback.**
     Consumer-facing TypeScript packages (e.g. `@bloqr/compiler-core`)
     should work correctly under Bun as well as Deno — no unguarded
     `Deno.*` references outside code explicitly documented as Deno-only —
     and that support should be verified by real CI against real Bun, not
     just asserted in docs. Node.js compatibility follows for free from
     the same `node:*` API shims Bun itself relies on; it is not a runtime
     this repo targets on its own.
  4. **pnpm only when npm/Node-ecosystem tooling is genuinely required**
     (e.g. the Gatsby website), and even then prefer it over plain `npm`.
     Avoid introducing a `package.json` to a Deno-native project just to
     install one npm-only dependency — use `deno.json`'s `npm:` import
     specifiers instead, which don't require one.
  5. **Cloudflare Workers is this org's cloud-native deploy target** for
     anything that needs to run as a service — see `bloqr-compiler` for
     the reference implementation (Workers, Hyperdrive, D1, Durable
     Objects). Gatsby-based `website` is a deliberate exception (no
     meaningful Deno-native static-site-generator option exists) and is
     expected to eventually move to Starlight in `bloqr-compiler`, not stay
     on Gatsby indefinitely.

## Prerequisites

| Requirement | Version | Required For |
|-------------|---------|--------------|
| .NET SDK | 10.0+ | .NET compiler |
| Deno | 2.0+ | TypeScript compiler |
| PowerShell | 7+ | PowerShell scripts |
| Python | 3.9+ | Python compiler |
| Rust | 1.85+ | Rust compiler (install via rustup) |
| @bloqr/compiler-core | 1.0.0 | TypeScript compiler (via JSR: `deno add @bloqr/compiler-core`) |
| Bun | latest | Optional — formally supported alternative runtime target for `src/compilers/typescript` (not required for this repo's own tooling; see that package's README) |
| Docker | 24.0+ | Container development (optional but recommended) |

## Key File Locations

- **Main filter list**: `output/adguard_dns_filter.txt` in [`BloqrAI/bloqr-blocklists`](https://github.com/BloqrAI/bloqr-blocklists)
- **Compiler configs**: `src/compilers/*/`
- **Common .NET library**: `src/common/dotnet/` (`Bloqr.Compiler.Abstractions`/`Bloqr.Compiler.Core`, own solution)
- **JSON Schemas**: `schemas/compiler-config.schema.json`, `schemas/dashboard-config.schema.json` — `compiler-config.schema.json` is wired into `Bloqr.Compiler.Core`'s `ConfigurationValidator` via `CompilerConfigJsonSchemaValidator` (#258); the Dashboard's `ICompilerConfigSchemaValidator` delegates to the same validator rather than re-embedding the schema
- **Deno configs**: `src/*/deno.json`
- **OpenAPI spec**: `api/openapi.yaml` in [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients)
- **Docker config**: `Dockerfile.warp`, `docker-compose.yml`, `.dockerignore`
- **Documentation**: `docs/` — see especially `docs/architecture/versioning-strategy.md`, `docs/RESTRUCTURING_RETROSPECTIVE.md`, and `docs/EPIC_256_RETROSPECTIVE.md`
- **Environment template**: `.env.example`
