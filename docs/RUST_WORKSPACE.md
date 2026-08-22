# Rust Workspace

This directory contains a unified Rust workspace for all Rust projects in the ad-blocking repository.

## Projects

The workspace includes the following crates:

### 1. **validation** (`src/validation/`)
Centralized validation library for AdGuard filter compilation with comprehensive security features.

- **core** (published to crates.io as `bloqr-validator-core`): Core library with validation, hashing, URL security
- **cli** (published to crates.io as `bloqr-validator-core-cli`): CLI tool (`bloqr-validate`)

> The Rust AdGuard DNS API client (`adguard-api-rust`) that used to live in this workspace moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) and is no longer a member of this Cargo workspace.

### 2. **bloqr-compiler** (`src/compilers/rust/`)
Rust compiler for AdGuard filter rules using `@bloqr/compiler-core`. Split
into a core lib crate + thin CLI crate (#173), mirroring the `validation`
workspace member's own `core`/`cli` split above.

- **core** (published to crates.io as `bloqr-compiler-core`, lib name `bloqr_compiler`): the library
- **cli** (published to crates.io as `bloqr-compiler`, unchanged binary name/registry name): the CLI
- Supports JSON, YAML, and TOML configurations

## Prerequisites

- **Rust**: 1.86 or later (enforced by `rust-toolchain.toml`)
- **Cargo**: Latest version

The toolchain will be automatically selected when you run cargo commands in this directory.

## Quick Start

```bash
# Build all projects
cargo build

# Build in release mode (optimized)
cargo build --release

# Run tests for all projects
cargo test

# Run tests with output
cargo test -- --nocapture

# Run clippy (linter)
cargo clippy --all-targets --all-features

# Format code
cargo fmt --all

# Check formatting without changes
cargo fmt --all -- --check
```

## Building Individual Projects

```bash
# Build only the validation library/CLI
cargo build -p bloqr-validator-core
cargo build -p bloqr-validator-core-cli

# Build only the compiler library/CLI
cargo build -p bloqr-compiler-core
cargo build -p bloqr-compiler
```

## Running Binaries

```bash
# Run bloqr-validate CLI
cargo run -p bloqr-validator-core-cli -- --help

# Run bloqr-compiler
cargo run -p bloqr-compiler -- --help

# Release mode (faster)
cargo run --release -p bloqr-compiler -- -c config.json
```

## Development

### Code Style

The workspace uses consistent code formatting defined in `.rustfmt.toml`:
- Max line width: 100 characters
- Unix line endings
- 4 spaces for indentation
- Automatic import reordering

### Linting

Workspace-wide lints are configured in `Cargo.toml`:
- `unsafe_code = "forbid"`: No unsafe code allowed
- `missing_docs = "warn"`: Warn about missing documentation
- Clippy pedantic and nursery lints enabled

### Configuration Files

- **`rust-toolchain.toml`**: Specifies Rust version (1.86)
- **`.cargo/config.toml`**: Build configuration
- **`.rustfmt.toml`**: Formatting rules
- **`clippy.toml`**: Clippy configuration

## Workspace Structure

```
bloqr-core/
├── Cargo.toml                      # Workspace root configuration
├── Cargo.lock                      # Locked dependencies
├── rust-toolchain.toml             # Rust version specification
├── .cargo/
│   └── config.toml                 # Cargo configuration
├── .rustfmt.toml                   # Formatting configuration
├── clippy.toml                     # Clippy configuration
└── src/
    ├── validation/
    │   ├── core/
    │   │   ├── Cargo.toml
    │   │   ├── src/
    │   │   └── fuzz/
    │   └── cli/
    │       ├── Cargo.toml
    │       └── src/
    └── compilers/
        └── rust/
            ├── core/
            │   ├── Cargo.toml
            │   ├── src/
            │   └── fuzz/
            └── cli/
                ├── Cargo.toml
                └── src/
```

## Workspace Benefits

### 1. **Unified Dependency Management**
All dependencies are managed in the root `Cargo.toml` under `[workspace.dependencies]`. This ensures:
- Consistent versions across all projects
- Easier dependency updates
- Reduced duplication

### 2. **Shared Configuration**
- Common metadata (version, authors, license)
- Unified build profiles
- Consistent linting and formatting rules

### 3. **Improved Build Performance**
- Shared build cache across projects
- Parallel compilation
- Incremental builds

### 4. **Simplified CI/CD**
- Single command to build/test all projects
- Workspace-level clippy and formatting checks

## Common Tasks

### Update Dependencies

```bash
# Check for outdated dependencies
cargo outdated

# Update dependencies (respecting semver)
cargo update

# Update to latest version (breaking changes)
cargo upgrade
```

### Generate Documentation

```bash
# Generate and open documentation for all workspace members
cargo doc --workspace --no-deps --open

# Generate docs for a specific package
cargo doc -p bloqr-validator-core --open
```

### Benchmarking

```bash
# Run benchmarks (if available)
cargo bench
```

### Clean Build Artifacts

```bash
# Clean all build artifacts
cargo clean

# Clean specific package
cargo clean -p bloqr-compiler
cargo clean -p bloqr-compiler-core
```

## CI/CD Integration

The workspace is integrated with GitHub Actions:

### Workflow: `rust-clippy.yml`

Runs on every push and pull request:
1. **Build and Test Job**:
   - Formats check (`cargo fmt --all -- --check`)
   - Build workspace (`cargo build --workspace`)
   - Run tests (`cargo test --workspace`)
   - Run clippy (`cargo clippy --workspace`)

2. **Security Analysis Job**:
   - Runs clippy with SARIF output
   - Uploads results to GitHub Security

## Troubleshooting

### Build Errors

```bash
# Clear cache and rebuild
cargo clean
cargo build

# Update Cargo.lock
cargo update
```

### Clippy Warnings

```bash
# Fix automatically fixable issues
cargo clippy --fix --allow-dirty --allow-staged
```

### Format Issues

```bash
# Apply formatting
cargo fmt --all
```

## Best Practices

1. **Always run tests before committing**:
   ```bash
   cargo test --workspace
   ```

2. **Check clippy warnings**:
   ```bash
   cargo clippy --workspace --all-features
   ```

3. **Format code**:
   ```bash
   cargo fmt --all
   ```

4. **Update dependencies regularly**:
   ```bash
   cargo update
   ```

5. **Use workspace dependencies**:
   When adding dependencies to individual crates, use `{ workspace = true }` if the dependency is in the workspace dependencies.

## Contributing

When contributing to Rust projects:

1. Ensure code builds: `cargo build --workspace`
2. Run tests: `cargo test --workspace`
3. Check formatting: `cargo fmt --all -- --check`
4. Run clippy: `cargo clippy --workspace -- -D warnings`
5. Update documentation if needed

## License

GPL-3.0 - See LICENSE file for details

## Resources

- [Rust Book](https://doc.rust-lang.org/book/)
- [Rust by Example](https://doc.rust-lang.org/rust-by-example/)
- [Cargo Book](https://doc.rust-lang.org/cargo/)
- [Clippy Lints](https://rust-lang.github.io/rust-clippy/)
- [Rustfmt](https://github.com/rust-lang/rustfmt)
