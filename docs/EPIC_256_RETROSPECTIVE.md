# Epic #256 Retrospective (2026-08)

A record of the epic that took this repo from four independent compilers with no
shared library, no Dashboard, and docs describing a pre-split, YAML/TOML-first,
AdGuard-branded product, to what's actually here today. Written for whoever
picks up the next epic — `#331`/`#372`'s repo/namespace reorg is the obvious
next thing carrying real continuity from this one — so the reasoning behind
decisions isn't only reachable by re-reading ~30 PR diffs. Companion to
`docs/RESTRUCTURING_RETROSPECTIVE.md`, which covers the repo-split/JSR-extraction
epic this one continued from.

## What actually happened, in order

Roughly forty sub-issues, shipped as incrementally-reviewed PRs rather than
one enormous change:

1. **Dashboard scaffold.** `src/bloqr-dashboard/` — its own solution
   (`BloqrDashboard.slnx`), `Bloqr.Dashboard.Abstractions`/`.Core`/`.Console`/
   `.Tests`, menu-driven and never-terminating by design.
2. **Common .NET library extraction.** `Bloqr.Compiler.Abstractions`/`.Core`
   pulled out of `rules-compiler-dotnet` into `src/compiler-common-dotnet/`
   (its own solution, `CompilerCommon.slnx`) so both `RulesCompiler` and
   Dashboard consume the same code via `<ProjectReference>` instead of
   duplicating it.
