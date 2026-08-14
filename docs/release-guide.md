# Release Guide

This guide explains how to create a new release of the ad-blocking repository with automatically built binaries.

## Overview

The repository uses GitHub Actions to automatically build and attach binaries to releases when a new version tag is pushed. The release workflow (`release.yml`) builds the coordinated, multi-language binary bundle:

- **RulesCompiler.Console** - .NET rules compiler console app (Windows, Linux, macOS)
- **Bloqr.Dashboard.Console** - .NET Dashboard app (Windows, Linux, macOS)
- **rules-compiler** - Rust rules compiler (Windows, Linux, macOS)
- **rules-validator** - native validation library and its `rules-validate` CLI (Windows, Linux, macOS)

> AdGuard.ConsoleUI (the API client console UI) moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) — see that repo's own releases for it.

**NuGet and crates.io publish independently, not as part of this release.** `Bloqr.Compiler.Abstractions`/`Bloqr.Compiler.Core` (NuGet, via `publish-nuget.yml`) and `bloqr-validator-core` (crates.io, via `publish-crates.yml`) each have their own path-filtered workflow that publishes on every push to `main` touching that package's directory, or on demand via `workflow_dispatch` — the same pattern `publish-jsr.yml` already uses for `@bloqr/compiler-core`. See [`docs/architecture/nuget-distribution-strategy.md`](architecture/nuget-distribution-strategy.md) and [`docs/architecture/versioning-strategy.md`](architecture/versioning-strategy.md). This keeps a library-only change from having to wait for (or force) a full binary release, and vice versa.

## Creating a Release

### 1. Prepare the Release

Before creating a release, ensure:

- All changes are merged to the `main` branch
- All tests pass in CI/CD
- Version numbers are updated in project files if needed:
  - `src/rules-compiler-dotnet/src/RulesCompiler.Console/RulesCompiler.Console.csproj`
  - `src/rules-compiler-rust/Cargo.toml`
  - `src/rules-compiler-python/pyproject.toml`

### 2. Create and Push a Tag

Create a new version tag following semantic versioning (e.g., `v1.0.0`, `v1.1.0`, `v2.0.0-beta`):

```bash
# Create a new tag
git tag -a v1.0.0 -m "Release version 1.0.0"

# Push the tag to GitHub
git push origin v1.0.0
```

### 3. Wait for the Workflow to Complete

Once the tag is pushed:

1. The **Release Binaries** workflow will automatically start
2. Monitor the workflow progress at: `https://github.com/BloqrAI/bloqr-core/actions/workflows/release.yml`
3. The workflow will:
   - Build .NET executables for Windows, Linux, and macOS
   - Build Rust binaries for Windows, Linux, and macOS
   - Build Python wheel package
   - Create a GitHub release with all binaries attached

The complete workflow typically takes **15-20 minutes** to complete all builds.

### 4. Verify the Release

After the workflow completes:

