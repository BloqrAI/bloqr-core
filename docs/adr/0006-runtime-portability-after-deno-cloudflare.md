# ADR 0006: Runtime Portability Policy After "Deno Is Joining Cloudflare"

**Status**: Proposed
**Date**: 2026-10-10
**Related**: [Deno blog — "Deno is joining Cloudflare"](https://deno.com/blog/cloudflare) (2026-10-09), `docs/adr/0001-canonical-rules-compilation-engine.md` (the `deno run` subprocess integration model this ADR re-examines), `docs/adr/0004-src-reorg-naming-blueprint.md` (FOSS-repo scope boundary), `docs/architecture/versioning-strategy.md`, `docs/jsr-token-authentication.md`, `CLAUDE.md` § "TypeScript/JavaScript runtime and package-manager order of preference"

## Context

### What was announced

On 2026-10-09 the Deno team announced it is joining Cloudflare, folding into the Workers and
Durable Objects teams to build a new project ("celld") on the Workers programming model. As it
affects this repository, the post states (relative durations are the post's own; dates below are
derived from the announcement date and are approximate):

| Item | Stated outcome | Approximate date |
|---|---|---|
| Deno runtime | Monthly bug-fix and security releases for one more year; development then ends. Stays open source; others are invited to continue it. | ~Oct 2027 |
| Deno Deploy | Operates for six months, then shuts down. | ~Apr 2027 |
| JSR | Keeps operating; infrastructure moves to Cloudflare. | n/a |
| `rusty_v8` | Continues to be supported; work toward integrating it into `workerd`. | n/a |

The post describes no API, licensing, or compatibility changes. Everything in this ADR is based on
that post; nothing here has been confirmed with Deno or Cloudflare beyond it.

### Where this repository depends on Deno

Audited 2026-10-10 against `bloqr-core` `main` (`9830bf8`). Reproduce with the commands under
[Investigation](#investigation).

1. **Every non-TypeScript compiler requires the `deno` binary at runtime.** Per ADR 0001, the
   Rust, .NET, Python, Swift, and PowerShell wrappers shell out to
   `deno run … jsr:@bloqr/compiler-core/cli` rather than reimplementing compilation. The call sites
   are `src/compilers/rust/core/src/compiler.rs` (`find_command("deno")`),
   `src/common/dotnet/src/Bloqr.Compiler.Core/Helpers/CommandHelper.cs` and
   `src/compilers/dotnet/.../FilterCompiler.cs` (`DenoCommand = "deno"`),
   `src/compilers/python/bloqr_compiler/compiler.py`,
   `src/compilers/swift/Sources/BloqrCompilerCore/Compiler.swift`, and
   `src/compilers/powershell/BloqrCompiler/Private/Get-BloqrCompilerCommand.ps1`. The Dashboard's
   diagnostics menu lists `deno` as a required tool.
2. **The package those wrappers run is resolved from JSR at invocation time.** JSR continuity is
   therefore a runtime dependency of every wrapper, not just a publishing concern.
3. **Deno is the TypeScript package's toolchain**: `deno test`/`deno bench` (the `Deno.test` global
   appears ~4,470 times across both org repos), `deno lint`/`fmt`/`doc`, `deno task`, and
   `deno publish` (JSR OIDC) in `publish-jsr.yml`.
4. **Runtime source coupling is small.** In `src/compilers/typescript/src` ~11 non-test files use
   `Deno.*` beyond the test harness, concentrated in the CLI/console/orchestration layer (`cli.ts`, `logger.ts`,
   `shutdown.ts`, `mod.ts`) and `downloader/ContentFetcher.ts`; the library path is guarded with
   `typeof Deno` checks.
5. **Mitigation already in place**: Bun is a formally supported runtime target, verified by real CI
   (`typescript.yml` runs `src/mod.bun.ts --version`, `--validate`, and a `compile()` library
   smoke test on Bun), per the CLAUDE.md runtime policy.

### The tension

The current policy lists "Deno-native first" as the top preference. That was a sound choice when
made and its other tiers (JSR before npm, Bun as a real target, Workers as the deploy target,
WinterCG-style portability) turn out to be the parts that keep the cost of this announcement low.
But "Deno-native first" combined with "wrappers require `deno` on `PATH`" means end users of the
Rust/.NET/Python/Swift/PowerShell packages must install a runtime whose upstream development ends
in roughly a year.

### Scope

Per ADR 0004, this standard applies to `bloqr-core` and future FOSS libraries only. It does not
bind `bloqr-compiler` or other internal/commercial repos, which keep their own conventions (for
example, `bloqr-compiler`'s `AGENTS.md` currently states Bun is a consumer runtime target and not
the project's own runtime). Those repos are free to adopt the same direction; this ADR doesn't
require it.

## Options considered

### Policy (applies regardless of the wrapper option chosen)

**P1. Keep "Deno-native first" unchanged.** Cheapest now; leaves the policy contradicting the
upstream roadmap. Rejected.

**P2. Reword to "runtime-portable first".** TypeScript stays authored to Web-standard and `node:*`
APIs that run on Deno, Bun, and `workerd`; `Deno.*` is confined to explicitly Deno-only modules
(the `*.deno.ts` convention `bloqr-compiler` already uses is a good model); each supported runtime
is verified by real CI, not asserted. Deno remains the primary *development* toolchain until a
concrete reason to move appears. **Recommended.**

### Wrapper distribution

**W1. Status quo — require `deno` on `PATH`.** Zero work. After ~Oct 2027, users depend on either
a frozen upstream or an unproven community fork, and every wrapper's install docs carry that risk.
Acceptable only as a short-term position.

**W2. Add Bun as an alternative runner in each wrapper.** Wrappers resolve `deno`, then `bun`.
Reuses the Bun entry point (`src/mod.bun.ts`) and CI already in place. Cost: Bun has no import-map
concept, so the CI job installs JSR dependencies with `bunx jsr add …` and aliases `zod` by hand.
That dance would have to move from a CI recipe to something a wrapper can rely on on an end user's
machine. That means shipping a pre-resolved bundle, which is largely option W3.

**W3. Ship a prebuilt, self-contained compiler binary and have wrappers prefer it.** Build
`@bloqr/compiler-core`'s CLI to a single executable per platform (`deno compile` today, since
`bloqr-compiler` already uses it for its own artifact; `bun build --compile` is the alternative)
and publish it with each release. Wrappers resolve the bundled binary first and fall back to
`deno run jsr:…` if absent. Removes the end-user runtime install entirely and decouples users from
the runtime's lifecycle. Costs: per-platform release artifacts (the Dashboard already ships self-contained,
single-file per-platform binaries — see `docs/architecture/release-packaging-strategy.md` — so the
release machinery has precedent, though not for this CLI), and a compiled binary embeds
the runtime version it was built with, so security fixes in V8/the runtime require a rebuild and
re-release (this is still strictly better than an end user running a stale system-wide `deno`).
**Recommended as the end state.**