3. **Config generation, editing, and schemas.** First-party JSON Schemas for
   compiler config and Dashboard config, a wizard that walks every schema
   option and writes commented `.jsonc`, round-trip editing with git-based
   version history and automatic backups, JSON/JSONC documented as the only
   supported formats (YAML/TOML kept functionally working for backward
   compatibility, per #259, but undocumented).
4. **Hash verification, output publishing, and the event pipeline.**
   `.hashes.json` sidecar, conflict-strategy/archiving output handling, the
   full `CompilationStarting`→`Completed` event set wired through both the
   compiler and the chunking service, Polly-backed retry and optional
   background queueing so a slow event handler can't stall a compile.
5. **Rich compilation UI + CLI/library parity.** Stage-by-stage live progress
   in Dashboard, and everything reachable from its menus also exposed as a
   CLI switch and as `IDashboardService` for embedding.
6. **`rules-validator` real FFI, then wired into every wrapper.** A real
   `extern "C"` cdylib surface (not just a Rust API), P/Invoked from .NET,
   shelled out to via CLI from TypeScript/Python/PowerShell/bash/zsh.
7. **Build/release integration.** `build.sh`/`build.ps1` and `release.yml`
   extended to cover Dashboard, the common library, and `rules-validator`
   together, each app self-contained with its own copy of the native library.
8. **Documentation pass, phase 1 (#257–#259).** Hardcoded old-repo-name/path
   fixes, JSON/JSONC-only doc rewrite across `docs/` and both READMEs.
9. **NuGet distribution (#261).** `Bloqr.Compiler.Abstractions`/`.Core`
   published to GitHub Packages — in-repo consumers stay on
   `<ProjectReference>`, only out-of-repo consumers get the package.
10. **crates.io naming standard + publishing (#365, #371, #373).** Landed the
    org-wide `brand -> short-name -> core[-cli]` naming standard across
    JSR/crates.io/NuGet, renamed the Rust crates to `bloqr-validator-core`/
    `bloqr-validator-core-cli`, gave each crate an independent version
    (decoupled from `version.workspace = true`), published both to crates.io
    (the CLI via `cargo install`, matching how Rust distributes binaries).
11. **Release pipeline split (#374).** Extracted `publish-crates.yml`/
    `publish-nuget.yml` into their own path-filtered, independently-triggered
    workflows (mirroring `publish-jsr.yml`) rather than gating package
    publishing behind the repo-wide binary release. Found and fixed a
    pre-existing Rust workspace path bug in the process (see below).
12. **Documentation pass, phase 2 (#278, #358).** Moved `src/website/` to
    `website/` (repo root, ahead of eventual extraction into its own repo)
    and did a real content rewrite: a Dashboard page that hadn't existed
    despite Dashboard being the epic's flagship deliverable, a from-scratch
    verification of `ENVIRONMENT_VARIABLES.md` against actual code, and a
    sweep of stale crates.io/config-format references across ~10 other docs.

## Obstacles overcome

- **Squash-merge branch divergence — hit this twice.** After a squash-merged
  PR, the source branch (if reused for more commits) still contains the
  *pre-merge* commit as history, while `main` now has a *different* commit
  object with identical content. Continuing to build on the stale local
  branch makes GitHub see two independently-authored versions of the same
  change and report a real merge conflict on the next PR — even though
  nothing actually conflicts. Fix both times was the same:
  `git diff <stale-commit> origin/main --stat` to confirm they're
  content-identical, then `git rebase --onto origin/main <stale-commit>
  <branch>` to drop the duplicate and replay only the genuinely-new commits.
  **The generalizable lesson: after any PR on this branch merges, reset the
  branch onto fresh `origin/main` before adding more commits to it — don't
  assume the local branch is still equivalent to `main` just because it was
  a moment ago.**
- **Cargo workspace path assumption bug in `release.yml`.** The Rust
  binary-packaging step assumed `cargo build`'s output landed under
  `src/rules-compiler-rust/target/...` because that's where the build step's
  `working-directory` pointed — but `rules-compiler` is a Cargo **workspace
  member**, so output actually lands under the repo-root `target/` dir (no
  `target-dir` override exists in `.cargo/config.toml`). This had been wrong
  since the workspace was set up and was only caught because this epic's
  work forced the first real end-to-end `release.yml` run the repo has ever
  had post-restructuring.
- **crates.io versions are immutable.** Fixing a published crate's README or
  description after the fact isn't a re-publish — it requires a genuine new
  version. `bloqr-validator-core` went from 1.0.0 to 1.0.1 purely for this.
- **Docs drift further from code than a read-through reveals.**
  `docs/ENVIRONMENT_VARIABLES.md` documented an entire "Webhook Module"
  section with zero corresponding code, and a cross-language `ADGUARD_*`
  "standard" that was never actually implemented — while missing the real
  `.NET`/Dashboard env vars entirely. Caught by grepping actual usage sites
  across every language's source, not by reading the doc's prose. Worth
  doing this kind of grep-against-reality pass periodically, not just when
  an issue explicitly calls for a "documentation audit."
- **Cargo's `[package] name` vs `[lib] name` vs dependency-table-key
  decoupling.** Renaming a crate's crates.io identity without touching any
  `use` statement in the codebase requires three independent pieces to line
  up: `[package] name` (the registry identity), `[lib] name` (the internal
  module identifier), and the *dependency table key* in anything depending
  on it via `path` (which — not `[lib] name` — determines the `use`-visible
  extern-crate identifier for path dependencies). Got this wrong once
  (assumed `[lib] name` controlled it) before the real mechanism was
  confirmed via a real `E0432` compiler error.

## Tricky decisions worth remembering the reasoning for

- **The `-cli` suffix convention is Cargo-specific, not a universal law.**
  Cargo's packaging model forces separate crates for library-only vs.
  binary-only dependents (you don't want `clap` pulled into every consumer
  of the library). JSR and NuGet don't have this constraint — JSR already
  ships CLI+library as one package with subpath exports
  (`@bloqr/compiler-core`'s `deno.json`), and this org's NuGet convention is
  CLI apps ship as GitHub Release binaries, never NuGet packages at all. The
  full naming matrix (FOSS lib / FOSS CLI / commercial lib / commercial CLI,
  each versioned independently) is Rust-specific; don't mechanically copy it
  to a future JSR or NuGet package.
- **Package-registry publishing decoupled from the binary release,
  deliberately.** `release.yml` now only builds the one thing that
  genuinely needs to stay a single coordinated event — the multi-language
  binary bundle under one GitHub Release tag. `publish-crates.yml`/
  `publish-nuget.yml`/`publish-jsr.yml` each publish independently on a
  path-filtered push to `main`, so a validation-logic-only change doesn't
  wait for (or force) a full binary release, and vice versa.
- **NuGet target is GitHub Packages, not nuget.org — for now.** Avoids a
  second cross-service auth relationship to debug (JSR's OIDC saga was
  reason enough), appropriate for an audience that's currently "internal
  building blocks," and it's a config change rather than a re-architecture
  whenever that changes.
- **`bloqr-validator-core-cli` got published too, past what #365 originally
  scoped.** It was initially treated as workspace-internal-only (no
  perceived external consumer), but `cargo install` is the standard Rust
  distribution mechanism for CLI tools (same as `ripgrep`) — there was no
  real reason to withhold it once the library side was already going
  through the same pipeline.
- **`website/` moved out of `src/` ahead of, not during, its eventual repo
  extraction.** The move itself was low-risk and worth doing now (it isn't
  a compiler wrapper, doesn't belong next to the things that are); the
  extraction itself is deliberately deferred, with the two things that will
  actually need to change when it happens (the `docs/`-sourcing relative
  path, `pathPrefix`) documented in-place in `gatsby-config.js`'s header
  comment and `website/README.md`, not left to be rediscovered later.

## Where things stand now

- Epic #256: **29/29 sub-issues closed.**
- Published and live: `@bloqr/compiler-core` (JSR), `Bloqr.Compiler.Abstractions`/
  `.Core` (NuGet via GitHub Packages), `bloqr-validator-core` +
  `bloqr-validator-core-cli` (crates.io).
- Dashboard, the config wizard/editor, hash verification, the durable event
  pipeline, structured JSON logging, and the rewritten documentation site
  are all real, working, and verified (real builds, not just read-throughs)
  — not aspirational.
- `docs/architecture/versioning-strategy.md` is the reference doc for the
  per-package publishing pattern (JSR reference implementation:
  `@bloqr/compiler-core`; crates.io reference implementation:
  `bloqr-validator-core`) — copy it for the next independently-published
  package rather than re-deriving the pattern.

## Still open, worth carrying into the next epic

- **#372** (namespace/directory reorg, sub-issue of **#331**) — should
  `rules_validator` become `bloqr_validator` internally even though the
  *published* crate stays `bloqr-validator-core`? Still genuinely
  undecided. Scoped to cover the PoC wrappers (Python/TypeScript/PowerShell/
  Shell) too, not just Rust and the .NET reference implementation.
- **Conventional-Commits bump/tag automation for crates.io and NuGet.**
  JSR has it (`compiler-core-version-bump.yml`/`-create-version-tag.yml`);
  crates.io and NuGet versions are still bumped by hand. Explicitly flagged
  as follow-up in `versioning-strategy.md`, not silently dropped.
- **NuGet ID-prefix reservation for `Bloqr.*`.** Deprioritized/moot per the
  repo owner while GitHub Packages remains the only target — revisit if/when
  nuget.org publishing is actually pursued.
- **Native AOT for the .NET apps.** Evaluated and deliberately deferred
  (`docs/architecture/release-packaging-strategy.md`) — YAML/TOML support
  and `AnsiConsole.WriteException` are both real blockers, not busywork.
- **WPF UI layer on top of Dashboard** — explicitly named in the epic's
  original scope as a follow-on, not started.
- **`website/`'s eventual extraction into its own repository**, and CLAUDE.md's
  existing note that it's expected to eventually move to Starlight in
  `bloqr-compiler` rather than staying on Gatsby indefinitely — two separate
  future moves, don't conflate them.
