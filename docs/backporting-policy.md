# Backporting Policy: bloqr-compiler → adblock-compiler-core

This document defines what gets backported from the commercial `bloqr-compiler` product ([`BloqrAI/bloqr-compiler`](https://github.com/BloqrAI/bloqr-compiler)) into this repository's open-source `@bloqr/compiler-core` package (`src/adblock-compiler-core/`), and the process for doing it.

## Why two compilers exist

`@bloqr/compiler-core` (this repo) is a minimal, dependency-free filter-list compilation engine, extracted from `bloqr-compiler`'s core. `bloqr-compiler` is the full-featured commercial product: AST-level parsing via `@adguard/agtree`, linting, diff reports, a plugin system, Cloudflare Workers deployment, and observability integrations. See `src/adblock-compiler-core/README.md`'s Architecture section for the full history of how the JSR namespace ended up here.

They are separate products going forward, not two versions of the same thing. Backporting is about deliberately pulling specific, narrow improvements across — not keeping them in sync feature-for-feature.

## What gets backported (criteria)

| Category | Backport? | Why |
|---|---|---|
| Core-engine bug fixes (transformations, downloader, formatters, chunking, hashing) | **Yes** | Correctness issues affect both products equally |
| Performance improvements to shared algorithms | **Yes** | Both products benefit; this is the whole point of extracting a shared core |
| CLI ergonomics improvements (flags, error messages) that don't depend on commercial features | **Maybe** | Case-by-case — only if genuinely useful without the features being flagged |
| Anything requiring `@adguard/agtree` or another third-party AdGuard library | **No** | adblock-compiler-core is deliberately dependency-free; see [bloqr-compiler#2200](https://github.com/BloqrAI/bloqr-compiler/issues/2200) |
| Cloudflare-specific features (Workers deployment, Browser Rendering, Flagship feature flags, Page Shield) | **No** | Out of scope for an npm/JSR-distributed CLI/library |
| Plugin system, diff reports, conflict detection, rule optimizer, analytics, query language, agent system | **No** | Commercial differentiators, by design |
| Observability (OpenTelemetry, Sentry) | **No** | adblock-compiler-core keeps a no-op-by-default diagnostics seam only |

When in doubt: if the change is inside a module that adblock-compiler-core doesn't have at all (see the "Not included" list in `src/adblock-compiler-core/CHANGELOG.md`), it's not a backport candidate. If it's inside a module both packages share (transformations, downloader, formatters, compiler, config schemas), it usually is.

## Process

1. **Identify the source change** in `bloqr-compiler` — the commit or PR with the fix/improvement.
2. **Classify it** against the criteria table above. If it's ambiguous, open an issue on `bloqr-lists` tagged `backport-candidate` and ask before porting.
3. **Locate the equivalent file** in `src/adblock-compiler-core/`. Module layout intentionally mirrors `bloqr-compiler`'s (`transformations/`, `downloader/`, `formatters/`, `compiler/`, `configuration/`), so most files have a 1:1 counterpart — but remember `RuleUtils.ts` and `ValidateTransformation.ts` are hand-written, non-AGTree reimplementations, not verbatim ports. A fix in `bloqr-compiler`'s AGTree-based `RuleUtils`/`ValidateTransformation` needs to be re-expressed in string/regex terms for the adblock-compiler-core versions, not copy-pasted.
4. **Port the change**, adapting for the two differences above (no AGTree, no plugin/diagnostics infrastructure beyond the no-op seam).
5. **Run the adblock-compiler-core test suite** (`cd src/adblock-compiler-core && deno task test`) and add/adjust tests for the ported change.
6. **Bump the version** in `src/adblock-compiler-core/deno.json` per semver (patch for fixes, minor for backward-compatible improvements).
7. **Record the backport** in `src/adblock-compiler-core/CHANGELOG.md`, linking back to the source `bloqr-compiler` commit/PR for traceability.

## Non-goals

This policy does not obligate `bloqr-compiler` to backport *from* adblock-compiler-core — the open-source engine is the extraction source, not a feature contributor to the commercial product. It also doesn't require every bloqr-compiler release to trigger a review here; backporting is pull-based (someone notices a fix worth porting), not push-based (no obligation to track every upstream change).