**W4. Move wrappers to Node.js.** Node is the most widely installed runtime, but CLAUDE.md
deliberately does not treat Node as a target in its own right, and it would still require users to
have a runtime installed. Rejected for now; revisit only if W3's artifacts prove impractical.

## Decision (proposed)

1. **Adopt P2.** On acceptance, update CLAUDE.md's runtime-preference list: replace item 1 with a
   "runtime-portable first" statement, keep Deno as the default development toolchain, keep Bun as
   a CI-verified target, and add `workerd` as a verified target once a real smoke test exists
   (don't claim it before then — same rule CLAUDE.md already applies to Bun).
2. **Adopt W3 as the target architecture for wrappers**, with W1 remaining the fallback path during
   the transition. Concretely: (a) a release job producing per-platform compiled CLI binaries,
   (b) a single binary-resolution function per wrapper (they each already have a `find_command`
   equivalent) that prefers an explicit override, then the bundled binary, then `deno`, and
   (c) diagnostics/docs updated so "Deno required" becomes "Deno optional".
3. **Sequence the work by the dates above**, not before they matter:
   - **Now:** this ADR; fix any docs that advertise Deno Deploy as a deployment target.
   - **Before ~Apr 2027:** confirm no component or documentation depends on Deno Deploy.
   - **Before ~Oct 2027:** W3 shipped for at least the Rust and .NET wrappers, with Deno fallback
     intact for all five.
   - **Decision gate (~mid 2027):** reassess P2/W3 against what celld/`workerd` integration and any
     community Deno fork look like by then. This ADR is written to be cheap to revise; it should not
     be treated as final.

## Consequences

**Positive**
- End users of the Rust/.NET/Python/Swift/PowerShell wrappers stop depending on a system-wide Deno
  and on its upstream lifecycle.
- The policy stops contradicting the upstream roadmap, and the portability work the repo already
  did (guards, Bun CI, JSR-first deps) becomes the stated strategy instead of an accident.
- JSR remaining available means the TypeScript package's distribution channel is unaffected.

**Negative / costs**
- A new release pipeline (per-platform compiled binaries) and a wrapper resolution-order change
  across five languages, each with its own tests.
- Compiled binaries pin a runtime version; we own rebuild-on-advisory for them.
- `Deno.test` (~4,470 uses) is the largest coupling in the repo's own tooling. This ADR
  deliberately does not propose migrating it; that decision should wait for the ~mid-2027 gate.

**Risks to watch**
- **JSR infrastructure migration.** The post says JSR moves to Cloudflare infrastructure. The
  JSR OIDC trusted-publishing setup (see the "Operational Notes" in CLAUDE.md and
  `docs/jsr-token-authentication.md`) was live-verified only recently and has failed before on
  issuer details; re-verify publishing after any announced JSR migration, and keep the
  token-based fallback documented.
- **Wrappers' runtime fetch from JSR.** Until W3 lands, a wrapper invocation needs JSR reachable
  (cached after first use). Any JSR disruption during migration affects wrapper users directly.
- **Unverified claims.** `workerd` integration and a community fork are stated intentions in the
  post, not shipped artifacts. Nothing here should be read as depending on either.

## Investigation

Commands used for the 2026-10-10 audit (run from the workspace containing both org repos; counts
are grep-based and approximate):

```sh
# Deno Deploy usage (workflows/config/source): none found; only doc/marketing mentions
grep -rniE 'deno\.com/deploy|deployctl|deno deploy|\.deno\.dev' --exclude-dir={node_modules,.git,target} .

# Wrappers shelling out to deno
grep -rnE 'find_command\("deno"\)|DenoCommand|Get-Command.*deno|findCommand\("deno"\)' src

# Deno.* API usage by name, repo-wide
grep -rhoE '\bDeno\.[a-zA-Z]+(\.[a-zA-Z]+)?' --include='*.ts' --exclude-dir={node_modules,.git,target} . | sort | uniq -c | sort -rn | head -40

# Bun is exercised by real CI
grep -nE 'setup-bun|bun run' .github/workflows/typescript.yml
```

Not investigated: whether a `deno compile`d `@bloqr/compiler-core` CLI actually produces a working
binary on all five wrapper platforms (W3's feasibility is assumed from `bloqr-compiler`'s existing
use of `deno compile`, not tested here), and binary size/startup cost versus `deno run`. Both
should be spiked before this ADR moves from Proposed to Accepted.
