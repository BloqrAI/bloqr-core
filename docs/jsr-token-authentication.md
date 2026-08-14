# JSR Token Authentication

This document describes how BloqrAI uses JSR tokens for package publishing and consumption in GitHub Actions workflows.

## Overview

`publish-jsr.yml`'s actual publish step (`deno publish`) now authenticates via **JSR OIDC** ("trusted publishing") — GitHub Actions' own OIDC identity, no token involved. See "Publishing via OIDC (current)" below. `JSR_WORKFLOW_TOKEN` remains documented here as the fallback mechanism (used briefly while OIDC was blocked — see "Known Issues"), and `JSR_API_TOKEN` is still the mechanism for *consuming* `@bloqr/*` packages, which OIDC doesn't cover.

JSR provides two types of tokens:

1. **Workflow Tokens** (`JSR_WORKFLOW_TOKEN`): Fallback for publishing packages from CI/CD pipelines when OIDC isn't usable. Scoped to specific namespaces/scopes (e.g., `@bloqr`).
2. **API Tokens** (`JSR_API_TOKEN`): Used for programmatic API access and package consumption (e.g., adding JSR packages as dependencies). General-purpose authentication.

Both tokens are stored as **organization-level GitHub Action secrets** in the BloqrAI organization and are scoped to the `@bloqr` JSR namespace.

## Publishing via OIDC (current)

`publish-jsr.yml`'s `publish` job has `permissions: id-token: write` and runs plain `deno publish` (no `--token`). `deno publish` auto-detects it's running in GitHub Actions with that permission and authenticates via GitHub's OIDC token — no secret needed, nothing to rotate. This requires `@bloqr/compiler-core` to be linked to `BloqrAI/bloqr-core` in JSR's package settings (already done).

Live-verified end-to-end on 2026-08-14: a real `@bloqr/compiler-core@1.2.1` patch release published successfully via OIDC after this org's GitHub Enterprise account's custom-OIDC-issuer setting was disabled (see "Known Issues").

## Token Security & Rotation

- Tokens are stored as **encrypted organization secrets** in GitHub
- They are only accessible to workflows within the BloqrAI organization
- **Tokens must be rotated regularly** (recommend quarterly or when staff changes)
- Never hardcode tokens; always use `${{ secrets.JSR_WORKFLOW_TOKEN }}` or `${{ secrets.JSR_API_TOKEN }}`

## Publishing with Workflow Tokens (fallback)

`publish-jsr.yml` does not use this today (see "Publishing via OIDC" above) — kept here in case OIDC breaks again (e.g. the Enterprise custom-issuer setting getting re-enabled) and a quick fallback is needed.

### Configuration

The `JSR_WORKFLOW_TOKEN` is configured in:
- **Location**: BloqrAI GitHub organization secrets (Settings > Secrets and variables > Actions)
- **Scope**: `@bloqr` namespace (access to all packages under `@bloqr/*`)
- **Permissions**: Publish packages

### Usage in Workflows

Pass the token via the `--token` flag to `deno publish`:

```yaml
- name: Publish to JSR
  run: deno publish --token ${{ secrets.JSR_WORKFLOW_TOKEN }}
```

**Important**: Use the `--token` flag (not environment variable) — this is how `deno publish` accepts the token.

### Reverting to this fallback

If OIDC starts failing again, revert `publish-jsr.yml`'s `Publish to JSR` step to the `--token` form above and remove `id-token: write` from the job's `permissions`. The rest of the workflow (checkout, Deno setup, type check, tests, lint, symbol docs, dry-run) is unaffected either way.

1. Checks out the repository
2. Sets up Deno v2.x
3. Runs type checking, tests, and linting
4. Performs a dry-run validation with `deno publish --dry-run`
5. Publishes to JSR (OIDC today; `JSR_WORKFLOW_TOKEN` if reverted)
6. Reports any failures with actionable error messages

## Consuming Packages with API Tokens

### Configuration

The `JSR_API_TOKEN` is configured in:
- **Location**: BloqrAI GitHub organization secrets (Settings > Secrets and variables > Actions)
- **Scope**: `@bloqr` namespace (read access to all packages under `@bloqr/*`)
- **Permissions**: Read/consume packages

### Usage in Workflows

