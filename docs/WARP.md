# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

Project scope
- This repo houses the multi-language rules-compiler toolkit: TypeScript/Deno, C#/.NET 10, Python, and Rust compilers, bash/zsh shell scripts, a PowerShell toolkit, the Rust validation library (`src/validation/`), and the Gatsby documentation site (`website/`, at the repo root).
- The AdGuard DNS API clients (.NET, TypeScript, Rust, PowerShell) and the Linear import tool moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) and are no longer part of this repo.
- CI pipelines (GitHub Actions) validate the .NET, TypeScript/Deno, Python, Rust, and PowerShell compilers, plus the Gatsby site. Keep local commands aligned with the workflows below.

Common commands (build, lint, test)
TypeScript/Deno – rules compiler (src/adblock-compiler-core)
- Cache deps: cd src/adblock-compiler-core && deno cache src/mod.ts
- Type-check: deno check src/mod.ts
- Lint: deno task lint
- Unit tests: deno task test
- Coverage: deno task test:coverage
- Compile rules: deno task compile
  Notes
  - Reads compiler configuration and writes compiled rules. The canonical filter list lives in [`BloqrAI/bloqr-blocklists`](https://github.com/BloqrAI/bloqr-blocklists) (`output/adguard_dns_filter.txt`), not this repo.

.NET – rules compiler (src/rules-compiler-dotnet)
- Restore/build/test: cd src/rules-compiler-dotnet; dotnet restore RulesCompiler.slnx; dotnet build RulesCompiler.slnx; dotnet test RulesCompiler.slnx
- Run the console UI: dotnet run --project src/RulesCompiler.Console/RulesCompiler.Console.csproj

Python – rules compiler (src/rules-compiler-python)
- Install: cd src/rules-compiler-python && pip install -e ".[dev]"
- Test: pytest
- Lint/type-check: ruff check .; mypy .

Rust – rules compiler (src/rules-compiler-rust)
- Build/test: cd src/rules-compiler-rust && cargo build && cargo test
- Run: cargo run -- -c config.json

PowerShell scripts (src/rules-compiler-powershell)
- Static analysis (same as CI): Invoke-ScriptAnalyzer -Path src/rules-compiler-powershell -Recurse
- Tests: Invoke-Pester -Path ./src/rules-compiler-powershell -Recurse

Running a single test
- TypeScript/Deno
  - By file: cd src/adblock-compiler-core && deno test src/cli.test.ts
  - All tests: deno task test
- .NET (xUnit)
  - By class pattern (rules compiler): cd src/rules-compiler-dotnet && dotnet test RulesCompiler.slnx --filter "FullyQualifiedName~RulesCompilerServiceTests"
  - By class pattern (shared library): cd src/compiler-common-dotnet && dotnet test CompilerCommon.slnx --filter "FullyQualifiedName~ConfigurationValidatorTests"
- Python: pytest -k "test_read_yaml"
- Rust: cargo test test_count_rules

High-level architecture and structure
- Filter rules ([`BloqrAI/bloqr-blocklists`](https://github.com/BloqrAI/bloqr-blocklists))
  - `output/adguard_dns_filter.txt` is the compiled, tracked filter list consumed by AdGuard DNS. It is no longer part of this repo.
- Rules compilers (`src/`)
  - `src/adblock-compiler-core/` — Deno/TypeScript wrapper around `@bloqr/compiler-core`, published on JSR.
  - `src/rules-compiler-dotnet/` — .NET 10 library + Spectre.Console CLI.
  - `src/rules-compiler-python/` — pip-installable package with CLI and API.
  - `src/rules-compiler-rust/` — single-binary CLI with zero runtime deps.
  - `src/rules-compiler-shell/` — bash and zsh scripts for compiling rules without a language runtime.
  - `src/rules-compiler-powershell/` — class-based PowerShell modules with Pester tests.
- Validation
  - `src/validation/` — Rust library (published to crates.io as `bloqr-validator-core`) and CLI (published as `bloqr-validator-core-cli`) for filter/config validation.
- Documentation site
  - `website/` — Gatsby 5 site (repo root, not under `src/`) sourcing content from `docs/` and repo root.

Notes pulled from existing docs
- Root README lists prerequisites: .NET 10, Deno 2.0+, Python 3.9+, Rust 1.85+, and PowerShell 7+. It also documents the typical steps to compile filters with each toolchain.
- AdGuard DNS API client usage now lives in the [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) READMEs.

Alignment with CI
- .github/workflows/dotnet.yml builds and tests `RulesCompiler.slnx` with .NET 10.
- .github/workflows/typescript.yml validates the TypeScript/Deno compiler with `deno check`, `deno lint`, and `deno test`.
- .github/workflows/python.yml, .github/workflows/rust-clippy.yml, and .github/workflows/powershell.yml cover the remaining compilers.
- .github/workflows/gatsby.yml builds the documentation site.
