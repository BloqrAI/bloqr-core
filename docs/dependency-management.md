# Dependency & Vulnerability Management

This document covers how *automated* dependency-update and vulnerability
signals — Dependabot version-update PRs, Dependabot (security) alerts, and
the supplementary `deno-dependency-check.yml` workflow — are triaged and
worked to closure. It complements [`SECURITY.md`](../SECURITY.md), which
covers *reported* vulnerabilities (the private advisory / disclosure flow)
rather than ones GitHub or Dependabot detect on their own.

## Detection surfaces

| Surface | Scope | Cadence |
|---|---|---|
| `.github/dependabot.yml` (version updates) | `devcontainers`, `github-actions`, `npm` (root + `/website`), `cargo` (workspace), `nuget` (central package management), `pip` (`src/compilers/python`), `docker` | Weekly, Monday 09:00 America/New_York |
| Dependabot security alerts (Security tab) | Same ecosystems as above, triggered by GitHub Advisory Database matches | Continuous |
| `.github/workflows/deno-dependency-check.yml` | All of `src/compilers/typescript/deno.json` via `deno outdated --update` — both `jsr:`-native imports (which Dependabot has no ecosystem for at all) and that file's own `npm:` shorthand imports. Dependabot's npm ecosystem doesn't read `deno.json`/`deno.lock`, so this workflow is the *only* update coverage for that file, not a supplement to Dependabot for it | Scheduled, weekly Monday 09:00 UTC (`deno-dependency-check.yml`'s own cron, run alongside but independently of Dependabot's) |

Dependabot's npm/cargo/nuget/pip/docker ecosystems above are also the
channel through which **security alerts** *can* surface as PRs: when
GitHub's Advisory Database has a match against a vulnerable version this
repo depends on, and Dependabot can produce a compatible update for it,
Dependabot opens (or fast-tracks) a version-update PR outside the normal
weekly cadence. A PR opened this way is functionally identical to a
routine version-update PR (same bot, same `dependencies` label) — the
distinguishing signal is the linked GHSA advisory in the PR body's
changelog excerpt, not a separate label. Always read the changelog excerpt
Dependabot includes before triaging by title alone.

This PR-opening path is not automatic or exhaustive, though: an alert can
remain on the Security tab with **no** corresponding PR when no fixed
version has been published yet, or when Dependabot can't automatically
patch the manifest (a transitive dependency with no direct entry, a
version range it can't safely bump). Confirmed in practice — this repo
currently shows 80 open Dependabot alerts against only 12 open
version-update PRs. **The Security tab, not the PR list, is the source of
truth for what's outstanding**; alerts without a PR still need triage
(see step 4 below), not just the ones Dependabot already turned into PRs.

## Triage policy

Work every open automated dependency-update PR — Dependabot's or
`deno-dependency-check.yml`'s — *and* every open Security-tab alert
(including ones with no PR yet) against this decision order, most urgent
first. The `deno-dependency-check.yml` PRs won't carry a GHSA/CVE advisory
themselves (that workflow does plain `deno outdated` version bumps, not
vulnerability-driven updates), so in practice they fall under step 3
below; a JSR/npm package pulled in through `deno.json` that has its own
published advisory is still tracked as a Security-tab alert and triaged
through steps 1/2/4 like any other:

1. **Has a linked GHSA/CVE advisory, patch available, no breaking change**
   (i.e. within the PR's own semver-compatible bump) → merge as soon as CI
   is green. Don't wait for the weekly grouped batch — these are worked
   immediately, independent of day-of-week.
2. **Has a linked GHSA/CVE advisory, but the fix requires a major-version
   bump** → don't wave it through inside a group merge without reading it.
   In the `npm`/`cargo`/`nuget`/`pip` ecosystems this is safe by
   construction, since their `minor-and-patch`/`gatsby-minor-and-patch`
   groups are explicitly scoped to `minor`/`patch` update-types — a major
   bump is never silently included there. The `github-actions` ecosystem's
   `actions` group is the one exception: it matches `*` with no
   update-type restriction, so a major action version bump (e.g. #484,
   `actions-rust-lang/setup-rust-toolchain` 1→2) *can* land inside a
   grouped PR. Review the breaking changes, patch call sites in the same PR
   or a follow-up, and merge deliberately. If a fix can't land immediately,
   open a tracking issue referencing the GHSA ID and note the interim
   mitigation (or accepted risk) in that issue.
3. **No advisory, routine minor/patch bump** → these are what the weekly
   grouped Dependabot PRs (`minor-and-patch`, `gatsby-minor-and-patch`)
   exist for, and what every `deno-dependency-check.yml` PR is by
   construction. Let CI gate them; merge once green. No individual review needed
   for a clean version bump with no changelog red flags.
4. **Has an advisory, but no fix version has been published yet** (so
   there's no PR to review — this is the "alert with no PR" case above) →
   track via a GitHub Issue referencing the advisory ID rather than leaving
   it as a bare unaddressed Security-tab entry; revisit on the next weekly
   triage pass and open the update PR yourself the moment a fix ships.

A PR's age is itself a signal: **any Dependabot PR carrying a security
advisory that has been open longer than one week should be treated as
overdue**, not routine backlog — check first whether CI is actually running
against it (see below) before assuming inaction is a review decision.

## Known gaps (checked 2026-09-18)

- **`dependabot.yml`'s `/website` npm entry labels PRs `website`, but that
  label does not exist in the repository.** Dependabot posts a comment on
  every affected PR (see BloqrAI/bloqr-core#465) saying it can't apply the
  label and asking for it to be created before it will proceed with label
  handling on that ecosystem. Fix: create the `website` label
  (Settings → Labels, or `gh label create website` locally) — no
  `dependabot.yml` change needed once the label exists.
- **Dependabot-authored PRs currently show zero CI status checks**
  (confirmed against `bloqr-core#465` and `#484`, one on a same-day push).
  This blocks the triage policy above at step 1: a security-advisory PR
  can't be confidently fast-merged with no signal that it doesn't break the
  build. Note that Dependabot PRs are *not* just ordinary same-repository
  PRs from a permissions standpoint even though they aren't fork PRs:
  GitHub runs `pull_request`-triggered workflows against them with a
  read-only `GITHUB_TOKEN` and no repository secrets, specifically because
  the branch content (a bumped manifest/lockfile) is bot-authored rather
  than human-reviewed. A workflow that needs write access or a secret to
  run its checks (to push a commit, call an authenticated API, etc.) will
  correctly no-op or fail under that restricted context — that's expected,
  not a misconfiguration. But *read-only* checks (build, lint, test) should
  still run and report under that same restricted token, so `total_count:
  0` — no checks reporting at all, not checks failing — points at the
  workflow trigger/path-filter/Actions-policy layer, not the token
  restriction. Confirm which workflow trigger(s) are expected to fire
  against `dependabot/**` branches and check whether a repo/org Actions
  policy is suppressing them before assuming permissions are the cause.

## Out of scope here

- Vulnerabilities reported by a third party through the private advisory
  flow — see [`SECURITY.md`](../SECURITY.md).
- The compiled filter lists in
  [`BloqrAI/bloqr-blocklists`](https://github.com/BloqrAI/bloqr-blocklists)
  and the API clients in
  [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) —
  each is out of scope for this repo's dependency triage, same as in
  `SECURITY.md`.
