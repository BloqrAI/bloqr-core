# Restructuring Retrospective (2026-08)

A record of the multi-day effort that took this repo from `bloqr-lists` — a
monolith holding compiled filter lists, API clients, and every rules
compiler — to `bloqr-core`: a focused, JSR-publishing compiler toolkit, with
`data/` and the API clients split into their own repos. Written for whoever
picks up Epic #256 next, human or AI, so the reasoning behind the current
shape of things isn't only reachable by re-reading old PR diffs.

## What actually happened, in order

1. **Repo rename + three-way split.** `bloqr-lists` → `bloqr-core`.
   `data/` → `BloqrAI/bloqr-blocklists` (public, since a filter list's whole
   point is public consumption). `src/adguard-api-*` + `src/linear/` →
   `BloqrAI/bloqr-apiclients` (private, internal tooling). Both splits used
   `git subtree split` + `git subtree add` to preserve file history rather
   than a flat copy — verified after the fact with `git merge-base
   --is-ancestor` that the moved commits are still real ancestors in the
   new repos, not just replayed diffs.
2. **JSR extraction.** `src/adblock-compiler-core` pulled out of the
   monolith as an AGTree-free, standalone TypeScript/Deno package,
   published as `@bloqr/compiler-core`. This is the repo's first JSR
   package and the reference implementation for everything that follows.
3. **JSR publishing, the hard way.** OIDC trusted publishing was the first
   attempt and never worked — see "OIDC: the multi-day dead end" below.
   Landed on token-based publishing (`JSR_WORKFLOW_TOKEN` / `JSR_API_TOKEN`,
   org-level secrets scoped to `@bloqr`) instead, documented in
   `docs/jsr-token-authentication.md` and `docs/jsr-org-standards.md`.
