# Security Policy

Bloqr Core compiles and validates ad-blocking filter rules across five language implementations (TypeScript, .NET, Python, Rust, PowerShell), several of which fetch and process remote filter-list content and hash-verify downloaded files. We take reports about vulnerabilities in that pipeline seriously and appreciate responsible disclosure.

## Reporting a vulnerability

**Do not open a public issue for a security vulnerability.** Public issues are searchable and would disclose the problem before a fix is available.

Instead, report privately using GitHub's private security advisory feature:

1. Go to the [Security tab](../../security) of this repository.
2. Click **"Report a vulnerability"**.
3. Fill in as much detail as you can (see "What to include" below).

This opens a private channel between you and the maintainers, separate from public issues and PRs, and lets us coordinate a fix and a coordinated disclosure timeline before anything is public.

If you're unable to use GitHub's advisory flow for some reason, contact **security@bloqr.dev** as a fallback. (This is a placeholder address — replace with a real monitored inbox before relying on it.)

### What to include

- A description of the vulnerability and its potential impact.
- Steps to reproduce, or a proof-of-concept (a minimal filter-list/config combination that triggers the issue is ideal, given the nature of this project).
- The affected component (compiler language, `bloqr-validator-core`, the PowerShell toolkit, the website, CI/CD workflows, etc.) and version/commit.
- Any known mitigations.

### What's in scope

- The four rules compilers (TypeScript/`@bloqr/compiler-core`, .NET, Python, Rust) and the PowerShell toolkit — including config-parsing, remote-source fetching, transformation, and output generation.
- `bloqr-validator-core` and `bloqr-validator-core-cli` — hash verification, URL security checks, syntax/rule validation (DNS and browser engines).
- Supply-chain issues in this repo's own build/release/CI workflows (`.github/workflows/`).
- The Gatsby documentation site (`website/`), where the issue is more than a purely cosmetic bug (e.g. XSS, dependency vulnerabilities affecting the build).

### What's out of scope

- Vulnerabilities in third-party dependencies with no Bloqr-specific exploit path — please report those upstream (though we're glad to hear about them too, especially if they affect how we use the dependency).
- The compiled filter lists themselves, which live in [`BloqrAI/bloqr-blocklists`](https://github.com/BloqrAI/bloqr-blocklists) — that's a data repository, not code.
- The AdGuard DNS API clients and Linear import tool, which live in [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients).
- Denial-of-service reports that rely purely on feeding the compiler an arbitrarily large or slow local input (resource exhaustion from a file you control locally is expected of any compiler).

If you're unsure whether something is in scope, report it anyway — we'd rather triage a borderline report than miss a real one.

## Response time

We aim to acknowledge new reports **within 5 business days**. This is a placeholder SLA — no formal security response process has been documented for this project yet, so treat this as our current best-effort target rather than a contractual guarantee. Once acknowledged, we'll work with you on a timeline for a fix and coordinated disclosure, and credit you in the advisory (unless you'd prefer to stay anonymous).

## Supported versions

This repo does not yet publish a single repo-wide version — instead, each independently-published package/crate versions on its own cadence (`@bloqr/compiler-core` on JSR, `bloqr-validator-core`/`bloqr-validator-core-cli` on crates.io, plus per-language wrapper projects; see `docs/architecture/versioning-strategy.md`). In practice:

- **Security fixes are only backported to the latest released version of each package/crate.** There is no long-term-support branch for older releases at this time.
- If you're consuming `@bloqr/compiler-core`, `bloqr-compiler` (the Rust CLI), or `bloqr-validator-core`/`bloqr-validator-core-cli`, upgrading to the latest published version is the fastest path to a fix once one ships.
- If your report affects a specific published version, please mention it — it helps us confirm which packages need a patch release.

This policy will be revisited as the versioning strategy matures (see `docs/architecture/versioning-strategy.md` for the org-wide direction).
