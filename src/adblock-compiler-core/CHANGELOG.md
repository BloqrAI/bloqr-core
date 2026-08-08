# Changelog

All notable changes to `@bloqr/compiler-core` (formerly `@jk-com/adblock-compiler`) are documented here.

## [Unreleased]

### Changed

- **Breaking**: package renamed from `@jk-com/adblock-compiler` to `@bloqr/compiler-core`. `@jk-com` was a personal-project JSR scope; all Bloqr JSR packages (this one, and future ones like `@bloqr/diagnostics`) now live under the `@bloqr` scope. No functional changes — same package contents, same exports, just a new name and scope. See `README.md`'s Architecture section for the full story.

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
