# Copilot instructions: bloqr-core

Org-wide rules (security, commits, copy) apply. This repository is the open-source toolkit: five rules compilers (TypeScript, .NET, Python, Rust, Swift), a PowerShell toolkit, a shared .NET library, a Dashboard, a Rust validator, and the Gatsby docs site.

- Follow each language's own idioms; do not impose one cross-language naming pattern.
- All compilers share one config schema (schemas/compiler-config.schema.json). Changing the schema means updating every compiler and the docs. JSONC support varies by compiler: the TypeScript reader only accepts plain `.json`, `.yaml`, `.yml` or `.toml`, so never suggest a `.jsonc` config for it without stripping comments first.
- TypeScript: Deno first, JSR before npm, Bun supported. pnpm only for the Gatsby website.
- @bloqr/compiler-core keeps its JSR symbol-documentation score near 100 percent: add JSDoc for every exported symbol in the same change.
- Packages published to JSR (for example @bloqr/compiler-core) follow the per-package standard: their own VERSION, version:sync script and tag prefix. .NET packages take their version from the csproj `<Version>` and Rust crates from Cargo.toml. Never add a single repo-wide version.
- Rust: run cargo fmt and clippy for the workspace. .NET: build each .slnx separately.
