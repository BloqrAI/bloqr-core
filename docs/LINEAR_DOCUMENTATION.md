# Bloqr List Utils - Linear Documentation

## Project Overview

A comprehensive multi-language toolkit for ad-blocking, network protection, and AdGuard DNS management. Features filter rule compilers in 4 core languages (TypeScript, .NET, Python, Rust) plus PowerShell modules, complete API SDKs in C#, TypeScript, and Rust with interactive console interfaces, a Rust validation library, and shell script wrappers.

| Property | Value |
|----------|-------|
| **License** | GPLv3 |
| **Repository** | [BloqrAI/bloqr-lists](https://github.com/BloqrAI/bloqr-lists) |

---

## Architecture Overview

```
┌───────────────────────────────────────────────────────────────────────┐
│                        Bloqr List Utils                               │
├───────────────────────────────────────────────────────────────────────┤
│                                                                        │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │       @bloqr/compiler-core (src/adblock-compiler-core/)      │  │
│  │       Open-source, dependency-free compilation engine            │  │
│  └────────────────────────────────────────────────────────────────┘  │
│         │              │              │              │                │
│         ▼              ▼              ▼              ▼                │
│  ┌───────────┐  ┌───────────┐  ┌───────────┐  ┌───────────┐          │
│  │TypeScript │  │   .NET    │  │  Python   │  │   Rust    │          │
│  │ Compiler  │  │ Compiler  │  │ Compiler  │  │ Compiler  │          │
│  │(in-process)│  │(shells out)│  │(shells out)│  │(shells out)│         │
│  └───────────┘  └───────────┘  └───────────┘  └───────────┘          │
│         │              │              │              │                │
│         └──────────────┴──────────────┴──────────────┘                │
│                             ▼                                         │
│                    ┌──────────────┐                                   │
│                    │ Filter Rules │                                   │
│                    │    (.txt)    │                                   │
│                    └──────────────┘                                   │
│                                                                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                 │
│  │  API Clients │  │Rules Validator│  │  Website     │                │
│  │ (C#/TS/Rust) │  │    (Rust)     │  │  (Gatsby)    │                │
│  └──────────────┘  └──────────────┘  └──────────────┘                 │
│                                                                        │
└───────────────────────────────────────────────────────────────────────┘
```

---

## Components

### 1. Filter Rules (`/data/`)

**Purpose:** Organize and compile blocking lists for ad, tracker, and malware domains.

#### Input Sources (`../bloqr-blocklists/input/`)

Source location for filter rules before compilation:

- **Local filter files**: Custom rules in adblock or hosts format
  - Place `.txt` or `.hosts` files in this directory
  - Automatic format detection and syntax validation
  - Examples: `custom-rules.txt`, `company-blocklist.txt`

- **Internet source references**: Remote filter lists via URL
  - Create `internet-sources.txt` with one URL per line
  - Common sources: EasyList, StevenBlack hosts, AdGuard filters
  - Downloaded and cached during compilation

**Security & Validation:**
- SHA-384 hash verification for all input files
- Syntax validation before compilation
- Tampering detection via hash comparison
- Error reporting with line numbers

#### Compiled Output (`../bloqr-blocklists/output/`)

Filter lists compiled from `../bloqr-blocklists/input/` sources plus any remote sources configured in the compiler config, in adblock format.

#### Archive Storage (`../bloqr-blocklists/archive/`)

Automated archiving of processed input files for audit and rollback:

- **Automatic/Interactive/Disabled modes**: User-configurable archiving behavior
- **Timestamped snapshots**: Each compilation creates dated archive directory
- **Manifest tracking**: JSON metadata with hashes and compilation stats
- **Retention policy**: Automatic cleanup after 90 days (configurable)

**Use cases:**
- Historical tracking of filter rule changes
- Rollback to previous working configuration
- Compliance and audit requirements
- Verification of what was compiled and when

**Configuration:**
```bash
export ADGUARD_ARCHIVE_MODE=automatic     # or interactive, disabled
export ADGUARD_ARCHIVE_RETENTION_DAYS=90
```

---

### 2. TypeScript Rules Compiler (`/src/adblock-compiler-core/`)

**Purpose:** The canonical `@bloqr/compiler-core` engine — an open-source, dependency-free filter compilation engine published to JSR, that the .NET/Python/Rust compilers below all shell out to.

**Technology Stack:**
- TypeScript
- Deno 2.0+
- Deno test (testing)
- No third-party AdGuard library (`@adguard/agtree`, etc.) — rule classification is string/regex-based

**Key Files:**
| File | Description |
|------|--------------|
| `src/index.ts` | Core compilation engine (compiler, transformations, downloader, formatters) |
| `src/orchestration/` | CLI/config/chunking wrapper layer |
| `src/console/` | Interactive terminal UI |
| `src/lib/` | Builder-pattern library API |
| `deno.json` | Deno configuration, tasks, and JSR export map |

**Transformations Applied:**
- Deduplication, Compression, Validation, ASCII conversion, whitespace cleanup, and 6 more (11 total)

**Configuration Sources:**
- Local files (`../bloqr-blocklists/input/`) and remote URLs, per `compiler-config.json`

---

### 3. .NET Rules Compiler (`/src/rules-compiler-dotnet/`)

**Purpose:** C# library and Spectre.Console CLI for filter compilation, shelling out to `@bloqr/compiler-core` via Deno.

**Technology Stack:**
- .NET 10
- C#
- xUnit (testing)

**Key Projects:**
| Project | Description |
|---------|--------------|
| `Bloqr.Compiler.Abstractions` | Interfaces, event-args, and model/DTO types |
| `Bloqr.Compiler.Core` | Configuration reading/validation, chunking, file-locking, plugin management, compilation pipeline |
| `RulesCompiler` | Compiler-specific services (e.g. `FilterCompiler`) referencing both of the above |
| `RulesCompiler.Console` | Spectre.Console interactive and CLI frontend |

---

### 4. Python and Rust Rules Compilers (`/src/rules-compiler-python/`, `/src/rules-compiler-rust/`)

**Purpose:** pip-installable and single-binary compiler implementations, both shelling out to `@bloqr/compiler-core` via Deno.

**Technology Stack:**
- Python 3.9+ (pytest, mypy, ruff)
- Rust 1.85+ (cargo test, clippy, LTO release builds)

---

### 5. AdGuard DNS API Clients (`/src/adguard-api-dotnet/`, `/src/adguard-api-typescript/`, `/src/adguard-api-rust/`)

**Purpose:** SDKs for programmatic access to AdGuard DNS API v1.15, auto-generated from OpenAPI specification.

**Key Files:**
| File | Description |
|------|--------------|
| `api/openapi.json` | Centralized AdGuard DNS API v1.15 spec (primary) |
| `src/AdGuard.ConsoleUI/` | Spectre.Console interactive CLI (.NET) |

**API Coverage:**
- Account management (limits, usage)
- Device management (CRUD operations)
- DNS server profiles
- Dedicated IPv4 address allocation
- Filter lists management
- Query logging and analysis
- DNS statistics and reporting
- Web services configuration

**Authentication:** API Key and Bearer Token (OAuth)

---

### 6. Rules Validator (`/src/rules-validator/`)

**Purpose:** Rust library and CLI for validating filter and configuration files, with a real `extern "C"` FFI surface for embedding in .NET via P/Invoke.

**Key Projects:**
| Project | Description |
|---------|--------------|
| `rules-validator-core` | Core validation logic + FFI exports |
| `rules-validator-cli` | Command-line validation tool |

---

### 7. Documentation Website (`/src/website/`)

**Purpose:** Gatsby-based documentation site rendering the repository's `docs/` markdown files, plus a handful of static pages describing the toolkit.

**Technology Stack:**
- Gatsby 5
- React 18

**Content Structure:**
| Directory | Content |
|-----------|---------|
| `src/pages/` | Static pages (home, getting started, compiler pages) |
| `src/templates/doc.js` | Renders each `docs/**/*.md` file as a page |
| `src/components/` | Shared React components (Layout, nav) |

**Deployment:** GitHub Pages

---

### 8. PowerShell Modules (`/src/rules-compiler-powershell/`, `/src/adguard-api-powershell/`)

**Purpose:** Automation and orchestration scripts/modules for filter compilation and webhook invocation.

**Key Files:**
| File | Purpose |
|------|---------|
| `rules-compiler-powershell/RulesCompiler/RulesCompiler.psd1` | Modern class-based rules compiler module manifest |
| `adguard-api-powershell/Invoke-RulesCompiler.psm1` | Legacy rules compiler PowerShell module |
| `adguard-api-powershell/RulesCompiler-Harness.ps1` | Interactive test harness |

---

## Directory Structure

```
bloqr-lists/
├── .github/
│   ├── workflows/           # CI/CD pipelines
│   └── ISSUE_TEMPLATE/      # Issue templates
├── docs/
│   ├── README.md            # Documentation index
│   ├── api/                 # Auto-generated API docs
│   └── guides/               # Usage guides
├── data/
│   ├── input/                   # Source filter lists
│   │   ├── README.md            # Input directory documentation
│   │   ├── example-custom-rules.txt  # Example local rules
│   │   └── internet-sources.txt.example  # Example remote sources
│   ├── output/                  # Compiled filter output
│   └── archive/                 # Archived processed files
│       ├── README.md            # Archive documentation
│       └── .gitignore           # Ignore archive contents
├── src/
│   ├── adblock-compiler-core/    # @bloqr/compiler-core (TypeScript)
│   ├── rules-compiler-dotnet/    # .NET compiler
│   ├── rules-compiler-python/    # Python compiler
│   ├── rules-compiler-rust/      # Rust compiler
│   ├── rules-compiler-shell/     # Bash/Zsh wrappers
│   ├── rules-compiler-powershell/# Modern PowerShell modules
│   ├── rules-validator/          # Rust validation library + CLI
│   ├── adguard-api-dotnet/       # AdGuard DNS API C# client
│   ├── adguard-api-typescript/   # AdGuard DNS API TypeScript client
│   ├── adguard-api-rust/         # AdGuard DNS API Rust client
│   ├── adguard-api-powershell/   # Legacy PowerShell API client
│   ├── website/                  # Gatsby documentation site
│   └── linear/                   # This tool
├── LICENSE                  # GPLv3
├── README.md                # Main documentation
└── SECURITY.md              # Security policy
```

---

## CI/CD Pipelines

| Workflow | File | Purpose |
|----------|------|---------|
| TypeScript | `typescript.yml` | Build/test/lint `adblock-compiler-core` and other Deno projects |
| .NET | `dotnet.yml` | Build .NET projects, run xUnit tests |
| Python | `python.yml` | Build/test the Python compiler |
| Rust | `rust-clippy.yml` | Build/test/lint the Rust workspace |
| PowerShell | `powershell.yml` | Pester tests and PSScriptAnalyzer |
| Publish JSR | `publish-jsr.yml` | Publish `adblock-compiler-core` to `@bloqr/compiler-core` on JSR |
| Gatsby | `gatsby.yml` | Build website, deploy to GitHub Pages |
| Docker | `docker-image.yml` | Build the `Dockerfile.warp` dev image |
| Security | `security.yml` | Consolidated CodeQL, DevSkim, PSScriptAnalyzer scanning |
| Build Scripts Tests | `build-scripts-tests.yml` | Exercise root `build.sh`/`build.ps1` |
| Validation Compliance | `validation-compliance.yml` | Run the Rust validation CLI against fixtures |
| Release | `release.yml` | Build and publish release binaries |
| Claude AI | `claude-code-review.yml` | AI-powered code review on PRs |

---

## Technology Stack Summary

### Compilers
| Technology | Version | Purpose |
|------------|---------|---------|
| Deno | 2.0+ | TypeScript runtime, `@bloqr/compiler-core` |
| .NET | 10.0 | .NET compiler, API client, Console UI |
| Python | 3.9+ | Python compiler |
| Rust | 1.85+ | Rust compiler, validation library, API client |
| PowerShell | 7+ | PowerShell modules and scripts |

### Frontend
| Technology | Version | Purpose |
|------------|---------|---------|
| Gatsby | 5 | Documentation site generator |
| React | 18 | UI framework |

### Security & Quality
| Tool | Purpose |
|------|---------|
| CodeQL | Static code analysis |
| DevSkim | Security scanning |
| Deno lint / clippy / ruff / PSScriptAnalyzer | Per-language linting |

---

## API Documentation

### Available APIs

| API Class | Endpoints |
|-----------|-----------|
| `AccountApi` | Account limits, usage |
| `AuthenticationApi` | API key management, OAuth |
| `DevicesApi` | Device CRUD operations |
| `DNSServersApi` | DNS server profiles |
| `DedicatedIPAddressesApi` | IPv4 address allocation |
| `FilterListsApi` | Filter list management |
| `QueryLogApi` | Query logging |
| `StatisticsApi` | DNS statistics |
| `WebServicesApi` | Web service configuration |

### Data Models

Key models include:
- `Device` - Device configuration
- `DNSServer` - DNS server profile
- `FilterList` - Filter list definition
- `QueryLogItem` - Query log entry
- `Statistics` - DNS statistics data

Full API documentation available in `/docs/api/`.

---

## Quick Start

### Prerequisites
- Deno 2.0+
- .NET 10 SDK
- Python 3.9+
- Rust 1.85+
- PowerShell 7+

### Filter Compiler
```bash
cd src/adblock-compiler-core
deno task compile
```

### API Client
```bash
cd src/adguard-api-dotnet
dotnet restore src/AdGuard.ApiClient.sln
dotnet build src/AdGuard.ApiClient.sln
dotnet test src/AdGuard.ApiClient.sln
```

### Website
```bash
cd src/website
npm install
npm run develop
```

---

## Testing

### TypeScript Tests
```bash
cd src/adblock-compiler-core
deno task test
```

### .NET Tests
```bash
cd src/rules-compiler-dotnet
dotnet test RulesCompiler.slnx
```

### Python Tests
```bash
cd src/rules-compiler-python
pytest
```

### Rust Tests
```bash
cargo test --workspace
```

### PowerShell Tests
```powershell
Invoke-Pester -Path ./src/rules-compiler-powershell -Recurse
```

---

## Configuration

### Filter Compiler (`compiler-config.json`)
```json
{
  "name": "My Filter List",
  "sources": [
    { "name": "EasyList", "source": "https://easylist.to/easylist/easylist.txt" }
  ],
  "transformations": ["Deduplicate", "Validate", "InsertFinalNewLine"]
}
```

See [Configuration Reference](configuration-reference.md) for the complete schema.

---

## Security

- **CodeQL Analysis:** Automated security scanning on all pushes
- **DevSkim Scanning:** Regular security vulnerability checks
- **Authentication:** Supports API Key and OAuth Bearer tokens
- **Mandatory validation:** all compilers enforce hash and syntax validation on filter sources — see [Why Validation Matters](WHY_VALIDATION_MATTERS.md)

See `SECURITY.md` for vulnerability reporting guidelines.

---

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Run the relevant language's tests
5. Submit a pull request

All PRs automatically receive:
- Claude AI code review
- CodeQL security analysis
- CI/CD pipeline validation

---

## License

This project is licensed under the GNU General Public License v3.0 (GPLv3).

See `LICENSE` file for full terms.

---

## Links & Resources

- **API Documentation:** `/docs/api/`
- **Usage Guides:** `/docs/guides/`
- **Security Policy:** `SECURITY.md`
- **AdGuard DNS:** https://adguard-dns.io

---

## Roadmap & Future Work

### Potential Enhancements
- [ ] Additional filter sources integration
- [ ] Real-time filter update notifications
- [ ] Dashboard for statistics visualization
- [ ] Mobile app integration
- [ ] Kubernetes deployment manifests

---

*This documentation is maintained as part of the bloqr-lists repository and should be kept in sync with codebase changes.*
