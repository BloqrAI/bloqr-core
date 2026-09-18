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
| `.github/dependabot.yml` (version updates) | `devcontainers`, `github-actions`, `npm` (root + `/website`), `cargo` (workspace), `nuget` (central package management), `pip` (`src/compilers/python`) | Weekly, Monday 09:00 America/New_York |
| Dependabot security alerts (Security tab) | Same ecosystems as above, triggered by GitHub Advisory Database matches | Continuous |
| `.github/workflows/deno-dependency-check.yml` | JSR-native imports in `src/compilers/typescript/deno.json` (`jsr:` specifiers) — the one gap Dependabot's npm ecosystem doesn't cover, since it only reads `npm:` shorthand from `deno.lock` | Scheduled (see workflow for exact cron) |

Dependabot's npm/cargo/nuget/pip ecosystems above are also the channel
through which **security alerts** surface as PRs: when GitHub's Advisory
Database has a match against a vulnerable version this repo depends on,
Dependabot opens (or fast-tracks) a version-update PR for it outside the
normal weekly cadence and, on GitHub's Security tab, lists the
corresponding alert. A PR opened this way is functionally identical to a
routine version-update PR (same bot, same `dependencies` label) — the
distinguishing signal is the linked GHSA advisory in the PR body's
changelog excerpt, not a separate label. Always read the changelog excerpt
Dependabot includes before triaging by title alone.

## Triage policy

Work every open Dependabot PR (and every open Security-tab alert) against
this decision order, most urgent first:

1. **Has a linked GHSA/CVE advisory, patch available, no breaking change**
   (i.e. within the PR's own semver-compatible bump) → merge as soon as CI
   is green. Don't wait for the weekly grouped batch — these are worked
   immediately, independent of day-of-week.
2. **Has a linked GHSA/CVE advisory, but the fix requires a major-version
   bump** → do not silently fold into a `minor-and-patch` group (it won't
   be — Dependabot's grouping in `dependabot.yml` is scoped to minor/patch
   only). Review the breaking changes, patch call sites in the same PR or a
   follow-up, and merge deliberately. If a fix can't land immediately, open
   a tracking issue referencing the GHSA ID and note the interim mitigation
   (or accepted risk) in that issue.
3. **No advisory, routine minor/patch bump** → these are what the weekly
   grouped PRs (`minor-and-patch`, `gatsby-minor-and-patch`) exist for. Let
   CI gate them; merge in batches once green. No individual review needed
   for a clean version bump with no changelog red flags.
4. **No advisory, no fix version published yet** → track via a GitHub
   Issue referencing the advisory ID (once one exists) rather than leaving
   it as an unresolved PR; revisit on the next weekly triage pass.

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
  build. Before relying on this triage policy, confirm which workflow
  trigger(s) are expected to run against `dependabot/**` branches (most
  commonly `pull_request` with default `GITHUB_TOKEN` permissions is
  sufficient for same-repo Dependabot PRs, since they aren't fork PRs) and
  check whether a repo/org Actions policy is suppressing them.

## Out of scope here

- Vulnerabilities reported by a third party through the private advisory
  flow — see [`SECURITY.md`](../SECURITY.md).
- The compiled filter lists in
  [`BloqrAI/bloqr-blocklists`](https://github.com/BloqrAI/bloqr-blocklists)
  and the API clients in
  [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) —
  each is out of scope for this repo's dependency triage, same as in
  `SECURITY.md`.
