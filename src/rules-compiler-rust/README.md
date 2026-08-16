# Rules Compiler (Rust)

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

## Building

```bash
cd src/rules-compiler-rust

# Debug build
cargo build

# Release build (optimized)
cargo build --release

# Run tests
cargo test

# Run with debug output
cargo run -- -d -c ../filter-compiler/compiler-config.json
```

## CLI Usage

### Configuration File Discovery

The compiler automatically searches for configuration files in the following order:

1. **Explicit path**: If `-c/--config` is provided, uses that file
2. **Current directory**: Looks for `compiler-config.{json,yaml,toml}`
3. **Repository-specific path**: `src/adblock-compiler-core/compiler-config.json`
4. **Parent directories**: Traverses up the directory tree looking for `compiler-config.{json,yaml,toml}` (like git)

This means you can run the compiler from any subdirectory and it will find the nearest configuration file in the directory hierarchy.

### Examples

```bash
# Use default config (auto-discovery)
rules-compiler

# Use specific configuration file
rules-compiler -c compiler-config.json

# Compile and copy to rules directory
rules-compiler -c config.json -r

# Show version info
rules-compiler -V

# Show configuration only
rules-compiler config

# Interactive menu mode
rules-compiler -i

# Enable debug output
rules-compiler -c config.json -d

# Show help
rules-compiler --help

# Validate configuration before compiling
rules-compiler compile --validate

# Fail on validation warnings
rules-compiler compile --validate --fail-on-warnings
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
rules-compiler = { path = "../rules-compiler-rust" }
```

### Basic Usage

```rust
use rules_compiler::{RulesCompiler, ConfigurationFormat};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let compiler = RulesCompiler::new();

    let result = compiler.compile(
        "compiler-config.json",
        None,           // output_path
        true,           // copy_to_rules
        None,           // rules_directory
        None,           // format
    )?;

    if result.success {
        println!("Compiled {} rules", result.rule_count);
        println!("Output: {}", result.output_path);
    } else {
        eprintln!("Error: {:?}", result.error_message);
    }

    Ok(())
}
```

### Reading Configuration

```rust
use rules_compiler::{read_configuration, ConfigurationFormat};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Auto-detect format from extension
    let config = read_configuration("config.json", None)?;
    println!("Name: {}", config.name);
    println!("Sources: {}", config.sources.len());

    // Force specific format
    let config = read_configuration("config.txt", Some(ConfigurationFormat::Json))?;

    Ok(())
}
```

### Version Information

```rust
use rules_compiler::get_version_info;

fn main() {
    let info = get_version_info();

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
use rules_compiler::{count_rules, compute_hash};

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

JSON (and JSONC, JSON with comments) is the only documented configuration format. YAML and TOML remain readable for backward compatibility but are undocumented — see [`docs/guides/migration-guide.md`](../../docs/guides/migration-guide.md) for converting legacy configs to JSON.

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
| `RulesCompiler` | Main compiler struct |
| `CompilerResult` | Result of a compilation operation |
| `CompilerConfiguration` | Configuration file model |
| `FilterSource` | Source filter list definition |
| `VersionInfo` | Component version information |
| `PlatformInfo` | Platform-specific information |

### Enums

| Enum | Values |
|------|--------|
| `ConfigurationFormat` | `Json`, `Yaml`, `Toml` (`Yaml`/`Toml` supported for backward compatibility only) |
| `CompilerError` | Various error types |

### Functions

| Function | Description |
|----------|-------------|
| `compile_rules()` | Compile filter rules |
| `read_configuration()` | Read configuration from file |
| `detect_format()` | Detect format from file extension |
| `to_json()` | Convert configuration to JSON |
| `get_version_info()` | Get version information |
| `count_rules()` | Count rules in a file |
| `compute_hash()` | Compute SHA-384 hash |

## Running Tests

```bash
cd src/rules-compiler-rust

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

GPLv3 - See [LICENSE](../../LICENSE) for details.
