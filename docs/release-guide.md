# Release Guide

This guide explains how to create a new release of the ad-blocking repository with automatically built binaries.

## Overview

The repository uses GitHub Actions to automatically build and attach binaries to releases when a new version tag is pushed. The release workflow builds:

- **RulesCompiler.Console** - .NET rules compiler console app (Windows, Linux, macOS)
- **rules-compiler** - Rust rules compiler (Windows, Linux, macOS)
- **rules-compiler** - Python wheel package (cross-platform)

> AdGuard.ConsoleUI (the API client console UI) moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) — see that repo's own releases for it.

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
2. Monitor the workflow progress at: `https://github.com/BloqrAI/bloqr-lists/actions/workflows/release.yml`
3. The workflow will:
   - Build .NET executables for Windows, Linux, and macOS
   - Build Rust binaries for Windows, Linux, and macOS
   - Build Python wheel package
   - Create a GitHub release with all binaries attached

The complete workflow typically takes **15-20 minutes** to complete all builds.

### 4. Verify the Release

After the workflow completes:

1. Go to the [Releases page](https://github.com/BloqrAI/bloqr-lists/releases)
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

1. Go to [Create a new release](https://github.com/BloqrAI/bloqr-lists/releases/new)
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

- `.github/workflows/release.yml` - Release workflow definition
- `src/rules-compiler-dotnet/src/RulesCompiler.Console/RulesCompiler.Console.csproj` - .NET Rules Compiler project
- `src/rules-compiler-rust/Cargo.toml` - Rust project configuration
- `src/rules-compiler-python/pyproject.toml` - Python project configuration

## Support

If you encounter issues with releases, please:

1. Check existing [GitHub Issues](https://github.com/BloqrAI/bloqr-lists/issues)
2. Review the [Actions workflow runs](https://github.com/BloqrAI/bloqr-lists/actions)
3. Create a new issue with detailed logs if needed
