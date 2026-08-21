# NuGet Distribution Strategy for the Common .NET Library

**Status**: Active standard.
**Scope**: `Bloqr.Compiler.Abstractions` and `Bloqr.Compiler.Core` (the "common .NET library" the epic refers to), and any future decomposed .NET library that follows the same shape.

## The question (#261)

Raised by the epic itself: *"Should the common .Net library be published to NuGet and then just get folded into the root build scripts by virtue of being referenced by each individual apps via NuGet, or should the root build scripts copy the assemblies where they need to go?"*

## Decision

**Both, for different consumers — publish to NuGet, but keep in-repo consumers on `<ProjectReference>`.**

- **In-repo consumers** (`Bloqr.Compiler.Dotnet`, `Bloqr.Dashboard.Core`, and anything else inside `bloqr-core`) keep using `<ProjectReference>` to `Bloqr.Compiler.Abstractions`/`Bloqr.Compiler.Core`, exactly as today. There is no reason to make an in-repo build round-trip through a package feed — it adds a publish-then-restore latency step to every commit for zero benefit, and `dotnet publish --self-contained` already resolves `<ProjectReference>`s into the same self-contained, xcopy-deployable output it would produce from a `<PackageReference>`. The epic's xcopy-self-contained requirement is satisfied either way; it is not a reason to prefer one over the other for code that lives in this repo.
- **Out-of-repo consumers** (a future .NET MAUI host, a third party, or — per the stated long-term plan — this library becoming its own repository) get a real NuGet package. That's the actual audience the "should we publish to NuGet" question is about: nothing in this repo needs the package feed, but something outside it eventually will, and building that path now means the eventual "extract this into its own repo" move doesn't also require standing up packaging for the first time under pressure.

This mirrors how `@bloqr/compiler-core` already works: it's published to JSR for external consumers, while every in-repo shell-out (`.NET`, Python, Rust) goes through `deno run jsr:@bloqr/compiler-core/cli` as a subprocess rather than an in-repo dependency. NuGet publishing here is the equivalent move for the .NET libraries, adapted to how .NET consumes shared code (a referenced assembly, not a subprocess).

## Feed: GitHub Packages, not nuget.org

Initial publish target is **GitHub Packages' NuGet registry** (`https://nuget.pkg.github.com/BloqrAI/index.json`), not nuget.org:

- Authenticates with the workflow's own `GITHUB_TOKEN` (`packages: write` permission) — no new secret, no new credential to rotate, no cross-service trust relationship to debug. This repo already has one live example of what the alternative costs: `@bloqr/compiler-core`'s JSR publish has been blocked on an OIDC `InvalidIssuer` investigation (`docs/jsr-token-authentication.md`) since it went live. GitHub Packages sidesteps that whole class of problem for a first cut.
- `Bloqr.Compiler.Abstractions`/`Core` aren't public-consumption libraries yet — they're internal building blocks for `bloqr-core` and (eventually) a .NET MAUI host in the same org. A GitHub Packages feed scoped to `BloqrAI` is the right visibility for that today; publishing to nuget.org before there's an external consumer just means maintaining SemVer/deprecation discipline for an audience of zero.
- Moving to nuget.org later is a config change (new feed URL, new API key secret), not a re-architecture — nothing about the package projects themselves is GitHub-Packages-specific.

## Versioning

Follows the pattern `docs/architecture/versioning-strategy.md` already established for `@bloqr/compiler-core`, adapted to what's realistic for two libraries that don't yet have the volume of independent changes JSR's compiler-core does:

- Each `.csproj`'s `<Version>` is the single source of truth (already true — see `Bloqr.Compiler.Abstractions.csproj`/`Bloqr.Compiler.Core.csproj`).
- Publish is **idempotent and triggered independently on every push to `main` that touches `src/common/dotnet/**`** (`.github/workflows/publish-nuget.yml`), not by the repo-wide `v*` release tag — that coupling was removed once `bloqr-validator-core`'s crates.io publish needed the same independence, since a library-only change shouldn't have to wait for (or force) a full multi-language binary release. This now matches `publish-jsr.yml`'s path-filtered-push pattern exactly, just without JSR's separate `version.ts`/sync-script indirection (the `.csproj` `<Version>` field already is the single source of truth, same as Cargo's). There is still no separate per-package tag prefix or automated Conventional-Commits bump workflow — `docs/architecture/versioning-strategy.md` calls that "follow-up, not blocking" work. If `Bloqr.Compiler.Abstractions`/`Core` starts changing often enough to warrant it, that's a natural graduation path, not a reason to block this simpler version on the fuller machinery landing first.
- `dotnet nuget push --skip-duplicate` makes a re-run for an already-published version a no-op rather than a failure, matching `publish-jsr.yml`'s idempotency.

## Implementation

- `.github/workflows/publish-nuget.yml`: standalone workflow, triggered by push to `main` on `src/common/dotnet/**` (or `workflow_dispatch`), running `dotnet pack` + `dotnet nuget push --source github` against `Bloqr.Compiler.Abstractions.csproj` and `Bloqr.Compiler.Core.csproj`. Runs at `permissions: packages: write` (plus `contents: read`) scoped to itself, separate from `release.yml`'s `contents: write`. `release.yml` no longer has a `publish-nuget` job — see `docs/release-guide.md`.
- Package metadata (`PackageId`, `Authors`, `Description`, `RepositoryUrl`) already exists on both csprojs from an earlier pass; this issue adds `PackageLicenseFile`/`PackageProjectUrl` so the packages render correctly wherever they're browsed.
- No change to `Bloqr.Compiler.Dotnet`, `Bloqr.Compiler.Dotnet.Console`, or `Bloqr.Dashboard.*` — they keep their existing `<ProjectReference>`s per the decision above.

## What this doc does not cover

- `Bloqr.Compiler.Dotnet` itself is compiler-specific (shells out to `@bloqr/compiler-core`), not part of "the common library" the epic asks about, and is not published to NuGet by this decision.
- An automated per-package version-bump/tag workflow for `Bloqr.Compiler.Abstractions`/`Core` (the JSR-pattern graduation mentioned above) — tracked as future work if/when warranted, not part of this issue's scope.
- PyPI/crates.io/PowerShell Gallery publishing for the other language compilers — tracked separately in #253.
