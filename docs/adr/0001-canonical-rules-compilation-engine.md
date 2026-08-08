# ADR 0001: Canonical Rules-Compilation Engine Strategy Across Languages

**Status**: Accepted
**Date**: 2026-08-04
**Related**: #256 (epic), #262 (this ADR's tracking issue), #279 (implementation), [bloqr-compiler#2200](https://github.com/BloqrAI/bloqr-compiler/issues/2200)

**Update (2026-08-08)**: the package described throughout this ADR as `@jk-com/adblock-compiler` has been renamed to `@bloqr/compiler-core`. `@jk-com` was a personal-project JSR scope; all Bloqr JSR packages now live under `@bloqr`. This is a naming/scope change only — none of the reasoning or decisions below changed. References to `@jk-com/adblock-compiler` in the narrative below are left as-is since they accurately describe the package's name at the time these events occurred; treat them as `@bloqr/compiler-core` going forward. The "not yet published" `@bloqr/compiler` mentioned below as the commercial product's own package will not happen — `bloqr-compiler` is instead adopting `@bloqr/compiler-core` as a JSR dependency rather than publishing its own package (see repo restructuring plan, PR #284).

## Context

This repository ships four rules-compiler implementations (TypeScript, .NET, Python, Rust). The epic's premise — "every compiler needs identical features" — assumed a shared engine already existed. Research found the opposite: **two divergent compiler cores**.

- TypeScript (`src/rules-compiler-typescript/`, since retired) called a package published to JSR as `@jk-com/adblock-compiler`, in-process.
- .NET (`RulesCompiler/Services/FilterCompiler.cs`), Python, and Rust each independently shelled out to the `@adguard/hostlist-compiler` npm CLI via subprocess — locate the binary, fall back to `npx @adguard/hostlist-compiler`, same `--config`/`--output` args, duplicated three times with no shared implementation.

### First finding, later corrected

An initial pass concluded `@jk-com/adblock-compiler` was already "our own package" — a SOLID-refactored rewrite depending on AdGuard's `agtree` parsing primitive but owning its own orchestration — and that TypeScript already dogfooded it, so retargeting .NET/Python/Rust at the same JSR package would be a straightforward fix.

Deeper investigation corrected this. `@jk-com/adblock-compiler` on JSR (versions 0.6.0 through 0.96.0) was actually snapshots of **`bloqr-compiler`**, Bloqr's separate commercial product (`github.com/BloqrAI/bloqr-compiler`) — a full-featured, Cloudflare-Workers-deployed system with AST-level parsing via `@adguard/agtree`, a plugin system, diff reports, conflict detection, analytics, and observability integrations. It was never `rules-compiler-typescript`'s own code; that package had always been a thin CLI/config/chunking wrapper around whatever `@jk-com/adblock-compiler` resolved to.

Retargeting .NET/Python/Rust at that JSR package as originally planned would have meant dogfooding the commercial product's full surface area — including its `@adguard/agtree` dependency — across every open-source compiler in this repo. That's the coupling this ADR exists to avoid.

## Decision

**Extract a minimal, dependency-free compilation engine and publish *that* as `@jk-com/adblock-compiler`, then dogfood it across all four languages.**

Concretely:

1. `src/adblock-compiler-core/` was created by extracting only the AGTree-free, non-commercial modules from `bloqr-compiler`'s core: transformations, downloader, formatters, compiler orchestration, configuration schemas, and a Deno-native platform abstraction layer.
2. Two modules — `RuleUtils` (rule classification: comment/exception/hosts-rule detection, hostname extraction) and `ValidateTransformation` (DNS-blocker rule validation) — were AGTree-coupled even in bloqr-compiler's "core" transformation pipeline (not just its commercial-only features). These were hand-written as string/regex-based reimplementations with no third-party AdGuard library dependency, rather than ported verbatim.
3. `src/adblock-compiler-core/` was published as `@jk-com/adblock-compiler` v1.0.0 (reclaiming the JSR namespace from the interim commercial snapshots).
4. `src/rules-compiler-typescript/` (the old wrapper) was retired; its console UI, tests, and CLI/config/chunking orchestration layer were ported into `adblock-compiler-core`.
5. .NET, Python, and Rust were retargeted (#279) to shell out to `deno run jsr:@jk-com/adblock-compiler/cli` instead of `hostlist-compiler`/`npx @adguard/hostlist-compiler`.

Going forward there is one canonical engine — `@jk-com/adblock-compiler` — invoked in-process by TypeScript consumers and via `deno run` subprocess by .NET/Python/Rust, with `@adguard/hostlist-compiler` demoted to undocumented legacy fallback rather than the primary target.

### Why not dogfood `bloqr-compiler` directly

`bloqr-compiler`'s AGTree coupling isn't confined to its commercial-only features (AST viewer, diff, plugins) — it reaches into modules that would otherwise be shared core (`RuleUtils`, `ValidateTransformation`). Dogfooding it as-is would mean every open-source compiler in this repo inherits a third-party AdGuard library dependency and a large surface of Cloudflare-specific code it doesn't need. [bloqr-compiler#2200](https://github.com/BloqrAI/bloqr-compiler/issues/2200) tracks decoupling AGTree behind a formal interface in the commercial product itself — a prerequisite for a possible future Rust port of that parsing layer — but that work is independent of and doesn't block this decision.

### Backporting

Performance improvements and core-engine bug fixes discovered in `bloqr-compiler` may be selectively backported into `adblock-compiler-core` when they don't require an AdGuard library or commercial-only infrastructure. See `docs/backporting-policy.md` for the criteria and process. This is explicitly a one-way, pull-based relationship — `adblock-compiler-core` is not obligated to track every `bloqr-compiler` change, and `bloqr-compiler` is not obligated to backport from the open-source engine.

## Still open (not decided by this ADR)

- **Long-term Rust-core consolidation**: whether to further consolidate onto a single Rust core (native `cdylib`/`staticlib` for .NET P/Invoke and the Rust CLI, `wasm32` for Cloudflare Workers) remains an open architectural question. `@jk-com/adblock-compiler`'s TypeScript/Deno implementation is not being declared the permanent long-term engine for every runtime — it's the near-term fix to the subprocess-target fragmentation described above.
- **Python compiler's future**: the Python compiler has no unique runtime it serves — nothing in this repo targets Cloudflare or requires Python specifically. Whether to keep it at full feature parity, freeze it, or deprecate it is a deliberate decision this ADR does not make.

## Consequences

- All four compilers now depend on Deno being installed to invoke the compilation engine (previously Node.js, via `hostlist-compiler`/`npx`). This is a new toolchain dependency for the .NET, Python, and Rust compilers that wasn't previously documented as a prerequisite for them.
- `@jk-com/adblock-compiler` publishing to JSR requires a one-time manual step (linking the package to this GitHub repository via the JSR web UI) before the `publish-jsr.yml` workflow's OIDC-based publishing will succeed. This cannot be automated and needs the account owner.
- Consumers who depended on the JSR package for AST-level features (`AGTreeParser`, plugin system, diff reports) — i.e., anyone who was pinned to the interim `bloqr-compiler` snapshots at versions 0.6.0–0.96.0 — will not find those in v1.0.0+. Those features remain in the commercial `@bloqr/compiler` product (not yet published).