1. Go to the [Releases page](https://github.com/BloqrAI/bloqr-core/releases)
2. Find your new release (e.g., `v1.0.0`)
3. Verify that all binaries are attached:
   - `RulesCompiler.Console-windows.zip`
   - `RulesCompiler.Console-linux.tar.gz`
   - `RulesCompiler.Console-macos.tar.gz`
   - `rules-compiler-rust-windows.zip`
   - `rules-compiler-rust-linux.tar.gz`
   - `rules-compiler-rust-macos.tar.gz`
   - `rules_compiler-*.whl` (Python wheel)

### 5. Edit Release Notes (Optional)

The release is created with auto-generated notes. You can edit the release to:

- Add a changelog with notable changes
- Highlight breaking changes
- Add migration instructions if needed
- Reference related issues or pull requests

## Build Artifacts

### .NET Executables

The .NET executables are built as **self-contained, single-file** binaries with trimming enabled. This means:

- No .NET runtime installation required on target systems
- Single executable file per application
- Optimized size through trimming
- Includes all dependencies

### Rust Binaries

The Rust binaries are built in **release mode** with:

- Link-Time Optimization (LTO) enabled
- Single codegen unit for maximum optimization
- Debug symbols stripped
- Minimal binary size

### Python Wheel

The Python wheel package is built as a **universal wheel** compatible with Python 3.9+.

### NuGet Packages

`Bloqr.Compiler.Abstractions` and `Bloqr.Compiler.Core` are packed with `dotnet pack` and pushed to GitHub Packages' NuGet feed (`https://nuget.pkg.github.com/BloqrAI/index.json`) by `publish-nuget.yml` — **not** by `release.yml` — triggered on every push to `main` touching `src/compiler-common-dotnet/**`, or manually via `workflow_dispatch`. Authenticated with the workflow's own `GITHUB_TOKEN` — no separate secret to manage. The push is idempotent (`--skip-duplicate`), so re-running the workflow for an already-published version is a no-op. See [`docs/architecture/nuget-distribution-strategy.md`](architecture/nuget-distribution-strategy.md) for why these two libraries are published while everything else in the .NET solution stays on in-repo project references.

### crates.io Package

Both `bloqr-validator-core` (library) and [`bloqr-validator-core-cli`](https://crates.io/crates/bloqr-validator-core-cli) (the `rules-validate` binary, installable via `cargo install bloqr-validator-core-cli`) are published to [crates.io](https://crates.io/crates/bloqr-validator-core) by `publish-crates.yml` — also independent of `release.yml` — triggered on every push to `main` touching either crate's directory, or manually via `workflow_dispatch`. The CLI job runs after the library job, since it depends on `bloqr-validator-core` via the registry. Authenticated with the `CARGO_REGISTRY_TOKEN` org-level Action secret. `cargo publish` has no native `--skip-duplicate`, so the workflow checks the crates.io API for the current version before publishing to stay idempotent. `rules-compiler` is not published — it has no external consumer as a library, and its binary already ships via this release's GitHub Release bundle. See [`docs/architecture/versioning-strategy.md`](architecture/versioning-strategy.md).

## Troubleshooting

### Workflow Fails

If the release workflow fails:

1. Check the workflow logs for error messages
2. Common issues:
   - Build failures due to compilation errors
   - Missing dependencies in project files
   - Network issues downloading dependencies
   - Insufficient permissions (requires `contents: write`)

### Missing Binaries

If some binaries are missing from the release:

1. Check the individual job logs in the workflow
2. Verify the artifact upload steps completed successfully
3. Ensure the `create-release` job downloaded all artifacts

### Rebuilding a Release

To rebuild a release:

1. Delete the existing release and tag from GitHub
2. Delete the local tag: `git tag -d v1.0.0`
3. Create a new tag and push again

## Manual Release (Alternative)

If the automated workflow is not working, you can manually build and release:

### Build .NET Executables

```bash
# Rules Compiler Console
cd src/rules-compiler-dotnet/src/RulesCompiler.Console
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish/win-x64
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ./publish/linux-x64
dotnet publish -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -o ./publish/osx-x64
```

### Build Rust Binary

```bash
cd src/rules-compiler-rust
cargo build --release --target x86_64-unknown-linux-gnu
cargo build --release --target x86_64-pc-windows-msvc
cargo build --release --target x86_64-apple-darwin
```

### Build Python Wheel

```bash
cd src/rules-compiler-python
python -m build
```

### Create Release Manually

1. Go to [Create a new release](https://github.com/BloqrAI/bloqr-core/releases/new)
2. Choose your tag
3. Add release notes
4. Upload all the built binaries
5. Publish the release

## Best Practices

- **Version Numbering**: Follow [Semantic Versioning](https://semver.org/)
  - MAJOR version for incompatible API changes
  - MINOR version for new functionality in a backwards compatible manner
  - PATCH version for backwards compatible bug fixes
- **Pre-releases**: Use tags like `v1.0.0-beta`, `v1.0.0-rc1` for pre-releases
- **Testing**: Test the built binaries on all platforms before announcing the release
- **Documentation**: Update the main README.md with notable changes
- **Changelog**: Consider maintaining a CHANGELOG.md file

## Related Files

- `.github/workflows/release.yml` - Coordinated multi-language binary release workflow
- `.github/workflows/publish-nuget.yml` - Independent, path-filtered NuGet publish for the common .NET library
- `.github/workflows/publish-crates.yml` - Independent, path-filtered crates.io publish for `bloqr-validator-core`
- `src/rules-compiler-dotnet/src/RulesCompiler.Console/RulesCompiler.Console.csproj` - .NET Rules Compiler project
- `src/rules-compiler-rust/Cargo.toml` - Rust project configuration
- `src/rules-compiler-python/pyproject.toml` - Python project configuration
- `docs/architecture/nuget-distribution-strategy.md` - NuGet publishing decision record for the common .NET library
- `docs/architecture/versioning-strategy.md` - Per-package versioning standard (JSR, crates.io, NuGet)

## Support

If you encounter issues with releases, please:

1. Check existing [GitHub Issues](https://github.com/BloqrAI/bloqr-core/issues)
2. Review the [Actions workflow runs](https://github.com/BloqrAI/bloqr-core/actions)
3. Create a new issue with detailed logs if needed
