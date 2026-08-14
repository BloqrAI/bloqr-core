# BloqrAI Versioning Strategy

**Status**: Active standard, org-wide.
**Scope**: Every independently-published `@bloqr` JSR package, in this repo and any future repo that publishes one.

## The core rule

**Code version and package version always match, and there is exactly one place you edit to change either.**

Concretely, for any package that follows this standard:

1. A `VERSION` constant in the package's own source (e.g. `src/version.ts`) is the single source of *writable* truth.
2. A `version:sync` script propagates that constant into the package manifest (`deno.json`'s `"version"` field, and `package.json`/`wrangler.toml`/anything else that carries a duplicate copy, if the package has those).
3. Nobody hand-edits `deno.json`'s version field directly — it's always derived.

This isn't a new idea invented for this repo: `bloqr-compiler` (the private commercial monorepo) already runs exactly this pattern (`src/version.ts` → `scripts/sync-version.ts` → `deno.json`/`package.json`/`wrangler.toml`) and has for a while. This document makes it the explicit, written-down, org-wide standard — both because `bloqr-core` is about to need it for real (its first JSR package, `@bloqr/compiler-core`, just went live), and because `bloqr-core` is going to decompose into *several* independently-versioned JSR packages as that epic proceeds, which is a meaningfully different shape than `bloqr-compiler`'s single-package case.

## Why "one repo, one version" doesn't work here

`bloqr-compiler` has one deployable artifact, so one `VERSION` constant and one tag prefix (`v*` / `compiler-v*`) is sufficient. `bloqr-core` is different: it already contains multiple independent things (four rules-compiler implementations, a rules-validator, wrapper CLIs, a docs website), and the plan is to decompose the TypeScript/Deno side specifically into multiple standalone `@bloqr/*` JSR packages over time. Each of those needs to be versioned, tagged, and released **independently** — bumping `@bloqr/compiler-core` from 1.0.0 to 1.1.0 must not force (or even suggest) a version bump on some future `@bloqr/rules-schema` or `@bloqr/filter-utils` package that happens to live in the same repo and didn't change.

So the standard is **per-package**, not per-repo:

- Each package gets its own `VERSION` source-of-truth file, scoped to that package's own directory.
- Each package gets its own git tag prefix: **`<package-slug>-v<semver>`** (e.g. `compiler-core-v1.2.3`). No bare `v*` tags in this repo once there's more than one package — bare `v*` is ambiguous the moment a second package exists.
- Each package gets its own version-bump automation, scoped by path filter to only that package's directory, with its own bump-commit marker string (so `git log --grep` for "when did we last bump package X" doesn't accidentally match package Y's bump commits).
- Each package's publish workflow triggers off changes to its own path (already true for `publish-jsr.yml` — see below) and/or its own tag.

## Reference implementation: `@bloqr/compiler-core`

This is the first package on the new standard, and it's meant to be the copy-paste template for every future one. Four pieces, all scoped to `src/adblock-compiler-core/`:

| File | Role |
|---|---|
| `src/adblock-compiler-core/src/version.ts` | `export const VERSION = '1.0.0'` — hand-edited only by the bump automation (or a human doing a manual override), never by feature PRs. |
| `src/adblock-compiler-core/scripts/sync-version.ts` | `deno task version:sync` — reads `VERSION` from `version.ts`, writes it into `deno.json`'s `"version"` field. No-ops if already in sync. |
| `.github/workflows/compiler-core-version-bump.yml` | Runs on every push to `main` that touches `src/adblock-compiler-core/**`. Walks Conventional Commits since the last `chore: bump compiler-core version` commit, determines the bump type (see below), bumps `version.ts`, runs the sync script, updates `CHANGELOG.md`, and opens a PR (`auto-version-bump-compiler-core-<version>` branch). |
| `.github/workflows/compiler-core-create-version-tag.yml` | Runs when a `auto-version-bump-compiler-core-*` PR merges. Reads the now-updated `deno.json` version and pushes the `compiler-core-v<version>` tag. |
| `.github/workflows/publish-jsr.yml` | Unchanged by this doc's introduction — it already triggers on any push to `src/adblock-compiler-core/**` on `main`, and `deno publish` is idempotent (no-ops if the current version is already published). The version-bump PR's merge commit is what actually causes the next real publish; the tag above exists for traceability, not to gate the publish. |

### Conventional Commits → bump type

Same rule `bloqr-compiler` already uses, scoped to commits that touched the package's own directory:

| Commit prefix | Bump |
|---|---|
| `fix:`, `perf:` | patch |
| `feat:` | minor |
| `feat!:`, `fix!:`, or a `BREAKING CHANGE:` footer | major |
| anything else (`chore:`, `docs:`, `test:`, `ci:`, ...) | no bump |

A push with no bump-worthy commits since the last bump is a silent no-op (logged to the workflow's job summary, no PR opened).

## Onboarding a new decomposed package

When a piece of `bloqr-core` (or a brand-new repo) becomes its own `@bloqr/*` JSR package, copy the compiler-core pattern with these substitutions:

1. `<package-dir>/src/version.ts` (or equivalent) — new file, `VERSION = '0.1.0'` (or wherever the extracted code's version actually starts — if it's an extraction of existing code that already has a version, keep continuity rather than resetting to 0.1.0).
2. `<package-dir>/scripts/sync-version.ts` — copy `compiler-core`'s, change the relative import path if the directory depth differs. If the new package also ships a `package.json`/`wrangler.toml` (unlikely for a pure JSR library, but possible), add sync steps for those too — see `bloqr-compiler`'s fuller `sync-version.ts` for that shape.
3. `.github/workflows/<package-slug>-version-bump.yml` — copy `compiler-core-version-bump.yml`, replace:
   - the `paths:` filter (`src/adblock-compiler-core/**` → the new package's path)
   - the bump-commit grep marker (`chore: bump compiler-core version` → `chore: bump <package-slug> version`)
   - the branch prefix (`auto-version-bump-compiler-core-` → `auto-version-bump-<package-slug>-`)
   - the `working-directory` default
4. `.github/workflows/<package-slug>-create-version-tag.yml` — copy `compiler-core-create-version-tag.yml`, same substitutions, tag prefix `<package-slug>-v`.
5. That package's own `publish-jsr.yml` (or a shared one filtered by path — either is fine, `bloqr-core` currently has one workflow per publishable package) — path-filtered trigger, `deno publish --token ${{ secrets.JSR_WORKFLOW_TOKEN }}` per `docs/jsr-token-authentication.md`.
6. Add the new package to `docs/jsr-org-standards.md`'s package table.

## Reference implementation: `bloqr-validator-core` (crates.io)

The core rule — **independent version per publishable package identity, one place to edit it** — applies to crates.io exactly as it does to JSR, with two Cargo-specific differences:

1. **No separate `version.ts`/sync-script indirection is needed.** Cargo's manifest (`Cargo.toml`'s `[package] version` field) already *is* the single source of writable truth — there's no JSONC-vs-JSON split to work around like `deno.json`. So the "sync" step from the JSR pattern simply doesn't exist for Rust crates.
2. **`version.workspace = true` (inheriting `[workspace.package] version`) must not be used for any independently-published or independently-cadenced crate.** Workspace-inherited versioning is the crates.io equivalent of the "one repo, one version" anti-pattern this doc's introduction already rejects for JSR — it would force `bloqr-validator-core` and `bloqr-validator-core-cli` to bump in lockstep with each other and with `rules-compiler`, even when only one of them changed. As of #365's follow-up, both `bloqr-validator-core` and `bloqr-validator-core-cli` (`src/rules-validator/`) declare an explicit, independent `version = "1.0.0"` instead. `rules-compiler` (`src/rules-compiler-rust/`) is unpublished and workspace-internal, so it stays on `version.workspace = true` for now — revisit if it's ever published independently.

### The FOSS/commercial × library/CLI version-independence matrix

Per the Rust package-naming standard (`docs/jsr-org-standards.md`), a single product surface can have up to four crates: FOSS library, FOSS CLI, commercial library, commercial CLI. All four version **independently** — none of them share a version number by convention, even where one embeds another as a dependency:

| Crate | Versions independently because |
|---|---|
| `bloqr-validator-core` (FOSS lib) | Its cadence is driven by validation-logic changes; this is the published, externally-consumed artifact. |
| `bloqr-validator-core-cli` (FOSS CLI) | Its cadence is driven by flag/UX/output-format changes, which don't always coincide with a library-logic change (and vice versa — a library patch doesn't always need a CLI release). |
| `bloqr-validator` (commercial lib, reserved) | A different product surface with its own release cadence and its own team ownership boundary, even when the same engineers work both sides. May depend on `bloqr-validator-core` as a Cargo dependency, but that's a *dependency* version pin, not a lockstep release version. |
| `bloqr-validator-cli` (commercial CLI, reserved) | Same reasoning as the FOSS CLI, applied to the commercial surface. |

This mirrors the crates.io convention itself (any crate can depend on any version-compatible range of any other crate) and avoids the classic monorepo trap where an unrelated CLI flag tweak forces a version bump — and a fresh audit/compliance review — of the validation library it happens to wrap.

### crates.io publishing is independent of the repo-wide release, like JSR

`bloqr-validator-core` publishes via its own `publish-crates.yml`, triggered on every push to `main` touching `src/rules-validator/rules-validator-core/**` (or manually via `workflow_dispatch`) — the same path-filtered-push pattern as `publish-jsr.yml`, gated by an idempotency check against the crates.io API rather than a `cargo publish --skip-duplicate` flag (which doesn't exist). This is deliberately **not** part of `release.yml`'s repo-wide `v*`-tag-triggered binary release: `bloqr-validator-core-cli`'s and `rules-compiler`'s release *binaries* still bundle into that coordinated multi-language GitHub Release, but the *library's* crates.io publish doesn't need to wait for (or force) a full binary release cut just because a validation-logic change landed. `docs/architecture/nuget-distribution-strategy.md`'s `publish-nuget.yml` got the identical treatment for the same reason.

What's still genuinely manual: there's no automated Conventional-Commits bump/tag workflow pair (the `compiler-core-version-bump.yml`/`compiler-core-create-version-tag.yml` equivalents) for `bloqr-validator-core` yet — today its version is bumped by hand in `Cargo.toml`. Building that automation is tracked as follow-up under #372, not this doc.

## What this doc does *not* cover yet

- The non-JSR, non-Rust wrapper projects in this repo (.NET, Python, PowerShell) have their own version fields (`.csproj`, `pyproject.toml`) and are not yet wired into an equivalent automated bump/tag/release pattern for their own registries (NuGet, PyPI). `docs/release-guide.md` describes their current (manual, repo-wide-tag) release process, which predates this standard and still reflects the pre-split repo shape in places — it needs its own pass to either adopt an equivalent per-package pattern or explicitly document why it stays manual. Tracked as follow-up, not blocking this doc.
- Full crates.io (and NuGet) Conventional-Commits bump/tag automation — see the crates.io publishing note above.
- Cross-repo propagation: once packages are actually split out of `bloqr-core` into their own repos, this document (or a copy of it) needs to travel with them. For now, see `docs/org-documentation-strategy.md` for how org-wide docs are being tracked during this transitional period (`.github-private` for internal standards while things are still moving; this doc lives in `bloqr-core` itself since it's currently the one place with a working reference implementation).

## Related

- `docs/jsr-token-authentication.md` — how `publish-jsr.yml` authenticates to JSR.
- `docs/jsr-org-standards.md` — JSR scope/package conventions this versioning strategy sits alongside.
- `bloqr-compiler`'s `src/version.ts` / `scripts/sync-version.ts` / `.github/workflows/version-bump.yml` / `.github/workflows/create-version-tag.yml` — the pre-existing, independently-arrived-at prior art this standard formalizes and extends to the multi-package case.
