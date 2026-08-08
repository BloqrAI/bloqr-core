# Copilot Instructions for Ad-Blocking Repository

> 🔒 **Security First**: All filter compilation includes mandatory validation. See [Why Validation Matters](../docs/WHY_VALIDATION_MATTERS.md) for user-friendly explanation.

## Repository Overview

Multi-language toolkit for ad-blocking and AdGuard DNS management with **identical output** across all compilers. Key principle: **Four languages, one schema, same SHA-384 hash**.

**Components**:
- **Filter Rules**: AdGuard filter lists with input/output separation
  - **Input**: `../bloqr-blocklists/input/` - Local rules and internet source references with hash verification
  - **Output**: `../bloqr-blocklists/output/adguard_user_filter.txt` - Final compiled list in adblock format
- **API Client**: C# SDK for AdGuard DNS API v1.15 with Polly resilience
- **Rules Compilers**: TypeScript, C#, Python, Rust - all produce identical output
- **ConsoleUI**: Spectre.Console menu-driven interface with DI architecture
- **Website**: Gatsby portfolio site deployed to GitHub Pages
- **Scripts**: PowerShell modules and cross-platform shell scripts

## Architecture Principles

### Centralized Validation Layer
**NEW**: Rust-based validation library (`src/rules-validator/`) provides unified security across all compilers:
- **At-rest hash verification**: SHA-384 for local files (`../bloqr-blocklists/input/.hashes.json` database)
- **In-flight hash verification**: SHA-384 for downloads (prevents MITM)
- **URL security**: HTTPS enforcement, domain validation, content verification
- **Syntax validation**: Adblock and hosts format linting
- **File conflict handling**: Rename, overwrite, or error strategies
- **Archiving**: Timestamped with manifest tracking

**Integration Options**:
- **Rust compiler**: Direct native library usage (zero overhead)
- **.NET compiler**: P/Invoke to native library or WASM via Wasmtime
- **Python compiler**: PyO3 bindings for native Rust integration
- **TypeScript compiler**: WebAssembly module (no Node.js runtime required)

**Build outputs**:
- `librules_validator.so/.dll/.dylib` (native libraries)
- `rules_validator.wasm` (WebAssembly module)
- `rules-validate` (CLI tool)

### Compiler Equivalence
All four compilers (TypeScript, .NET, Python, Rust) use **[@bloqr/compiler-core](../src/adblock-compiler-core/)** and **must**:
- Support JSON, YAML, TOML config formats (except PowerShell: JSON only)
- Count rules identically (exclude empty lines and `!`/`#` comments)
- Compute SHA-384 hash of output (96 hex chars)
- Return same result structure: `{ success, ruleCount, hash, elapsedMs, outputPath }`
- **Use centralized validation library** for all security checks

📘 **See [adblock-compiler guide](../docs/guides/adblock-compiler-guide.md)** for package details, benefits, and CI/CD integration examples.

### ConsoleUI DI Pattern
`src/adguard-api-dotnet/src/AdGuard.ConsoleUI/` uses service-oriented architecture:
```
Program.cs → ConsoleApplication → [DeviceMenu, DnsServerMenu, StatisticsMenu] → ApiClientFactory → AdGuard.ApiClient
```
- **ApiClientFactory**: Manages API configuration, creates clients, tests connectivity
- All services registered via DI (`services.AddSingleton<T>()`)
- Configuration sources (priority): appsettings.json → environment vars (`ADGUARD_*`) → user secrets

## Project Structure

```
bloqr-core/
├── .github/                        # GitHub configuration and workflows
├── docs/                           # Documentation (API docs, guides)
├── schemas/                        # First-party JSON schemas
├── src/
│   ├── adblock-compiler-core/      # TypeScript rules compiler (the JSR-published @bloqr/compiler-core)
│   ├── rules-compiler-dotnet/      # .NET rules compiler
│   ├── rules-compiler-python/      # Python rules compiler
│   ├── rules-compiler-rust/        # Rust rules compiler
│   ├── rules-compiler-shell/       # Shell scripts (bash, zsh)
│   ├── rules-compiler-powershell/  # Canonical PowerShell compiler toolkit
│   ├── rules-validator/            # Rust validation library + CLI
│   └── website/                    # Gatsby documentation website
└── README.md
```

