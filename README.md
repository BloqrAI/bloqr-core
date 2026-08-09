# Bloqr Core

A multi-language toolkit for compiling and validating AdGuard-syntax ad-blocking filter rules. Four independent rules compilers (TypeScript, C#/.NET, Python, Rust), bash/zsh shell scripts, a PowerShell toolkit, a Rust validation library, and a Gatsby documentation site all live here and share one configuration schema.

🚀 **Active development** — multi-language support, a Docker development environment, and CI/CD coverage across every component.

## What's in this repo

- **Rules compilers** for TypeScript/Deno, C#/.NET, Python, and Rust, plus bash/zsh shell scripts — all reading the same JSON/YAML/TOML configuration schema and producing identical output.
- **`@bloqr/compiler-core`** (`src/adblock-compiler-core/`) — the canonical, dependency-free compilation engine, published on [JSR](https://jsr.io/@bloqr/compiler-core). The .NET, Python, and Rust compilers shell out to it via Deno rather than reimplementing compilation logic.
- **RulesCompiler PowerShell toolkit** (`src/rules-compiler-powershell/`) — class-based modules (`Common`, `RulesCompiler`, `AdGuardWebhook`) with Pester test suites.
- **`rules-validator`** (`src/rules-validator/`) — a Rust validation library and CLI for filter/config validation (hash verification, URL security, syntax linting).
- **Documentation website** (`src/website/`) — a Gatsby 5 site that builds guides, API reference, and security docs from `docs/` and this README.

**What moved out:** the compiled filter lists (and their input/archive files) now live in [`BloqrAI/bloqr-blocklists`](https://github.com/BloqrAI/bloqr-blocklists), and the AdGuard DNS API clients (.NET, TypeScript, Rust, PowerShell) plus the Linear import tool now live in [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients). Neither is part of this repo anymore — see [Related repositories](#related-repositories) below.

## Prerequisites

| Requirement | Version | Needed for |
|-------------|---------|------------|
| Deno | 2.0+ | TypeScript compiler; also shelled out to by .NET/Python/Rust |
| .NET SDK | 10.0+ | .NET compiler |
| Python | 3.9+ | Python compiler |
| Rust | 1.85+ | Rust compiler, `rules-validator` |
| PowerShell | 7+ | PowerShell toolkit |
| Docker | 24.0+ | Containerized dev environment (optional) |

## Quick start

```bash
git clone https://github.com/BloqrAI/bloqr-core.git
cd bloqr-core

# Optional: check out the filter-list repo as a sibling directory so the
# sample configs' relative paths (../bloqr-blocklists/...) resolve as-is
git clone https://github.com/BloqrAI/bloqr-blocklists.git ../bloqr-blocklists
```

Then pick a compiler:

### TypeScript (Deno)

```bash
cd src/adblock-compiler-core
deno task compile              # compile with the default config
deno task interactive          # menu-driven interactive mode
deno task test                 # run tests
```

### .NET

```bash
cd src/rules-compiler-dotnet
dotnet restore RulesCompiler.slnx
dotnet run --project src/RulesCompiler.Console -- --config config.yaml
dotnet test RulesCompiler.slnx
```

### Python

```bash
cd src/rules-compiler-python
pip install -e ".[dev]"
rules-compiler -c config.yaml
pytest
```

### Rust

```bash
cd src/rules-compiler-rust
cargo build --release
cargo run -- -c config.yaml
cargo test
```

### Shell (bash/zsh)

```bash
./src/rules-compiler-shell/bash/compile-rules.sh -c config.yaml -r
./src/rules-compiler-shell/zsh/compile-rules.zsh -c config.yaml -r
```

### PowerShell

```powershell
Import-Module ./src/rules-compiler-powershell/RulesCompiler/RulesCompiler.psd1
Invoke-RulesCompiler
```

Every compiler supports JSON, YAML, and TOML configuration, the full transformation set (`Deduplicate`, `Validate`, `RemoveComments`, `Compress`, and more — see [Configuration Reference](docs/configuration-reference.md)), and per-source inclusions/exclusions/transformations.

## Docker development environment

A pre-baked image with all toolchains installed:

```bash
docker build -f Dockerfile.warp -t ad-blocking-dev .
docker run -it -v $(pwd):/workspace ad-blocking-dev
```

Or with Docker Compose:

```bash
docker compose up -d dev            # start the dev environment
docker compose exec dev bash        # shell into it
docker compose --profile compile up # run every compiler once
docker compose --profile test run --rm test   # run all tests
```

## Architecture

```
bloqr-core/
├── src/
│   ├── adblock-compiler-core/    # TypeScript/Deno — canonical @bloqr/compiler-core (JSR)
│   ├── rules-compiler-dotnet/    # C#/.NET 10 — library + Spectre.Console CLI
│   ├── rules-compiler-python/    # Python 3.9+ — pip-installable package + CLI
│   ├── rules-compiler-rust/      # Rust — single-binary CLI, zero runtime deps
│   ├── rules-compiler-shell/     # bash + zsh scripts
│   ├── rules-compiler-powershell/# PowerShell modules + Pester tests
│   ├── rules-validator/          # Rust validation library + CLI
│   └── website/                  # Gatsby 5 documentation site
├── docs/                         # Guides, reference docs, security docs
└── schemas/                      # Shared configuration schema
```

The TypeScript compiler is the only one that implements compilation logic directly — it *is* `@bloqr/compiler-core`. The .NET, Python, and Rust compilers are thin wrappers that shell out to it via Deno, so behavior and output stay identical across languages; the shell scripts and PowerShell toolkit call whichever compiler is available. `rules-validator` provides the shared hash-verification and syntax-validation layer that all of them rely on for security.

`@bloqr/compiler-core` is deliberately separate from Bloqr's commercial `@bloqr/compiler` product ([`BloqrAI/bloqr-compiler`](https://github.com/BloqrAI/bloqr-compiler)), which layers AST tooling, linting, plugins, and Cloudflare Workers deployment on top of this open-source engine — see [`src/adblock-compiler-core/README.md`](src/adblock-compiler-core/README.md#architecture) for the full relationship.

## Related repositories

| Repository | What it holds |
|---|---|
| [`BloqrAI/bloqr-blocklists`](https://github.com/BloqrAI/bloqr-blocklists) | Compiled filter lists and their input/output/archive files (`output/adguard_dns_filter.txt`, etc.) — no longer part of this repo |
| [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) | AdGuard DNS API clients (.NET, TypeScript, Rust, PowerShell) and the Linear import tool — no longer part of this repo |
| [`BloqrAI/bloqr-compiler`](https://github.com/BloqrAI/bloqr-compiler) | Bloqr's commercial compiler, built on top of `@bloqr/compiler-core` |

## Documentation

- [`docs/README.md`](docs/README.md) — full documentation index
- [`docs/getting-started.md`](docs/getting-started.md) — installation and first compilation
- [`docs/WHY_VALIDATION_MATTERS.md`](docs/WHY_VALIDATION_MATTERS.md) — why security validation is mandatory, start here
- [`docs/configuration-reference.md`](docs/configuration-reference.md) — full configuration schema
- [`docs/compiler-comparison.md`](docs/compiler-comparison.md) — feature comparison across all compilers
- [`docs/docker-guide.md`](docs/docker-guide.md) — Docker development environment
- [`docs/release-guide.md`](docs/release-guide.md) — creating releases with automatic binary builds
- [`CLAUDE.md`](CLAUDE.md) / [`.github/copilot-instructions.md`](.github/copilot-instructions.md) — AI agent instructions for working in this repo
- [`src/website/`](src/website/) — the Gatsby site that publishes the above as a browsable documentation site (`npm install && npm run develop` to preview locally)

## Testing

```bash
cd src/adblock-compiler-core && deno task test
cd src/rules-compiler-dotnet && dotnet test RulesCompiler.slnx
cd src/rules-compiler-python && pytest
cd src/rules-compiler-rust && cargo test
cargo test --workspace   # rules-compiler-rust + rules-validator
Invoke-Pester -Path ./src/rules-compiler-powershell -Recurse
```

See [`docs/guides/testing-guide.md`](docs/guides/testing-guide.md) for coverage tooling, CI examples, and troubleshooting.

## CI/CD

GitHub Actions validates every component on push and pull request: `.github/workflows/dotnet.yml`, `typescript.yml`, `python.yml`, `rust-clippy.yml`, `powershell.yml`, `gatsby.yml`, plus consolidated `security.yml` (CodeQL, DevSkim, PSScriptAnalyzer) and `validation-compliance.yml` (runs the Rust validator against fixtures). See [CI/CD Alignment](CLAUDE.md#cicd-alignment) in `CLAUDE.md` for the full list.

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md) for the development workflow, coding standards, and pull request process. Report security issues per [`SECURITY.md`](SECURITY.md) rather than filing a public issue.

## License

See [`LICENSE`](LICENSE).
