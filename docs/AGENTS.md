# Repository Guidelines

## Project Structure & Module Organization

- The tracked filter list and compiler configuration files live in [`BloqrAI/bloqr-blocklists`](https://github.com/BloqrAI/bloqr-blocklists) (formerly `data/` in this repo).
- `src/` contains the multi-language rules-compiler toolchain:
  - `src/compilers/typescript/`, `src/compilers/dotnet/`, `src/compilers/python/`, `src/compilers/rust/` — TypeScript/Deno, .NET, Python, and Rust compilers that use `@bloqr/compiler-core`.
  - `src/compilers/powershell/` — the sole cross-platform scripting-language compiler (PowerShell 7+ runs on Windows/Linux/macOS); class-based modules and Pester tests.
  - `src/validation/` Rust validation library and CLI.
  - `website/` Gatsby documentation site.
- `docs/` holds guides and reference documentation.
- The AdGuard DNS API clients (.NET, TypeScript, Rust, PowerShell) and the Linear import tool moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) and are no longer part of this repo.

## Build, Test, and Development Commands

- Compile rules (any platform): `Invoke-BloqrCompiler -ConfigPath config.json -CopyToRules` (see `src/compilers/powershell/`).
- TypeScript compiler (`src/compilers/typescript/`):
  - `deno cache src/mod.ts` — cache dependencies
  - `deno task compile` — compile rules
  - `deno task lint` — Deno lint
  - `deno task test` — Deno tests
- .NET (`src/compilers/dotnet/`): `dotnet restore CompilerDotnet.slnx`, `dotnet build CompilerDotnet.slnx`, `dotnet test CompilerDotnet.slnx`
- Python (`src/compilers/python/`): `pip install -e ".[dev]"`, `pytest`, `ruff check .`, `mypy .`
- Rust (`src/compilers/rust/`, `src/validation/`): `cargo build`, `cargo test`, `cargo fmt`, `cargo clippy`
- PowerShell (`src/compilers/powershell/`): `Invoke-Pester -Path ./src/compilers/powershell -Recurse`
- Docker dev env: `docker build -f Dockerfile.warp .` (use when you want a pre-baked toolchain).

## Coding Style & Naming Conventions

- Follow the conventions of each language and keep changes scoped to the module you're touching.
- TypeScript/Deno: 2-space indentation, `deno lint` enforced; tests use `*.test.ts` with Deno test.
- .NET: match existing casing (PascalCase types/methods); prefer nullable-safe APIs; keep solutions in `.slnx`.
- Python: `ruff` (line length 100) + `mypy` (typed, strict-ish); tests use `tests/test_*.py`.
- PowerShell: use approved verbs and keep functions discoverable (`Verb-Noun`); PSScriptAnalyzer is run in CI.

## Testing Guidelines

- Add/adjust tests alongside changes (unit tests preferred; integration tests where appropriate).
- Run the closest test suite first (e.g., `deno task test`, `dotnet test`, `pytest`, `cargo test`, `Invoke-Pester`).

## Commit & Pull Request Guidelines

- Prefer Conventional Commit style when practical (examples: `feat(python): ...`, `docs(readme): ...`); short imperative messages like `Refactor: ...` are also used.
- PRs should include: a clear description, linked issue(s) when applicable, and test evidence (paste output or CI link). Include screenshots for website/UI changes.

## Security & Configuration Notes

- Follow `SECURITY.md` for vulnerability reporting.
- Secrets (e.g., AdGuard API key) must come from environment variables/config files and never be committed. API-client secrets now apply to the tools in [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients).