The AdGuard/Linear API clients (`adguard-api-*`, `linear`) and the compiled filter lists (formerly `data/`) have moved to their own repos: [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) (internal) and [`BloqrAI/bloqr-blocklists`](https://github.com/BloqrAI/bloqr-blocklists) (public), respectively.

## Critical Workflows

### Compiling Filter Rules (Core Workflow)
**Goal**: Generate `../bloqr-blocklists/output/adguard_user_filter.txt` with verified output in adblock format

**Input Processing**:
1. Scan `../bloqr-blocklists/input/` for local filter files (`.txt`, `.hosts`)
2. Parse `internet-sources.txt` for remote list URLs
3. **Validate URLs for security**:
   - Enforce HTTPS only (reject HTTP)
   - Validate domain via DNS
   - Verify Content-Type is text/plain
   - Scan first 1KB for valid filter syntax
   - Optional: verify SHA-384 hash (URL format: `https://...#sha384=hash`)
4. Validate syntax of all sources (adblock/hosts format)
5. Compute SHA-384 hashes for integrity verification
6. Detect tampering via hash comparison

**Compilation**:
```bash
# TypeScript (primary method)
cd src/adblock-compiler-core && npm run compile

# Validates inputs, fetches remote sources, writes output, computes SHA-384
# CI: .github/workflows/typescript.yml runs this with type-checking
```

**Output Guarantees**:
- ✅ Always in adblock syntax (not hosts format)
- ✅ SHA-384 hash computed and verified
- ✅ Rule count validation (excludes comments/empty lines)
- ✅ Deduplication and validation applied
- ✅ URL security verified (HTTPS, domain validation, content checks)

**Why four compilers?** Provides language flexibility while ensuring identical output. Use `docs/compiler-comparison.md` to choose based on runtime requirements.

### API Client Development Pattern
When working with `src/adguard-api-dotnet/`:
1. **Never modify auto-generated files** in `src/AdGuard.ApiClient/` (regenerated from `api/openapi.yaml`)
2. Place customizations in `Helpers/` (e.g., `ConfigurationHelper.cs`, `RetryPolicyHelper.cs`)
3. ConsoleUI services inherit from menu base classes, inject `ApiClientFactory`

### Testing Strategy
- **TypeScript**: Jest with coverage (`npm test`, `npm run test:coverage`)
- **.NET**: xUnit (`dotnet test`), filter by class: `--filter "FullyQualifiedName~DevicesApiTests"`
- **Python**: pytest, mypy for type checking
- **Rust**: cargo test
- **PowerShell**: PSScriptAnalyzer (CI enforced)

### Compiler Output Verification
**Cross-compiler testing workflow** (ensuring identical output):

1. **Compile with all four compilers** using the same config:
   ```bash
   # TypeScript
   cd src/adblock-compiler-core && npm run compile
   
   # .NET
   cd src/rules-compiler-dotnet && dotnet run --project src/RulesCompiler.Console
   
   # Python
   cd src/rules-compiler-python && rules-compiler
   
   # Rust
   cd src/rules-compiler-rust && cargo run --release
   ```

2. **Compare SHA-384 hashes** - all must produce identical 96-char hash:
   ```bash
   # Each compiler outputs: { hash: "abc123...", ruleCount: 1234, ... }
   # Verify: hash1 === hash2 === hash3 === hash4
   ```

3. **Verify rule counts match** - all compilers must report same number after filtering

4. **Test with different configs** (JSON, YAML, TOML where supported)

**Test file patterns** that verify this:
- TypeScript: `src/adblock-compiler-core/src/__tests__/compiler.test.ts`
- .NET: `src/rules-compiler-dotnet/src/RulesCompiler.Tests/OutputWriterTests.cs`
- Python: `src/rules-compiler-python/tests/test_compiler.py`
- Rust: `src/rules-compiler-rust/src/compiler.rs` (integration tests)

All test suites assert `hash.length === 96` and verify consistent hashing.

## Build and Test Commands

### C# API Client (`src/adguard-api-dotnet/`)
```bash
# Navigate to the solution directory
cd src/adguard-api-dotnet

# Restore dependencies
dotnet restore AdGuard.ApiClient.slnx

# Build
dotnet build AdGuard.ApiClient.slnx --no-restore

# Run tests
dotnet test AdGuard.ApiClient.slnx --no-build --verbosity normal

# Run specific test project
dotnet test src/AdGuard.ApiClient.Test/AdGuard.ApiClient.Test.csproj
```

**Requirements**: .NET 8.0 SDK

### TypeScript Rules Compiler (`src/adblock-compiler-core/`)
```bash
# Install dependencies
npm ci

# Build
npm run build

# Run compiler
npm run compile

# Type check
npx tsc --noEmit

# Lint
npx eslint .

# Run tests
npm test

# Test with coverage
npm run test:coverage
```

**Requirements**: Node.js 18+

### Python Rules Compiler (`src/rules-compiler-python/`)
```bash
# Install in development mode
pip install -e ".[dev]"

# Run tests
pytest

# Type checking
mypy rules_compiler/

# Linting
ruff check rules_compiler/

# Run compiler
rules-compiler --help
```

**Requirements**: Python 3.9+

### Rust Rules Compiler (`src/rules-compiler-rust/`)
```bash
# Build
cargo build

# Build optimized release
cargo build --release

# Run tests
cargo test

# Run compiler
cargo run -- --help
```

**Requirements**: Rust toolchain

### Website (`src/website/`)
```bash
# Install dependencies
npm ci

# Development server
npm run develop

# Build for production
npm run build
```

**Requirements**: Node.js 18+, Gatsby CLI

### PowerShell Scripts (`src/rules-compiler-powershell/`)
```bash
# Run PSScriptAnalyzer
pwsh -Command "Invoke-ScriptAnalyzer -Path . -Recurse"
```

**Requirements**: PowerShell 7+

## Code Conventions

### General Principles
- Follow existing patterns in each language/component
- Write clear, self-documenting code
- Use descriptive variable and function names
- Add comments only when code intent is not obvious

### C# (.NET)
- Use **C# 14** features with **.NET 10** (see `<TargetFramework>net10.0</TargetFramework>`)
- **Nullable reference types**: Always `#nullable enable` (enforced project-wide)
- **DI Pattern**: Constructor injection for all services (`ILogger<T>`, `IConfiguration`)
  - Register in `Program.cs` or `ServiceCollectionExtensions`
- **Async/Await**: Required for all I/O (file, network, database)
- **Polly**: Use `RetryPolicyHelper` for 408/429/5xx HTTP retries (see `Helpers/RetryPolicyHelper.cs`)
  - API client has built-in retry with exponential backoff
- **Auto-generated code**: Never edit `src/AdGuard.ApiClient/` directly
  - Customizations go in `Helpers/` or ConsoleUI services
- Test with **xUnit**, use `ITestOutputHelper` for test logging

### TypeScript
- **Target**: ES2022, Node.js 18+ (see `package.json` engines)
- **Strict mode** enabled (`"strict": true` in tsconfig.json)
- **Critical**: Use **node:** prefixed imports for built-ins (`node:fs`, `node:path`, `node:crypto`)
  - Why: Explicit Node.js imports vs. polyfills; required for Deno compatibility
- **Hashing**: Use `node:crypto` createHash('sha384') - never SHA-256
- JSDoc for exported functions
- Test with **Jest** (coverage >80% expected)
- ESLint with flat config (`eslint.config.js` not `.eslintrc`)

### Python
- **Python 3.9+** compatibility
- Follow **PEP 8** style guide
- Use **type hints** (typing module)
- Line length: 100 characters (configured in ruff)
- Use **pytest** for testing
- Use **mypy** for type checking
- Use **ruff** for linting and formatting

### Rust
- **Edition 2021**
- Follow standard Rust conventions
- Use **serde** for serialization
- Use **clap** for CLI argument parsing
- Use **thiserror/anyhow** for error handling

### PowerShell
- **Version**: PowerShell 7+ (Core, not Windows PowerShell 5.1)
- **Module structure**: `.psm1` (implementation) + `.psd1` (manifest)
- Use approved verbs (Get-, Set-, New-, Invoke-, etc.)
- **Critical**: Never use `ConvertTo-SecureString` with `-AsPlainText` and plaintext secrets
  - Use secure input or environment variables
- **Testing**: Pester v5 tests in `Tests/` folders
- **Linting**: PSScriptAnalyzer enforced in CI (`.github/workflows/powershell.yml`)
- **RulesCompiler module** (`src/rules-compiler-powershell/`):
  - `Invoke-RulesCompiler` wraps TypeScript compiler

### Security Practices

**CRITICAL - NEVER commit**:
- API keys, tokens, passwords
- `.env` files (only commit `.env.example` templates)

**Environment variables**:
- `ADGUARD_WEBHOOK_URL` - Webhook endpoint for notifications
- `ADGUARD_API_KEY` - AdGuard DNS API authentication
- `ADGUARD_LINEAR_API_KEY` - Linear integration (src/linear/)
- `DEBUG` - Enable debug mode (common cross-platform variable)

**Configuration priority** (ConsoleUI):
1. `appsettings.json` (checked into repo, no secrets)
2. Environment variables with `ADGUARD_` prefix
3. User secrets (`dotnet user-secrets set "AdGuard:ApiKey" "value"`)

### Dependencies & Scanning
- **CodeQL**: Security scanning (`.github/workflows/codeql.yml`)
- **DevSkim**: Additional security analysis
- Update deps regularly; CI breaks on high-severity vulnerabilities
- API client uses **Polly** for rate limiting and retry strategies

### Filter Rules Security
- **Input validation**: All files in `../bloqr-blocklists/input/` undergo syntax validation and linting
- **Hash verification**: SHA-384 hashes computed for all input files to detect tampering
- **Format enforcement**: Output is always adblock syntax, regardless of input formats
- **Source tracking**: Maintains provenance of rules from local and internet sources
- **Syntax validation**: Rules validated before compilation; errors reported with line numbers
- **Remote source verification**: Internet lists verified via hashes after download
- **URL security validation**: Comprehensive security checks for internet sources
  - HTTPS enforcement (HTTP rejected)
  - Domain validation via DNS
  - Content-Type verification (text/plain required)
  - Content scanning for valid filter syntax
  - Optional SHA-384 hash verification (add `#sha384=hash` to URL)
  - File size limits to prevent abuse
- Test rule changes locally before committing to `../bloqr-blocklists/output/adguard_user_filter.txt`
- Rules deployed to AdGuard DNS affect real traffic filtering
- Be cautious when adding rules from untrusted sources
- Validate and test new filter rules before deployment
- Only use trusted, well-known sources for internet lists

## Rules Compilation Standards

### Rule Counting (Cross-Language Contract)
**Must be identical across all compilers**:
```typescript
// Filter logic (pseudocode)
validRules = lines
  .filter(line => line.trim() !== '')        // No empty lines
  .filter(line => !line.startsWith('!'))     // No ! comments
  .filter(line => !line.startsWith('#'))     // No # comments
```

### Hashing (SHA-384 Contract)
- **Algorithm**: SHA-384 (produces 96 hex characters)
- **Input**: Compiled output file content (after writing to disk)
- **Why SHA-384**: Balance of security and performance
- **Verification**: Test suites assert `hash.length === 96`

### Result Structure
All compilers return/output:
```typescript
{
  success: boolean,
  ruleCount: number,     // After filtering
  outputPath: string,
  hash: string,          // SHA-384 (96 chars)
  elapsedMs: number,
  configFormat?: string  // 'json' | 'yaml' | 'toml'
}
```

### Configuration Schema
Supports 3 formats (JSON/YAML/TOML), based on `@bloqr/compiler-core` schema:
```json
{
  "output": "../bloqr-blocklists/output/adguard_user_filter.txt",
  "sources": [
    { "url": "https://example.com/list.txt", "transformations": ["RemoveComments"] }
  ],
  "transformations": ["RemoveComments", "Compress", "Validate"]
}
```
See `src/adblock-compiler-core/compiler-config.json` for reference.

## CI/CD Workflows

### GitHub Actions Workflows
| Workflow | File | Triggers | Purpose |
|----------|------|----------|---------|
| .NET | `dotnet.yml` | Push to main, PRs | Build/test API client (`dotnet test AdGuard.ApiClient.slnx`) |
| TypeScript | `typescript.yml` | Push to main, PRs | Type-check, lint, test (`tsc --noEmit`, `eslint`, `jest`) |
| PowerShell | `powershell.yml` | On-demand | PSScriptAnalyzer on `src/rules-compiler-powershell/` |
| Gatsby | `gatsby.yml` | Push to main | Build and deploy to GitHub Pages |
| CodeQL | `codeql.yml` | Schedule, push to main | Security scanning (breaks on high/critical) |
| DevSkim | `devskim.yml` | Schedule | Additional security analysis |
| Release | `release.yml` | Version tags | Build binaries for distribution |

**CI Alignment**: Local commands should match CI workflows
- TypeScript: `npm ci` (not `npm install`) for reproducible builds
- .NET: `dotnet restore` → `dotnet build --no-restore` → `dotnet test --no-build`

## Development Workflow

### Git Workflow
- Create feature branches from `main`
- Use descriptive commit messages
- CI runs on push to `main` and pull requests

### CI/CD Pipelines
- **.NET**: Builds and tests API client
- **TypeScript**: Builds and lints TypeScript projects
- **PowerShell**: Runs PSScriptAnalyzer
- **CodeQL**: Security scanning
- **DevSkim**: Security scanning
- **Gatsby**: Website deployment

### Testing Strategy
- Write unit tests for new functionality
- Maintain existing test coverage
- Run tests locally before pushing
- CI runs all tests automatically

## Common Tasks

### Adding Dependencies
```bash
# C#
dotnet add package <PackageName>

# TypeScript/Node
npm install <package-name>

# Python
# Add to pyproject.toml [project.dependencies]

# Rust
# Add to Cargo.toml [dependencies]
```

### Running the Full Test Suite
```bash
# Run all .NET tests
cd src/adguard-api-dotnet && dotnet test AdGuard.ApiClient.slnx

# Run all TypeScript tests
cd src/adblock-compiler-core && npm test

# Run all Python tests
cd src/rules-compiler-python && pytest

# Run all Rust tests
cd src/rules-compiler-rust && cargo test
```

## Integration Points

### AdGuard DNS API
- **OpenAPI spec**: `api/openapi.json` (v1.15, primary), `api/openapi.yaml` (optional)
- **Auto-generated client**: `src/adguard-api-dotnet/src/AdGuard.ApiClient/`
- **Base URL**: Configured via `AdGuard:BaseUrl` in appsettings
- **Auth**: Bearer token in `Authorization` header
- **Retry logic**: 3 attempts with exponential backoff (408, 429, 5xx)

### @bloqr/compiler-core
- **All compilers depend on this**: JSR package
- **Installation**: `deno add @bloqr/compiler-core` or via JSR
- **Source**: `src/adblock-compiler-core/` (this repo)
- **Documentation**: https://jsr.io/@bloqr/compiler-core
- Provides 11 transformations (RemoveComments, Compress, Validate, etc.)
- Compilers use this, handling config parsing and result formatting

### Docker Development
- **Dockerfile**: `Dockerfile.warp` (multi-stage with .NET 8 SDK + Node 20 + PowerShell 7)
- **Warp image**: `jaysonknight/warp-env:ad-blocking` (pre-configured)
- **Usage**:
  ```bash
  docker build -f Dockerfile.warp -t ad-blocking-dev .
  docker run -it -v $(pwd):/workspace ad-blocking-dev
  ```

## Key Files & Their Roles

| File/Folder | Purpose | When to Modify |
|-------------|---------|----------------|
| `../bloqr-blocklists/output/adguard_user_filter.txt` | **Production filter list** | After successful compilation and testing |
| `src/adblock-compiler-core/compiler-config.json` | **Primary config** for rule compilation | To change filter sources or transformations |
| `api/openapi.yaml` | AdGuard DNS API spec (v1.15) | Never (upstream dependency) |
| `src/adguard-api-dotnet/src/AdGuard.ApiClient/` | **Auto-generated** API client | Never (regenerate from spec instead) |
| `src/rules-compiler-powershell/Invoke-RulesCompiler.psm1` | PowerShell wrapper for compiler | Extending PowerShell automation |
| `docs/compiler-comparison.md` | **Decision guide** for choosing compiler | When adding features to compilers |

## Common Gotchas

1. **Hash Mismatches**: If compilers produce different hashes, check:
   - Rule counting logic (empty lines, comment filters)
   - SHA-384 algorithm (not SHA-256)
   - Output file encoding (UTF-8 without BOM)

2. **ConsoleUI Config**: Configuration sources cascade; environment vars override appsettings
   - Debug with: `dotnet run -- --verbose` to see loaded config

3. **TypeScript `node:` Prefix**: Required for Deno compatibility; ESLint enforces this

4. **PowerShell JSON-Only**: Unlike other compilers, PowerShell module doesn't support YAML/TOML

5. **CI vs Local**: Always use `npm ci` in CI (lockfile-driven), `npm install` locally

## Linear Integration (`src/linear/`)

**Purpose**: Import repository documentation into Linear for project management tracking.

### When to Use
- Creating Linear project for tracking repository features
- Importing roadmap items as Linear issues
- Syncing documentation structure to Linear
- Setting up project management workflow

### Setup
```bash
cd src/linear
npm install
npm run build

# Configure .env (never commit this!)
ADGUARD_LINEAR_API_KEY=lin_api_your_key_here
ADGUARD_LINEAR_TEAM_ID=optional_team_id
ADGUARD_LINEAR_PROJECT_NAME=Ad-Blocking Documentation
```

### Usage
```bash
# Full import with default settings
npm run import:docs

# Dry run (preview without making changes)
npm run import:dry-run

# Custom file and project
npm run import -- --file /path/to/docs.md --project "My Project"

# List available teams/projects
npm run import -- --list-teams
npm run import -- --list-projects
```

### What Gets Imported
- **Project**: Creates Linear project for tracking
- **Roadmap Issues**: Checkbox lists converted to Linear issues
- **Component Docs**: Each major component becomes a documentation issue
- **Architecture Info**: CI/CD pipelines, dependencies, tech stack

See `src/linear/README.md` for full documentation.

## Release Workflow

**Trigger**: Push a version tag (e.g., `v1.0.0`) to automatically build and release binaries.

### Creating a Release

1. **Update version numbers** in project files:
   - `src/adguard-api-dotnet/src/AdGuard.ConsoleUI/AdGuard.ConsoleUI.csproj`
   - `src/rules-compiler-dotnet/src/RulesCompiler.Console/RulesCompiler.Console.csproj`
   - `src/rules-compiler-rust/Cargo.toml`
   - `src/rules-compiler-python/pyproject.toml`

2. **Create and push tag**:
   ```bash
   git tag -a v1.0.0 -m "Release version 1.0.0"
   git push origin v1.0.0
   ```

3. **Wait for workflow** (`.github/workflows/release.yml`):
   - Builds .NET executables (Windows, Linux, macOS)
   - Builds Rust binaries (Windows, Linux, macOS)
   - Builds Python wheel package
   - Creates GitHub release with all binaries attached
   - Takes ~15-20 minutes

4. **Verify release** at `https://github.com/BloqrAI/bloqr-lists/releases`:
   - `AdGuard.ConsoleUI-{windows,linux,macos}.{zip,tar.gz}`
   - `RulesCompiler.Console-{windows,linux,macos}.{zip,tar.gz}`
   - `rules-compiler-rust-{windows,linux,macos}.{zip,tar.gz}`
   - `rules_compiler-*.whl` (Python wheel)

### Build Characteristics
- **.NET**: Self-contained, single-file, trimmed (no runtime required)
- **Rust**: LTO enabled, debug symbols stripped, optimized size
- **Python**: Universal wheel (cross-platform)

See `docs/release-guide.md` for full process.

## Documentation

- **API Reference**: `docs/api/` - Generated from OpenAPI spec
- **Guides**: `docs/guides/` - consoleui-architecture.md, api-client-usage.md
- **Comparison**: `docs/compiler-comparison.md` - Choose the right compiler
- **Docker**: `docs/docker-guide.md` - Container development setup