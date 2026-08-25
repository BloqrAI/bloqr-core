# ADR 0005: Browser-Syntax Validation Engine for `bloqr-validator-core`

**Status**: Accepted
**Date**: 2026-08-25
**Related**: #434 (this issue, Wave 0 of #432), #432 (EPIC: Dual-engine compilation — this issue gates its closure), `docs/adr/0002-aglint-integration-strategy.md` (what stays commercial: AGLint auto-fix, tsurlfilter deep-semantic validation), `docs/adr/0003-adguard-hostlist-compatibility.md` (the DNS-mode `syntax.rs` grammar this ADR extends, not replaces)

## Context

`bloqr-validator-core::syntax` deliberately rejects browser/client-side syntax — cosmetic rules (`##.ad-banner`), extended CSS, scriptlet injection, and browser-only `$` modifiers (`$script`, `$third-party`, …) — because it was written to validate only what a DNS-level blocker can act on (ADR 0003). All five language wrappers run this validator fail-closed over compiled output, so every dual-engine browser compile (epic #432) has been aborting with 100% of its cosmetic rules flagged invalid, worked around per-wrapper with an `--allow-unvalidated-output`/`AllowUnvalidatedOutput` escape hatch. #434 exists to remove that requirement by teaching the validator browser syntax natively, and explicitly **gates #432's closure** until it does.

### The build-vs-adopt question

#434's filing, and a follow-up comment from the org, named a concrete crate to evaluate before hand-writing a browser grammar: **`BloqrAI/bloqr-enginelib`**, an unmodified fork of Brave's `adblock-rust` (crate name still `adblock`, currently pinned at upstream v0.13.3, MPL-2.0). Two checklist items gated adopting it:

1. **License compatibility** (MPL-2.0 dependency vs. this repo's GPL-3.0).
2. **Grammar agreement**, now three-way per the follow-up comment: AGTree (what the TypeScript compiler/engines layer parses with, `src/compilers/typescript/src/engines/`), `bloqr-enginelib`'s own grammar, and the DNS path already in `bloqr-validator-core`.

## Investigation

### License (checklist item 1) — confirmed compatible, doc gap closed

MPL-2.0 is a file-level weak-copyleft license. Linking an MPL-2.0 crate (even statically, via Cargo) into a GPL-3.0 binary is a well-established compatible combination: MPL-2.0 §3.3 contains an explicit "Larger Works" carve-out permitting distribution under a secondary license (including GPL) for the combined work, provided the MPL-licensed files themselves stay under MPL. This is the same conclusion the org's own research reached independently before this issue existed (see #434's comment). `docs/architecture/licensing-strategy.md` did not yet reflect this finding; updated as part of this change (see "Docs updated" below) regardless of the adopt/hand-roll outcome, since the confirmation itself is real and durable even though the crate ultimately wasn't taken as a dependency.

### Grammar agreement (checklist item 2) — not reached; blocked upstream

Building a fixture corpus from this repo's own dual-engine test suites (`src/compilers/typescript/src/orchestration/compiler.dual-engine.test.ts`, `src/compilers/typescript/src/engines/browser/BrowserSyntaxCompiler.test.ts`) surfaced rules like:

```
##.ad-banner
example.com##.ad-banner
tracker.com#@#.allowed-banner
||ads.example.org^$script
```

Before this corpus could be run against `bloqr-enginelib` to check three-way parity with AGTree and the DNS path, a **toolchain incompatibility blocked the crate from building in this workspace at all**:

- `bloqr-enginelib`'s `rust-toolchain.toml` pins `channel = "1.97"` and its `Cargo.toml` declares `edition = "2024"`. Its source uses let-chain syntax (`if ... && let Some(x) = ...`) and `usize::is_multiple_of` as stable APIs — both gated behind nightly-only feature flags on any toolchain this workspace can actually use.
- `bloqr-core`'s own `rust-toolchain.toml` pins `channel = "1.86"` and `Cargo.toml` declares `rust-version = "1.86"`, `edition = "2021"` workspace-wide.
- Attempting `cargo build` with `adblock` (the `bloqr-enginelib` package name) as a path/git dependency of `bloqr-validator-core` fails with 13 `E0658` "unstable feature" errors, entirely inside `bloqr-enginelib`'s own source — not a transitive-dependency or feature-flag issue that could be worked around from this crate's `Cargo.toml`.

Bumping this workspace's whole MSRV from 1.86 to 1.97 to accommodate one dependency is a large, invasive, cross-cutting change: it touches every crate in the workspace's toolchain contract, is unrelated to what #434 was scoped to deliver, and was not something either the issue or its follow-up comment anticipated or authorized (`bloqr-enginelib` was flagged specifically as "currently uncustomized/pinned at upstream v0.13.3" — i.e. evaluated as a drop-in, not as license to also uplift the workspace toolchain). This is exactly the situation #434's own process notes anticipate: *"If the grammar-parity fixture-corpus check reveals `bloqr-enginelib` does NOT agree well enough with AGTree/the existing DNS validator to be trustworthy, do NOT force the adoption."* A crate that cannot be compiled with this workspace's toolchain fails that bar even more fundamentally than a grammar mismatch would have — there is no way to run the fixture corpus against it at all without first making an unscoped, unauthorized change to shared workspace configuration.

## Decision

**Do not adopt `bloqr-enginelib` at this time. Hand-roll a narrow browser-syntax validator** inside `bloqr-validator-core::syntax`, scoped exactly to what #434 asks for: "can this rule be parsed and is it well-formed" for the specific shapes named in scope item 2 — cosmetic separators (`##`, `#@#`, `#?#`, `#$#`, `#%#`, and their exception/extended-CSS/scriptlet variants), extended CSS selector and scriptlet-injection *bodies* (accepted as opaque non-empty payloads, not parsed to an AST — full cosmetic-body semantics are tsurlfilter's job per ADR 0002), and browser-only `$` modifiers (`$script`, `$image`, `$third-party`, `$domain=`, …). This is deliberately **not** a full AGTree-equivalent grammar; it mirrors the DNS-mode validator's own existing scope discipline (`valid_adblock_rule`'s pattern/character-class checks, not full semantic validation).

The hand-rolled implementation's separator table and modifier list were derived from reading `bloqr-enginelib`'s own `src/filters/cosmetic.rs` (`CosmeticFilter::parse`'s `#`/`##` marker-scanning and action-marker logic) and `src/lists.rs` (`parse_filter`'s network/cosmetic dispatch) during the investigation above — so even though the crate wasn't taken as a runtime dependency, its source was used as a grammar reference, keeping this hand-rolled parser aligned with a real, actively-maintained adblock-rust fork rather than being invented from scratch.

### Revisiting this decision

This is not a permanent rejection of `bloqr-enginelib`. If a future change either (a) bumps this workspace's MSRV to 1.97+ for independent reasons, or (b) the upstream/fork toolchain requirement relaxes, the grammar-parity fixture-corpus check described in #434 should be completed at that point, and this ADR revisited. The fixture corpus collected during this investigation (the four rules above, plus this issue's test additions to `syntax.rs`) is a reusable starting point for that future check.

## Consequences

- No new runtime dependency added to `bloqr-validator-core` for browser-engine validation; `Cargo.lock` and the crate's dependency surface stay unchanged apart from the code in this PR.
- The hand-rolled browser grammar is intentionally narrower than AGTree or `bloqr-enginelib`'s own parser — it validates syntactic well-formedness, not full rule semantics. A rule that's syntactically well-formed but semantically nonsensical (e.g. conflicting modifiers) is out of scope here, same as it always has been for DNS-mode `valid_adblock_rule`.
- DNS-mode validation (`ValidationEngine::Dns`, the default) is unchanged — same functions, same tests (now engine-gated rather than deleted), same behavior.
- `docs/architecture/licensing-strategy.md` now documents the MPL-2.0/GPL-3.0 compatibility finding even though the crate wasn't adopted, since a future revisit (see above) benefits from not re-deriving it.
