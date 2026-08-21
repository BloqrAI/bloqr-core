# Bloqr Compiler (Rust)

Rust API for compiling AdGuard filter rules.

## Features

- Fast, single-binary CLI tool
- Library for embedding in other Rust projects
- Supports JSON/JSONC configuration format (YAML and TOML remain readable for backward compatibility only)
- Cross-platform (Windows, macOS, Linux)
- Zero runtime dependencies (statically linked)

## Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| Rust | 1.85+ | Core language |
| Node.js | 18+ | For compilation engine |

## Installation

Published to [crates.io](https://crates.io/crates/bloqr-compiler) as both a library and the `bloqr-compiler` binary:

```bash
# Install the CLI
cargo install bloqr-compiler

# Or add the library to a Cargo.toml
cargo add bloqr-compiler
```

## Building (from a clone of this repository)

```bash
cd src/compilers/rust

# Debug build
cargo build

# Release build (optimized)
cargo build --release

# Run tests
cargo test

# Run with debug output
cargo run -- -d -c ../typescript/compiler-config.json
```

## CLI Usage

### Configuration File Discovery

The compiler automatically searches for configuration files in the following order:

1. **Explicit path**: If `-c/--config` is provided, uses that file
2. **Current directory**: Looks for `compiler-config.{json,yaml,toml}`
3. **Repository-specific path**: `src/compilers/typescript/compiler-config.json`
4. **Parent directories**: Traverses up the directory tree looking for `compiler-config.{json,yaml,toml}` (like git)

This means you can run the compiler from any subdirectory and it will find the nearest configuration file in the directory hierarchy.

### Examples

```bash
# Use default config (auto-discovery)
bloqr-compiler

# Use specific configuration file
bloqr-compiler -c compiler-config.json

# Compile and copy to rules directory
bloqr-compiler -c config.json -r

# Show version info
bloqr-compiler -V

# Show configuration only
bloqr-compiler config

# Interactive menu mode
bloqr-compiler -i

# Enable debug output
bloqr-compiler -c config.json -d

# Show help
bloqr-compiler --help

# Validate configuration before compiling
bloqr-compiler compile --validate

# Fail on validation warnings
bloqr-compiler compile --validate --fail-on-warnings
```

### CLI Options

| Option | Short | Description |
|--------|-------|-------------|
| `--config PATH` | `-c` | Path to configuration file |
| `--output PATH` | `-o` | Path to output file |
| `--copy-to-rules` | `-r` | Copy output to rules directory |
| `--format FORMAT` | `-f` | Force format (`json`; `yaml`/`toml` accepted for backward compatibility only) |
| `--version-info` | `-V` | Show version information |
| `--debug` | `-d` | Enable debug output |
| `--interactive` | `-i` | Run in interactive mode |
| `--help` | `-h` | Show help message |

### Compile Subcommand Options

| Option | Description |
|--------|-------------|
| `--validate` | Validate configuration before compiling |
| `--fail-on-warnings` | Fail compilation if configuration has validation warnings |

## Library Usage

Add to your `Cargo.toml`:

```toml
[dependencies]
bloqr-compiler = { path = "../rust" }
```

### Basic Usage

```rust
use bloqr_compiler::{BloqrCompiler, CompileOptions};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let options = CompileOptions::new()
        .with_copy_to_rules(true)
        .with_validation(true);

    let compiler = BloqrCompiler::with_options(options);
    let result = compiler.compile("compiler-config.json")?;

    if result.success {
        println!("Compiled {} rules", result.rule_count);
        println!("Output: {}", result.output_path_str());
    } else {
        eprintln!("Error: {:?}", result.error_message);
    }

    Ok(())
}
```

### Reading Configuration

```rust
use bloqr_compiler::{read_config, ConfigFormat};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Auto-detect format from extension
    let config = read_config("config.json", None)?;
    println!("Name: {}", config.name);
    println!("Sources: {}", config.sources.len());

    // Force a specific format
    let config = read_config("config.txt", Some(ConfigFormat::Json))?;

    Ok(())
}
```

### Version Information

```rust
use bloqr_compiler::VersionInfo;

fn main() {
    let info = VersionInfo::collect();

    println!("Module: {}", info.module_version);
    println!("Rust: {}", info.rust_version);
    println!("Platform: {}", info.platform.os_name);

    if let Some(node_version) = info.node_version {
        println!("Node.js: {}", node_version);
    }
}
```

### Helper Functions

```rust
use bloqr_compiler::{count_rules, compute_hash};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Count rules in a file
    let count = count_rules("rules.txt");
    println!("Rules: {}", count);

    // Compute SHA-384 hash
    let hash = compute_hash("output.txt")?;
    println!("Hash: {}", hash);

    Ok(())
}
```

## Configuration Formats

JSON (and JSONC, JSON with comments) is the only documented configuration format. YAML and TOML remain readable for backward compatibility but are undocumented — see [`docs/guides/migration-guide.md`](../../../docs/guides/migration-guide.md) for converting legacy configs to JSON.

### JSON

```json
{
  "name": "My Filter Rules",
  "version": "1.0.0",
  "sources": [
    { "name": "Local", "source": "./rules.txt", "type": "adblock" }
  ],
  "transformations": ["Deduplicate", "Validate"]
}
```

### YAML (backward compatibility only)

```yaml
name: My Filter Rules
version: 1.0.0
sources:
  - name: Local
    source: ./rules.txt
    type: adblock
transformations:
  - Deduplicate
  - Validate
```

### TOML (backward compatibility only)

```toml
name = "My Filter Rules"
version = "1.0.0"
transformations = ["Deduplicate", "Validate"]

[[sources]]
name = "Local"
source = "./rules.txt"
type = "adblock"
```

## API Reference

### Structs

| Struct | Description |
|--------|-------------|
| `BloqrCompiler` | Main compiler struct |
| `CompileOptions` | Builder for compilation options (`with_copy_to_rules`, `with_debug`, `with_validation`, ...) |
| `CompilerResult` | Result of a compilation operation |
| `CompilerConfig` | Configuration file model |
| `FilterSource` | Source filter list definition |
| `VersionInfo` | Component version information |
| `PlatformInfo` | Platform-specific information |

### Enums

| Enum | Values |
|------|--------|
| `ConfigFormat` | `Json`, `Yaml`, `Toml` (`Yaml`/`Toml` supported for backward compatibility only) |
| `SourceType` | `Adblock`, `Hosts` |
| `CompilerError` | Various error types (see [`src/error.rs`](src/error.rs)) |

### Functions

| Function | Description |
|----------|-------------|
| `compile_rules()` | Compile filter rules |
| `read_config()` | Read configuration from file (auto-detects format from extension when `None` is passed) |
| `to_json()` / `to_yaml()` / `to_toml()` | Serialize a `CompilerConfig` back to text |
| `VersionInfo::collect()` | Collect version information for all components |
| `count_rules()` | Count rules in a file |
| `compute_hash()` | Compute SHA-384 hash |

## Running Tests

```bash
cd src/compilers/rust

# Run all tests
cargo test

# Run with output
cargo test -- --nocapture

# Run specific test
cargo test test_count_rules
```

## Performance

The Rust implementation offers:
- Faster startup time than interpreted languages
- Lower memory usage
- Single binary distribution (no runtime dependencies)
- Native performance for file operations

## Security & hardening

CI enforces, on every PR touching this crate:

- `cargo clippy -D clippy::all -D clippy::correctness -D clippy::suspicious` (a hard gate, not just warnings)
- `cargo deny check` (`deny.toml` at the repo root) — dependency license compliance, `RUSTSEC` advisory/yanked-crate checks, and source-registry pinning; covers this crate's dependency tree alongside `bloqr-validator-core`/`bloqr-validator-core-cli`
- A 60-second smoke-fuzz run (via `cargo-fuzz`/libFuzzer) of each target in `fuzz/fuzz_targets/` — `fuzz_compiler_config` (all three supported `CompilerConfig` formats: JSON/YAML/TOML) and `fuzz_merge_chunks` (chunk-merge/dedup logic), the untrusted-input surfaces this crate exposes

Filter-list URL fetching itself is delegated to the external `hostlist-compiler` process (invoked via `std::process::Command` with argument vectors, not a shell, so it isn't subject to shell injection); SSRF-class hardening for URL validation lives in `bloqr-validator-core`'s `url_security.rs`, which this crate uses for local-file syntax/hash validation.

To fuzz locally for longer than the CI smoke test: `cd fuzz && cargo +nightly fuzz run fuzz_compiler_config -- -max_total_time=600` (swap the target name for `fuzz_merge_chunks`).

## Cross-Compilation

Build for other platforms:

```bash
# Add target
rustup target add x86_64-pc-windows-gnu
rustup target add aarch64-apple-darwin

# Cross-compile
cargo build --release --target x86_64-pc-windows-gnu
cargo build --release --target aarch64-apple-darwin
```

## License

GPLv3 - See [LICENSE](../../../LICENSE) for details.
