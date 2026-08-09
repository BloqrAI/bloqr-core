# BloqrAI Organization Documentation Strategy

This document outlines the recommended approach for sharing documentation and standards across all BloqrAI repositories.

## Current State (Interim)

Documentation is currently distributed across individual repositories:
- `docs/jsr-token-authentication.md` (bloqr-core)
- `docs/jsr-org-standards.md` (bloqr-core)
- Similar patterns exist in bloqr-blocklists, bloqr-apiclients, bloqr-compiler

**Limitation**: Documentation is siloed; no central discovery or cross-repo linking.

## Recommended: GitHub `.github` Organization Repository

GitHub has a special feature where creating a public `.github` repository in an organization allows centralized documentation and shared configurations.

### Setup Steps

1. **Create `BloqrAI/.github` repository** (public)
   ```bash
   git init BloqrAI/.github
   cd BloqrAI/.github
   ```

2. **Add standard organization files**:
   ```
   .github/
   ├── CONTRIBUTING.md           # Contribution guidelines
   ├── CODE_OF_CONDUCT.md        # Code of conduct
   ├── SECURITY.md               # Security policy
   ├── FUNDING.yml               # Sponsorship info
   ├── pull_request_template.md  # PR template
   ├── ISSUE_TEMPLATE/
   │   ├── bug_report.md
   │   ├── feature_request.md
   │   └── question.md
   └── docs/
       ├── jsr-standards.md      # JSR publishing standards
       ├── workflow-best-practices.md
       ├── security-practices.md
       ├── deno-best-practices.md
       └── glossary.md
   ```

3. **Add organization README**:
   ```markdown
   # BloqrAI Organizations

   Welcome to BloqrAI. This is the home for centralized documentation.

   ## Quick Links
   - [Contributing](CONTRIBUTING.md)
   - [JSR Standards](docs/jsr-standards.md)
   - [Workflow Best Practices](docs/workflow-best-practices.md)
   - [Security Practices](docs/security-practices.md)

   ## Repositories
   - [bloqr-core](https://github.com/BloqrAI/bloqr-core) - Compiler core & wrappers
   - [bloqr-blocklists](https://github.com/BloqrAI/bloqr-blocklists) - Compiled filter lists
   - [bloqr-apiclients](https://github.com/BloqrAI/bloqr-apiclients) - API client libraries
   - [bloqr-compiler](https://github.com/BloqrAI/bloqr-compiler) - Commercial compiler
   ```

4. **Link from individual repositories**:
   In each repo's README.md:
   ```markdown
   ## Contributing & Standards

   See [BloqrAI/.github](https://github.com/BloqrAI/.github) for:
   - [Contribution Guidelines](https://github.com/BloqrAI/.github/blob/main/CONTRIBUTING.md)
   - [JSR Publishing Standards](https://github.com/BloqrAI/.github/blob/main/docs/jsr-standards.md)
   - [Security Practices](https://github.com/BloqrAI/.github/blob/main/docs/security-practices.md)
   ```

### Benefits

✅ **Single source of truth** for organization standards  
✅ **Automatic pull request templates** apply to all repos  
✅ **Centralized security policy** (SECURITY.md)  
✅ **Easy to discover** - linked from org profile  
✅ **Consistent contributor experience** across all repos  
✅ **Reduced duplication** - one version of each doc  

### What Gets Centralized

| File | Scope | Example |
|------|-------|---------|
| `CONTRIBUTING.md` | Org-wide | Commit message format, PR process |
| `CODE_OF_CONDUCT.md` | Org-wide | Community standards |
| `SECURITY.md` | Org-wide | Vulnerability reporting |
| `docs/jsr-standards.md` | Org-wide + JSR | Token setup, publishing workflow |
| `docs/workflow-*.md` | Org-wide | GitHub Actions best practices |
| `ISSUE_TEMPLATE/` | Auto-applied | Bug reports, feature requests |
| `pull_request_template.md` | Auto-applied | PR checklist |

### What Stays in Individual Repos

| File | Scope | Reason |
|------|-------|--------|
| `README.md` | Per-repo | Project-specific overview |
| `docs/architecture.md` | Per-repo | Repo-specific design decisions |
| `docs/guide-*.md` | Per-repo | Repo-specific tutorials |
| `CHANGELOG.md` | Per-repo | Per-package release history |

## Alternative Approaches

### 1. GitHub Wiki (Not Recommended)
- ✅ Easy to set up
- ❌ Slower to search
- ❌ Not version-controlled like code
- ❌ Can't use in automated workflows
- ❌ Limited discoverability

### 2. Shared Private Repository
- ✅ Version-controlled
- ❌ Not discoverable by public contributors
- ❌ Requires special access
- ❌ Can't use for public policy (SECURITY.md, CODE_OF_CONDUCT.md)

### 3. Organization Pages (GitHub.io)
- ✅ Professional appearance
- ❌ Overkill for standards docs
- ❌ Adds deployment complexity
- ❌ Different from code workflow

### 4. Individual Repo Docs (Current)
- ✅ Easy to get started
- ✅ Per-repo flexibility
- ❌ Duplicated content
- ❌ No single discovery point
- ❌ Harder to keep in sync

## Migration Path

### Phase 1 (Now - Epic 284 completion)
- Keep docs in individual repos as they are
- Link to `docs/jsr-token-authentication.md` from each repo
- Document the pattern (this file)

### Phase 2 (Future)
1. Create `BloqrAI/.github` repository
2. Move organization-level docs there (JSR standards, workflows, etc.)
3. Update each repo to link back to `.github` repo
4. Archive duplicated docs (with redirect links)

### Phase 3 (Polish)
- Add GitHub Pages or GitHub Wiki for searchable docs
- Integrate with org profile
- Set up automated documentation site

## Implementation for JSR Standards

**Current**:
```
bloqr-core/docs/jsr-token-authentication.md
bloqr-core/docs/jsr-org-standards.md
```

**Future**:
```
BloqrAI/.github/docs/jsr-standards.md

# Then in each repo's README:
See [JSR Standards](https://github.com/BloqrAI/.github/blob/main/docs/jsr-standards.md)
```

## GitHub Organization Features That Support This

1. **Centralized `.github` repo**: Automatic PR templates, issue templates
2. **Organization README**: Displays on org profile page
3. **Organization-level Actions secrets**: Shared across all repos (JSR tokens)
4. **Organization-level branch protection rules**: Consistent security policies
5. **Organization-level rulesets**: Enforce commit signing, branch naming, etc.

## Next Steps

1. ✅ Document current standards in individual repos (this PR)
2. ⏳ Create `BloqrAI/.github` repository (separate task)
3. ⏳ Migrate organization-level docs to `.github` repo
4. ⏳ Update all repos to link back to `.github`
5. ⏳ Set up org-wide GitHub Pages or Wiki

## Related Documentation

- [GitHub `.github` Repository Docs](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/creating-a-default-community-health-file)
- [GitHub Organization Profiles](https://docs.github.com/en/organizations/setting-up-and-managing-your-github-organization-or-enterprise)
- [GitHub Actions Reusable Workflows](https://docs.github.com/en/actions/using-workflows/reusing-workflows) (for shared CI/CD)

---

**Owned By**: @BloqrAI/core-team  
**Status**: Interim strategy (Phase 1 — awaiting Phase 2 implementation)  
**Last Updated**: 2026-08-09