4. **`bloqr-compiler` dependency swap.** The private commercial monorepo
   that originally donated this code was still vendoring its own
   near-duplicate copy. Swapped ~15 formatting-identical files for a real
   `@bloqr/compiler-core` JSR dependency, kept the genuinely-diverged
   commercial files (AGTree-powered validation, conflict detection, rule
   optimizer) local, and preserved one real behavioral difference
   (`FilterDownloader`'s plugin-registry hook) via explicit dependency
   injection rather than losing it. Closed `bloqr-compiler#2200` and
   `#1131`.
5. **Housekeeping issues #289, #293, #294** — stale post-migration doc
   paths + README rewrite, JSR module/symbol documentation (94% → full
   marks on the two doc-score checks), and an audit of npm dependencies for
   JSR-native replacements (`yaml`→`@std/yaml`, `@iarna/toml`→`@std/toml`,
   `chalk`→`@std/fmt/colors`, `cli-table3`→`@cliffy/table`; `ora`,
   `figlet`, `@inquirer/prompts` kept on npm with documented reasoning).
6. **Org-wide versioning strategy** (`docs/architecture/versioning-strategy.md`)
   — designed and merged *before* the decomposition epic starts, not
   after, specifically because bloqr-core is about to become several
   independently-versioned JSR packages and retrofitting a versioning
   scheme onto N packages later is much more expensive than establishing
   the pattern once, now, with one package as the reference
   implementation.

## Obstacles overcome

### OIDC: the multi-day dead end

JSR's OIDC trusted publishing failed with `InvalidIssuer
(invalidOidcToken)` on every attempt, across a full scope/package
recreation, a fresh repo link, and confirmation that BloqrAI has *no*
org-level GitHub OIDC policies configured (checked directly in org
settings — only placeholder/sample values were ever present). This wasn't
a configuration mistake on our end that a docs re-read would fix; it
looks like a genuine JSR-side gap or bug in how org-owned-repo OIDC is
validated. Filed `bloqr-core#291` and, once the user found it independently,
cross-linked the upstream report at
[jsr-io/jsr#1485](https://github.com/jsr-io/jsr/issues/1485).

**The lesson**: don't burn unbounded time re-attempting a third-party
integration that fails identically every time despite correct
configuration. Two or three reproductions with a clean environment each
time is enough to conclude "this is their bug, not ours" — at that point,
switch to the documented, working fallback (token auth) and move on. We
did eventually pivot, but only after the user pushed for it; a faster
internal call to de-risk (try tokens, keep OIDC as a future upgrade path)
would have saved real time.

### `deno publish --token` vs `JSR_TOKEN` env var

Small but completely blocking: `deno publish` does **not** read a
`JSR_TOKEN` environment variable — it needs `--token <value>` as an
explicit CLI flag. The first token-based workflow attempt used `env:
JSR_TOKEN: ${{ secrets.JSR_WORKFLOW_TOKEN }}` (a natural pattern to reach
for) and failed with "No means to authenticate." One log read fixed it.
**Lesson**: when adapting an auth pattern from one tool/doc to another,
verify the *exact* flag/env-var contract against that tool's own error
message rather than assuming symmetry with a similar tool.

### The npm-mirror / minimum-dependency-age collision

Getting `@bloqr/compiler-core` consumed by `bloqr-compiler`'s Cloudflare
Worker build surfaced a real cross-runtime interop gap: Wrangler's esbuild
bundler has no concept of Deno's `jsr:` specifiers, so a bare
`@bloqr/compiler-core` import failed to resolve even though every Deno
task (test/check/lint) resolved it fine. Fix: point the import at JSR's
npm-compatible mirror (`npm:@jsr/bloqr__compiler-core`) instead, which
Deno *does* materialize into `node_modules` for bundler tools to find, plus
a `wrangler.toml` `[alias]` entry mapping the bare specifier to the mirror
package name.

That fix then collided with Deno's **minimum-dependency-age** supply-chain
protection (default 24h) — a brand-new package can't be installed via its
npm-compat mirror until it's aged past that window, even though the exact
same code, addressed as `jsr:`, has no such restriction. This is a real,
if temporary, tension between "publish and consume same-day" (the
decomposition epic's actual cadence) and a legitimate security control.
Resolved by explicitly relaxing it in CI (`--minimum-dependency-age 0`,
scoped to CI only — local developer installs keep full protection) with a
tracked reversion issue (`bloqr-compiler#2212`) rather than either
silently weakening security or blocking indefinitely. **Lesson**: when a
security control and a stated architectural direction (fast, frequent,
same-org package publishing) are in real tension, don't quietly pick a
side — make the tradeoff, the scope of the relaxation, and the reversion
condition all explicit and written down.

### Concurrent background agents sharing one working directory

Three agents were launched in parallel for issues #289/#293/#294 without
`isolation: "worktree"`, so all three ended up doing `git checkout -b`,
edits, and commits in the *same* shared clone. Concretely, this caused:
one agent's commit landing on a different agent's branch during a `HEAD`
race; a subsequent reset attempt getting itself raced by further commits
before it could fully take; and two batches of stale uncommitted changes
left behind as `git stash` entries. None of it was silently lost — every
agent that hit contamination flagged it explicitly in its own final
report rather than pretending the run was clean — but reconciling it
required a careful multi-step audit: diffing every PR's file list against
every other PR's, cross-checking uncommitted working-tree state against
each PR's actual pushed content (including catching a false-alarm CRLF
line-ending diff along the way), and one deliberate rebase to drop a
stray commit that had landed on the wrong branch.

**Lesson, now load-bearing**: always pass `isolation: "worktree"` when
spawning more than one background agent against the same repo in
parallel. It would have prevented all of the above outright. This isn't
a maybe — treat it as a hard requirement for parallel background agents
in this repo going forward.

### The JSONC gotcha, caught only by a real final-integration check

`sync-version.ts` (added by the versioning-strategy PR) used
`JSON.parse`/`JSON.stringify` to update `deno.json`'s version field. Deno's
*own* config loader tolerates `//` comments in `deno.json` (JSONC), and a
later, independent PR (#294's dependency audit) legitimately added
explanatory comments there — "no JSR-native library exists for `ora`,
here's why" and similar. Each PR's own CI was green in isolation: `deno
check`/`lint`/`test`/`publish --dry-run` all use Deno's JSONC-tolerant
parser internally, so none of them ever exercised the strict-JSON code
path in `sync-version.ts`. The break only surfaced when *all four PRs were
actually combined on `main`* and `version:sync` was run for real, in a
dedicated final verification pass — one that wasn't required by anything,
was't part of any individual PR's checklist, and almost didn't happen.

**Lesson**: green CI on N individually-correct PRs is not evidence that
their *combination* is correct, especially when they touch the same
files or adjacent parts of the same config. After merging a batch of
interdependent PRs, do one real end-to-end pass against the actual merged
`main` — not a rebase-and-recheck of the last PR, the literal post-merge
state — before calling the batch done. This is cheap (minutes) relative to
the cost of the bug shipping unnoticed.

## Tricky decisions worth remembering the reasoning for

- **Per-package, not per-repo, versioning.** `bloqr-compiler` already had a
  working `version.ts` → `sync-version.ts` → auto-bump-PR → tag →
  release.yml pipeline, but for exactly *one* deployable artifact. Copying
  that pattern verbatim for `bloqr-core` would have created a single
  repo-wide version — which breaks the moment a second JSR package exists,
  since bumping `@bloqr/compiler-core` must never imply bumping some
  future sibling package sharing the repo. Solved with a tag-prefix
  convention (`<package-slug>-v<semver>`, e.g. `compiler-core-v1.2.3`) and
  path-filtered, per-package bump/tag workflows from the start, with a
  documented onboarding checklist for each future decomposed package.
- **What stays local vs. moves to the JSR dependency in `bloqr-compiler`.**
  Not a blind "delete anything with a same-named file in the new package"
  pass — real `diff -bBw` (whitespace-insensitive) comparisons distinguished
  pure formatting drift (safe to delete + repoint) from genuine commercial
  divergence (AGTree-powered validation, conflict detection, the rule
  optimizer — all correctly kept local). One easy-to-miss case:
  `FilterDownloader`'s `resolveCustomDownloader` default differed between
  the two copies (core defaults to a no-op; the commercial copy wired in a
  plugin registry for `s3://`/`gcs://` sources) — preserved via explicit
  dependency injection at every construction site rather than silently
  dropped when the vendored file was deleted.
- **Merge ordering for the four housekeeping PRs.** #297 (docs/README) and
  #295 (versioning) had no file overlap with anything and merged first,
  cleanly. #296 (JSDoc) and #298 (dependency swap) both touched the same
  three compiler-core files — deliberately merged last, in sequence, so
  the one real conflict got resolved exactly once, by rebasing the second
  one onto a fully-updated `main`, rather than resolved twice or resolved
  against a moving target.

## Where things stand now

- `@bloqr/compiler-core@1.0.0` is live on JSR, publishing via token auth,
  versioned via the new per-package strategy.
- `bloqr-compiler` consumes it as a real dependency, not a vendored copy.
- `bloqr-core`, `bloqr-blocklists`, and `bloqr-apiclients` are cleanly
  split with preserved history.
- Docs are current (root `README.md` rewritten, ~18 stale-path files
  fixed, JSR doc-score gaps closed).
- The versioning strategy is documented and has one working reference
  implementation, ready to be copied for each package Epic #256's
  decomposition work produces.

## Still open, worth carrying into epic planning

- `bloqr-core#291` — JSR OIDC investigation, tracking upstream
  `jsr-io/jsr#1485`. No action needed from us; revisit if/when JSR
  responds.
- `bloqr-compiler#2212` — revert the CI-only `--minimum-dependency-age 0`
  relaxation once same-day publish-then-consume settles down.
- `docs/release-guide.md` — still describes the old single-repo-wide-tag
  release process; needs a pass to reflect the new per-package tag scheme
  once there's a second package to make the distinction concrete.
- Dependabot alerts (54 open on `bloqr-core`, 126 on `bloqr-compiler` as of
  this writing) haven't been triaged as part of this restructuring — worth
  a look before the epic adds substantially more surface area (the
  Dashboard app, more compiler wrappers).
- `bloqr-core/.github/dependabot.yml` only configures the `devcontainers`
  ecosystem — no automated version-update PRs for npm, Cargo, NuGet, pip,
  or GitHub Actions themselves. Worth deciding deliberately whether that's
  intentional before the epic multiplies the number of dependency
  manifests in this repo.