Set the token as an environment variable before running Deno commands that fetch JSR packages:

```yaml
- name: Add JSR package dependency
  env:
    JSR_TOKEN: ${{ secrets.JSR_API_TOKEN }}
  run: deno add jsr:@bloqr/compiler-core
```

### Usage in Local Development

To use `JSR_API_TOKEN` locally, store it in a `.env` file (never commit to git):

```bash
export JSR_TOKEN="jsr_pat_..."
deno cache jsr:@bloqr/compiler-core
```

## Standard Pattern Across Repositories

All BloqrAI repositories that interact with JSR should follow this pattern:

1. **Publishing workflows**: Use `JSR_WORKFLOW_TOKEN` in publish steps
2. **Consuming workflows**: Use `JSR_API_TOKEN` when adding/updating JSR dependencies
3. **Documentation**: Link back to this central documentation
4. **Token rotation**: Rotate tokens quarterly or when staff changes

## Troubleshooting

### `error: Permission denied: insufficient permissions to publish`

- Check that `JSR_WORKFLOW_TOKEN` is configured as an organization secret
- Verify the token's scope includes the `@bloqr` namespace
- Confirm the package is linked to the GitHub repository in JSR settings

### `error: could not find JSR token`

- Ensure the environment variable is named `JSR_TOKEN` (not `JSR_WORKFLOW_TOKEN`)
- Verify `${{ secrets.JSR_WORKFLOW_TOKEN }}` is set correctly in the workflow

### `error: failed to resolve jsr:@bloqr/...`

- When consuming packages, ensure `JSR_API_TOKEN` is configured for read access
- Verify the package is published and accessible in JSR
- Check that `deno.lock` is not stale (regenerate if needed)

## Related Documentation

- [JSR Publishing Documentation](https://jsr.io/docs/publishing-packages)
- [JSR API Authentication](https://jsr.io/docs/api)
- [Deno Publishing Guide](https://docs.deno.com/runtime/manual/basics/packages/)
- [@bloqr/compiler-core JSR Package](https://jsr.io/@bloqr/compiler-core)

## Known Issues

### OIDC Publishing — resolved, was never a JSR bug (#291)

OIDC-based trusted publishing from GitHub Actions previously failed with `InvalidIssuer` (tracked in [bloqr-core#291](https://github.com/BloqrAI/bloqr-core/issues/291) and reported upstream as [jsr-io/jsr#1485](https://github.com/jsr-io/jsr/issues/1485)). At the time, org settings showed no OIDC policy configured — because the actual cause lives at the **GitHub Enterprise** level, not the org level, so it wasn't visible from where the investigation looked.

Root cause: this org's GitHub Enterprise account ("Bloqr Systems") had **"Use enterprise-specific issuer URL"** enabled under Enterprise Settings → Policies → Actions → OIDC Configuration. That customizes every GitHub Actions OIDC token's `iss` claim to `https://token.actions.githubusercontent.com/bloqrsystems` instead of the plain `https://token.actions.githubusercontent.com`. JSR's OIDC verifier expects the plain issuer and rejects the customized one as invalid — hence `InvalidIssuer`.

This was confirmed, not just inferred: crates.io's Trusted Publishing hit the *identical* failure mode against the same repo at the same time, but with a more descriptive rejection message (`Unsupported JWT issuer: https://token.actions.githubusercontent.com/bloqrsystems`) that named the actual issuer string. That setting was disabled org-wide, and both crates.io and JSR OIDC were then live-verified end-to-end with real version publishes — no JSR-side or Deno-side fix was needed.

**If `InvalidIssuer` reappears**: check Enterprise Settings → Policies → Actions → OIDC Configuration → "Use enterprise-specific issuer URL" before assuming JSR or this workflow regressed.

## Token Rotation Checklist

When rotating tokens:

- [ ] Create new `JSR_WORKFLOW_TOKEN` on JSR
- [ ] Create new `JSR_API_TOKEN` on JSR
- [ ] Update both secrets in BloqrAI GitHub organization
- [ ] Test publishing workflow with new token
- [ ] Test dependency consumption with new token
- [ ] Delete old tokens from JSR
- [ ] Update this document with rotation date (optional)

---

**Last Updated**: 2026-08-14  
**Owned By**: @BloqrAI/core-team
