# Repository Guidelines

## Project Structure & Module Organization

- `data/` contains the tracked filter list (`data/output/adguard_user_filter.txt`) and compiler configuration files.
- `src/` contains the multi-language toolchain:
  - `src/rules-compiler-*` (TypeScript/Deno, .NET, Python, Rust, shell) compilers that use `@bloqr/compiler-core`.
  - `src/adguard-api-dotnet/`, `src/adguard-api-typescript/`, and `src/adguard-api-rust/` SDKs + interactive clients for the AdGuard DNS API.
  - `src/adguard-api-powershell/` PowerShell modules and Pester tests.
- `docs/` holds guides and reference documentation.

## Build, Test, and Development Commands

- Compile rules (any platform): `./src/rules-compiler-shell/bash/compile-rules.sh -c data/Config/config.yaml -r` (see `src/rules-compiler-shell/`).
- TypeScript compiler (`src/adblock-compiler-core/`):
  - `deno cache src/mod.ts` — cache dependencies
  - `deno task compile` — compile rules
  - `deno task lint` — Deno lint
  - `deno task test` — Deno tests
- TypeScript API client (`src/adguard-api-typescript/`):
  - `deno task start` — interactive CLI
  - `deno task test` — run tests
- .NET (`src/rules-compiler-dotnet/`, `src/adguard-api-dotnet/`): `dotnet restore`, `dotnet build`, `dotnet test`
- Python (`src/rules-compiler-python/`): `pip install -e ".[dev]"`, `pytest`, `ruff check .`, `mypy .`
- Rust (`src/rules-compiler-rust/`, `src/adguard-api-rust/`): `cargo build`, `cargo test`, `cargo fmt`, `cargo clippy`
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

- Prefer Conventional Commit style when practical (examples: `feat(rules-compiler-python): ...`, `docs(readme): ...`); short imperative messages like `Refactor: ...` are also used.
- PRs should include: a clear description, linked issue(s) when applicable, and test evidence (paste output or CI link). Include screenshots for website/UI changes.

## Security & Configuration Notes

- Follow `SECURITY.md` for vulnerability reporting.
- Secrets (e.g., AdGuard API key) must come from environment variables/config files and never be committed.

