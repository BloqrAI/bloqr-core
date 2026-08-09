# Changelog

All notable changes to `@bloqr/compiler-core` (formerly `@jk-com/adblock-compiler`) are documented here.

## [Unreleased]

### Added

- **cli**: Add Bun as a formally supported runtime target (`src/mod.bun.ts`, exported as `./bun`) alongside Deno. `getVersionInfo()`/`showVersion()` now correctly identify the Bun runtime (`Runtime: Bun x.y.z`) instead of erroring, `main()`'s default argument list and `findDefaultConfig()`'s default base path use `node:process` when the `Deno` global isn't present, and environment-variable reads (`DEBUG`, `LOG_LEVEL`, `LOG_FORMAT`, `LOG_MODULE_OVERRIDES`, `LOG_STRUCTURED`, plus `EnvConfigurationSource`) fall back to `process.env` instead of silently returning nothing. CI (`bun-support` job in `.github/workflows/typescript.yml`) installs this package's JSR/npm dependencies for Bun and runs CLI + library smoke tests against real Bun on every PR. See `README.md`'s "Bun (Supported)" section.
- **fix**: `ShutdownHandler` (`orchestration/shutdown.ts`) previously called `globalThis.addEventListener('unhandledrejection', ...)` unconditionally, which Node.js does not implement at all (Bun does, but the module was documented "Deno-only" and untested there) — this would have thrown under Node.js on every CLI invocation reaching `initializeShutdownHandler()`. Signal handling (`SIGTERM`/`SIGINT`/`SIGHUP`) and unhandled-rejection reporting now use `process.on(...)` when the `Deno` global isn't present.
- **docs**: `scripts/check-symbol-docs.ts` (`deno task lint:docs`), a new CI-gated check (wired into both `typescript.yml` and `publish-jsr.yml`) that walks `deno doc --json` for every published entrypoint and fails below 98% JSDoc coverage across the full public API — top-level exports plus enum members and public interface/class properties and methods, which `deno doc --lint` alone doesn't check. Added because PR #310 dropped this package's JSR "symbol documentation" score from ~88% to 61% by adding several undocumented enum members that no existing check caught; those gaps (plus a handful of pre-existing ones) are now documented and this regression class is CI-enforced going forward.

### Changed

- **Breaking**: package renamed from `@jk-com/adblock-compiler` to `@bloqr/compiler-core`. `@jk-com` was a personal-project JSR scope; all Bloqr JSR packages (this one, and future ones like `@bloqr/diagnostics`) now live under the `@bloqr` scope. No functional changes — same package contents, same exports, just a new name and scope. See `README.md`'s Architecture section for the full story.

## [1.1.0] - 2026-08-09

### Added

- **compiler-core**: formally support Bun as a runtime target (#310)
- org-wide per-package JSR versioning strategy (#295)

### Fixed

- make sync-version.ts JSONC-safe (#299)

## [1.0.0] - 2026-08-04

Initial release under this package's new architecture. `@jk-com/adblock-compiler` is now the extracted, dependency-free core compilation engine from this repository (`src/adblock-compiler-core/`), superseding the previous `src/rules-compiler-typescript/` proof-of-concept, which is retired.

### Changed

- **Breaking**: the JSR package `@jk-com/adblock-compiler` now resolves to this engine, not the commercial `bloqr-compiler` product. Versions 0.6.0–0.96.0 of this JSR namespace were snapshots of the commercial compiler; that product continues at `@bloqr/compiler` (not yet published) instead. See `README.md`'s Architecture section for the full story.
- Rule classification (`RuleUtils`) and rule validation (`ValidateTransformation`) are hand-written, string/regex-based implementations — no `@adguard/agtree` or other third-party AdGuard library dependency, by design.

### Added

- Core compilation engine: `FilterCompiler`, `SourceCompiler`, `compile()`, transformation pipeline (`RemoveComments`, `Compress`, `RemoveModifiers`, `Validate`, `ValidateAllowIp`, `Deduplicate`, `InvertAllow`, `RemoveEmptyLines`, `TrimLines`, `InsertFinalNewLine`, `ConvertToAscii`, `Exclude`, `Include`)
- Downloader with preprocessor directive support (`!#if`, `!#include`, etc.)
- Output formatters: adblock, hosts, dnsmasq, DoH, PiHole, Unbound, JSON, hostname-list
- Deno-native platform abstraction (HTTP fetcher, composite fetcher, pre-fetched content)
- Minimal no-op-by-default diagnostics/tracing seam
- Orchestration layer (`./orchestration`): multi-format (JSON/YAML/TOML) config reading, chunked parallel compilation, SHA-384 hash verification, structured logging, graceful shutdown, CLI
- Interactive console (`./console`)
- High-level builder API (`./lib`): `RulesCompiler`, `ConfigurationBuilder`

### Not included (by design)

- `@adguard/agtree`-based AST parsing/linting — see [bloqr-compiler#2200](https://github.com/BloqrAI/bloqr-compiler/issues/2200)
- Plugin system, diff reports, conflict detection, rule optimizer — commercial-only, remain in `@bloqr/compiler`
- OpenTelemetry/Sentry observability exporters, Cloudflare Workers deployment (`WorkerCompiler`, `BrowserFetcher`, `FeatureFlagService`) — commercial-only
