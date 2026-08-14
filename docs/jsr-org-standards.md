# BloqrAI JSR Organization Standards

This document defines the standard practices for all BloqrAI repositories that interact with the JSR (JavaScript Registry).

**Note**: This should eventually be moved to a centralized `BloqrAI/.github` repository for organization-wide documentation. For now, see `docs/jsr-token-authentication.md` in each repository.

## Organization Structure

**JSR Scope**: `@bloqr` (https://jsr.io/@bloqr)

All public packages must be published under the `@bloqr` scope. Examples:
- `@bloqr/compiler-core` - TypeScript/Deno core compiler (published from bloqr-core)
- Future JSR packages follow the same pattern

Note: not every BloqrAI package is a JSR package. The Rust validation library
(`bloqr-validator-core`) is published to crates.io instead, since JSR only
covers JavaScript/TypeScript - see `docs/RUST_WORKSPACE.md`. The two
ecosystems share the same `brand -> short-name -> core` naming convention
(`@bloqr/compiler-core` vs `bloqr-validator-core`), just expressed differently
since crates.io has no scope syntax.

## Token Management

### Token Types

| Token | Purpose | Scope | Repositories |
|-------|---------|-------|--------------|
| `JSR_WORKFLOW_TOKEN` | Publishing packages in CI/CD | `@bloqr` namespace | Publishing repos (bloqr-core) |
| `JSR_API_TOKEN` | Consuming packages programmatically | `@bloqr` namespace | All repos (for dependencies) |

### Storage

Both tokens are stored as **organization-level GitHub Action secrets**:
- **Location**: BloqrAI org Settings > Secrets and variables > Actions
- **Visibility**: Available to all repositories in the organization
- **Permissions**: Scoped to `@bloqr` namespace (cannot access other scopes)

### Security

- **Never commit tokens** to any repository
- **Never expose in logs** - GitHub Actions automatically masks known secrets
- **Rotate quarterly** or when team membership changes
- **Audit usage** - Review JWT claims and access patterns regularly

## Workflow Implementation

### Publishing Packages

Use in `.github/workflows/publish-jsr.yml`:

```yaml
- name: Publish to JSR
  run: deno publish --token ${{ secrets.JSR_WORKFLOW_TOKEN }}
```

See `docs/jsr-token-authentication.md` for complete workflow example.

### Consuming Packages

Use in workflows that add JSR dependencies:

```yaml
- name: Add JSR package
  env:
    JSR_TOKEN: ${{ secrets.JSR_API_TOKEN }}
  run: deno add jsr:@bloqr/compiler-core
```

## Publishing Strategy

### Trigger Conditions

Publishing workflows should trigger on:
- Pushes to `main` branch (when package version changes)
- Manual workflow dispatch (for testing/emergency publishes)
- **Never** on pull requests (to prevent accidental publishes)

### Version Management

- **Semantic Versioning**: All packages follow semver (MAJOR.MINOR.PATCH)
- **Version Source**: a per-package `VERSION` constant (e.g. `src/version.ts`) is the single source of truth, synced into `deno.json`'s `"version"` field by that package's `version:sync` task — never hand-edit `deno.json`'s version directly
- **Automated bumps**: Conventional Commits (`feat:`/`fix:`/`perf:`/breaking) drive automatic version-bump PRs, one workflow pair per package, path-filtered to that package's directory
- **Per-package tags**: `<package-slug>-v<semver>` (e.g. `compiler-core-v1.2.3`) — no bare `v*` tags once a repo has more than one JSR package
- **Idempotent Publishing**: `deno publish` no-ops if version already exists
- **Pre-release Versions**: Use `-rc`, `-beta`, `-alpha` suffixes for testing

See **`docs/architecture/versioning-strategy.md`** for the full standard, the reference implementation (`@bloqr/compiler-core`), and the checklist for onboarding each future decomposed package onto the same pattern.

### Dry-Run Validation

All publishing workflows must include:
```yaml
- name: Dry-run publish (validates package, catches slow types)
  run: deno publish --dry-run
```

This catches:
- Slow type checking issues before publishing
- Missing dependencies
- Configuration errors
- Breaking changes

## Repository Checklist

For each BloqrAI repository, ensure:

- [ ] **Publishing repos**: Have `.github/workflows/publish-jsr.yml` configured
- [ ] **All repos**: Link to `docs/jsr-token-authentication.md` in README
- [ ] **All repos**: Document JSR packages used in `deno.json` or `Cargo.toml`
- [ ] **All repos**: Include JSR scope (`@bloqr`) in package identifiers
- [ ] **All repos**: Never commit JSR tokens
- [ ] **Publishing repos**: Test workflow with `workflow_dispatch` before merging
- [ ] **CI/CD workflows**: Use `${{ secrets.JSR_API_TOKEN }}` for dependency fetches

## Troubleshooting Guide

| Error | Cause | Solution |
|-------|-------|----------|
| `Permission denied: insufficient permissions` | Token lacks scope access | Verify `JSR_WORKFLOW_TOKEN` has `@bloqr` scope |
| `could not find JSR token` | Env var not set or named wrong | Use `JSR_TOKEN` (not `JSR_WORKFLOW_TOKEN`) |
| `failed to resolve jsr:@bloqr/...` | Package not published or network issue | Verify package exists on jsr.io, run `deno cache` |
| `InvalidIssuer (invalidOidcToken)` | OIDC auth (deprecated) | Use token-based auth, see bloqr-core#291 |

## Future Improvements

- **Provenance Attestations**: Investigate token-based provenance support with JSR
- **Package Registry Mirror**: Mirror packages to npm for broader compatibility (future)
- **API Token Rotation**: Automate token rotation via GitHub Actions
- **Non-JSR wrapper versioning**: extend the `docs/architecture/versioning-strategy.md` pattern to the .NET/Python/Rust/PowerShell wrapper projects and their own registries (NuGet/PyPI/crates.io) — currently manual, see `docs/release-guide.md`

## Related Issues

- `bloqr-core#291` - JSR OIDC InvalidIssuer investigation (archived, token approach adopted)

## Contact

For questions about JSR standards across BloqrAI:
- Check `.github/CONTRIBUTING.md` (once org repo is created)
- Open an issue in the relevant repository
- Reference `docs/jsr-token-authentication.md` or this file

---

**Owned By**: @BloqrAI/core-team  
**Last Updated**: 2026-08-09  
**Status**: Active standard (awaiting BloqrAI/.github repo for centralization)
