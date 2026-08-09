# JSR Token Authentication

This document describes how BloqrAI uses JSR tokens for package publishing and consumption in GitHub Actions workflows.

## Overview

JSR provides two types of tokens:

1. **Workflow Tokens** (`JSR_WORKFLOW_TOKEN`): Used for publishing packages from CI/CD pipelines. Scoped to specific namespaces/scopes (e.g., `@bloqr`).
2. **API Tokens** (`JSR_API_TOKEN`): Used for programmatic API access and package consumption (e.g., adding JSR packages as dependencies). General-purpose authentication.

Both tokens are stored as **organization-level GitHub Action secrets** in the BloqrAI organization and are scoped to the `@bloqr` JSR namespace.

## Token Security & Rotation

- Tokens are stored as **encrypted organization secrets** in GitHub
- They are only accessible to workflows within the BloqrAI organization
- **Tokens must be rotated regularly** (recommend quarterly or when staff changes)
- Never hardcode tokens; always use `${{ secrets.JSR_WORKFLOW_TOKEN }}` or `${{ secrets.JSR_API_TOKEN }}`

## Publishing with Workflow Tokens

### Configuration

The `JSR_WORKFLOW_TOKEN` is configured in:
- **Location**: BloqrAI GitHub organization secrets (Settings > Secrets and variables > Actions)
- **Scope**: `@bloqr` namespace (access to all packages under `@bloqr/*`)
- **Permissions**: Publish packages

### Usage in Workflows

Set the token as an environment variable before calling `deno publish`:

```yaml
- name: Publish to JSR
  env:
    JSR_TOKEN: ${{ secrets.JSR_WORKFLOW_TOKEN }}
  run: deno publish
```

**Important**: Use `JSR_TOKEN` (not `JSR_WORKFLOW_TOKEN`) as the environment variable name — this is what `deno publish` expects.

### Example: `.github/workflows/publish-jsr.yml`

See `.github/workflows/publish-jsr.yml` for the full implementation. The workflow:

1. Checks out the repository
2. Sets up Deno v2.x
3. Runs type checking, tests, and linting
4. Performs a dry-run validation with `deno publish --dry-run`
5. Publishes to JSR using the `JSR_WORKFLOW_TOKEN`
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

### OIDC Publishing Limitations (Archived)

Previous attempts to use OIDC-based trusted publishing from GitHub Actions failed with `InvalidIssuer` errors. While OIDC provides provenance attestations, token-based authentication is reliable and is the recommended approach for org-owned repositories.

See GitHub issue [bloqr-core#XXX](https://github.com/BloqrAI/bloqr-core/issues) for details on OIDC investigation.

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

**Last Updated**: 2026-08-09  
**Owned By**: @BloqrAI/core-team
