# Licensing Strategy: FOSS (`bloqr-core`) vs. Commercial (`bloqr-compiler`)

**Status**: Active standard — relicensing of `bloqr-core` is in progress, see
[`docs/legal/LICENSE-DRAFT.md`](../legal/LICENSE-DRAFT.md) and its tracking
issue. This doc explains the relationship, not the mechanics of the swap
itself.

## The model

Bloqr operates two related but license-distinct codebases:

- **`bloqr-core`** (this repo) — source-available, free for non-commercial
  use. Anyone can read, audit, fork, and use it to produce their own output
  (a compiled filter list, a hostlist, a configuration) and give that output
  away for free, including as a business or professional entity. What's
  restricted is monetizing the Software or its output — selling it,
  paywalling it, or building a revenue-generating product/service around it
  — without a commercial license from Bloqr Systems.
- **`bloqr-compiler`** ([`BloqrAI/bloqr-compiler`](https://github.com/BloqrAI/bloqr-compiler),
  private) — the commercial, hosted "Compiler-as-a-Service" product: AGTree-powered
  multi-syntax AST parsing/translation across AdGuard, uBlock Origin, and
  Adblock Plus formats, deployable on Cloudflare Workers/Deno Deploy/Vercel
  Edge/AWS Lambda@Edge, with metered API billing. Licensed under its own
  [source-available commercial license](https://github.com/BloqrAI/bloqr-compiler/blob/main/LICENSE)
  and [commercial license agreement template](https://github.com/BloqrAI/bloqr-compiler/blob/main/COMMERCIAL_LICENSE.md).

**Different licenses, same underlying philosophy**: free to use, audit, and
build on for non-commercial purposes; commercial use requires a license from
Bloqr Systems, who retains unlimited rights to its own software. `bloqr-core`'s
license is adapted from `bloqr-compiler`'s rather than copied verbatim,
because `bloqr-core` ships as source you run yourself — it has no hosted
component to meter usage through, so the SaaS-API-billing and proxy-metering
commercial paths that make sense for `bloqr-compiler` don't apply here.

## Why they're kept separate, not one license for both

- `bloqr-compiler`'s commercial value (the AGTree AST engine, multi-syntax
  translation, hosted CaaS infrastructure, billing) is deliberately **not**
  present in `bloqr-core`. `@bloqr/compiler-core`'s own source comments
  document this: it was extracted from `bloqr-compiler`'s core specifically
  *without* `@adguard/agtree` and without the Workers/CaaS-specific code
  (see `src/compilers/typescript/src/transformations/ValidateTransformation.ts`
  and `src/compilers/typescript/src/utils/RuleUtils.ts`, both of which
  reference the companion decoupling effort tracked as `bloqr-compiler#2200`).
- Keeping the license text specific to what each repo actually contains
  avoids promising enforcement mechanisms (like API metering) that
  `bloqr-core` structurally cannot provide, and avoids importing
  `bloqr-compiler`-specific commercial terms (SaaS pricing tiers, proxy
  billing) that have no equivalent here.
- This split also matters for the **"no infectious licenses" goal**
  (see `CLAUDE.md`'s Operational Notes): `bloqr-core` deliberately avoids
  copyleft (GPL/AGPL/LGPL) third-party dependencies so that adopting a
  Bloqr-authored restrictive-but-open license isn't undermined by a
  dependency's own copyleft terms forcing broader disclosure than intended.
  See the dependency license compatibility audit (tracked separately,
  modeled on `bloqr-compiler`'s own
  [`DEPENDENCY_LICENSE_COMPATIBILITY.md`](https://github.com/BloqrAI/bloqr-compiler/blob/main/DEPENDENCY_LICENSE_COMPATIBILITY.md)).

## Current state (as of this doc)

- `bloqr-core`'s root `LICENSE` file says MIT. ~25 other files across the
  repo (workspace `Cargo.toml`, `pyproject.toml`, most compiler `README.md`
  files, several `docs/*.md`, the website's `package.json` and footer) say
  GPL-3.0. **Neither is the intended final state** — both predate this
  licensing-strategy decision. The intended replacement is
  [`docs/legal/LICENSE-DRAFT.md`](../legal/LICENSE-DRAFT.md), pending review.
- Every published package (`@bloqr/compiler-core` on JSR; `bloqr-validator-core`,
  `bloqr-validator-core-cli`, `bloqr-compiler` on crates.io;
  `Bloqr.Compiler.Abstractions`, `Bloqr.Compiler.Core` on NuGet via GitHub
  Packages) currently carries GPL-3.0 in its own registry metadata. Already-published
  versions are immutable on their registries, so a repo-wide relicense
  doesn't retroactively change what's already out there — see the open
  questions in `LICENSE-DRAFT.md` for what that implies for the rollout.

## Related documents

- [`docs/legal/LICENSE-DRAFT.md`](../legal/LICENSE-DRAFT.md) — the candidate
  replacement license text and its open questions.
- [`bloqr-compiler`'s `LICENSE`](https://github.com/BloqrAI/bloqr-compiler/blob/main/LICENSE)
  and [`COMMERCIAL_LICENSE.md`](https://github.com/BloqrAI/bloqr-compiler/blob/main/COMMERCIAL_LICENSE.md) —
  the reference implementation this repo's license is modeled on.
- [`docs/architecture/versioning-strategy.md`](versioning-strategy.md) — per-package
  versioning, relevant to the "already-published packages are immutable"
  question above.
