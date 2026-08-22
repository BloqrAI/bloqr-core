# Bloqr Compiler (Rust)

Rust API for compiling AdGuard filter rules.

## Crate layout

Split into a library crate and a thin CLI crate, mirroring
[`src/validation`](../../validation/)'s own `core`/`cli` split — both are
members of the repo-root Cargo workspace:

| Crate | Directory | Published as | Contains |
|-------|-----------|---------------|----------|
| Library | [`core/`](core/) | [`bloqr-compiler-core`](https://crates.io/crates/bloqr-compiler-core) | `BloqrCompiler`, config reading, chunked compilation, events - everything under "Library Usage" below |
| CLI | [`cli/`](cli/) | [`bloqr-compiler`](https://crates.io/crates/bloqr-compiler) | The `bloqr-compiler` binary (clap-based args, interactive menu, `benchmark` subcommand) |

The library's Rust import name is unchanged (`use bloqr_compiler::...`) even
though the published crate is `bloqr-compiler-core` - only the CLI kept the
pre-split registry name `bloqr-compiler`, so `cargo install bloqr-compiler`
and the `bloqr-compiler` binary itself are unaffected by the split.

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

```bash
# Install the CLI (published to crates.io as bloqr-compiler)
cargo install bloqr-compiler

# Or add the library to a Cargo.toml (published as bloqr-compiler-core;
# the import name is still `bloqr_compiler`)
cargo add bloqr-compiler-core
```

## Building (from a clone of this repository)

```bash
# From the repo root - core and cli are separate workspace members
cargo build -p bloqr-compiler-core -p bloqr-compiler
cargo build --release -p bloqr-compiler-core -p bloqr-compiler

# Run all tests for both crates
cargo test -p bloqr-compiler-core -p bloqr-compiler

# Run the CLI with debug output
cd cli
cargo run -- -d -c ../../typescript/compiler-config.json
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
# From crates.io - the import name is `bloqr_compiler` even though the
# published package is bloqr-compiler-core.
bloqr_compiler = { package = "bloqr-compiler-core", version = "1" }

# Or, from a clone of this repository:
# bloqr_compiler = { package = "bloqr-compiler-core", path = "../rust/core" }
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
| `CompilerError` | Various error types (see [`core/src/error.rs`](core/src/error.rs)) |

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
# From the repo root
cargo test -p bloqr-compiler-core           # Library tests
cargo test -p bloqr-compiler-core -- --nocapture
cargo test -p bloqr-compiler-core test_count_rules
cargo test -p bloqr-compiler                # CLI crate's own tests (config discovery)
```

## Benchmarking

`bloqr-compiler benchmark` compiles the canned `benchmarks/data/{small,medium,large,xlarge}.txt`
datasets through the real `compile_rules()`/`compile_chunks_async()` pipeline - not a
simulation - once unchunked and once chunked, and reports the actual elapsed time for both.
Part of [epic #415](https://github.com/BloqrAI/bloqr-core/issues/415)'s per-compiler
benchmark work; see that issue's other sub-issues for the equivalent subcommand/switch in
each of the other four language wrappers.

```bash
# Benchmark all four canned dataset sizes, chunked vs unchunked (auto-discovers benchmarks/data)
bloqr-compiler benchmark

# Just one size, with 8 duplicated sources and 8 parallel workers for the chunked run
bloqr-compiler benchmark --size large --sources 8 --max-parallel 8

# Machine-readable output for the root comparison script (see benchmarks/)
bloqr-compiler benchmark --json

# Point at a benchmarks/data directory explicitly (e.g. when not run from a repo checkout)
bloqr-compiler benchmark --data-dir /path/to/benchmarks/data
```

Both runs cover the same total workload (`--sources` identical copies of the dataset file, so
the only intended variable is the chunking strategy), but see
[#424](https://github.com/BloqrAI/bloqr-core/issues/424): today the unchunked and chunked
paths shell out to two different underlying compilers (Deno + `@bloqr/compiler-core` vs.
`hostlist-compiler`/`npx`), so part of any timing delta may reflect that rather than chunking
overhead alone, and each side needs its own tool on `PATH` to succeed.

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

Fuzzing (`core/fuzz/fuzz_targets/` — `fuzz_compiler_config`, covering all
three supported `CompilerConfig` formats JSON/YAML/TOML, and
`fuzz_merge_chunks`, the chunk-merge/dedup logic — the untrusted-input
surfaces this crate exposes) is **on-demand only**, not run automatically on
every PR: trigger it by hand from the Actions tab (Rust CI -> Run workflow)
when you want it exercised, e.g. before a release or after touching
untrusted-input parsing code.

Filter-list URL fetching itself is delegated to the external `hostlist-compiler` process (invoked via `std::process::Command` with argument vectors, not a shell, so it isn't subject to shell injection); SSRF-class hardening for URL validation lives in `bloqr-validator-core`'s `url_security.rs`, which this crate uses for local-file syntax/hash validation.

To fuzz locally: `cd core/fuzz && cargo +nightly fuzz run fuzz_compiler_config -- -max_total_time=600` (swap the target name for `fuzz_merge_chunks`).

## Cross-Compilation

Build the CLI binary for other platforms:

```bash
# Add target
rustup target add x86_64-pc-windows-gnu
rustup target add aarch64-apple-darwin

# Cross-compile (from the repo root, or from cli/ without -p)
cargo build --release -p bloqr-compiler --target x86_64-pc-windows-gnu
cargo build --release -p bloqr-compiler --target aarch64-apple-darwin
```

## License

GPLv3 - See [LICENSE](../../../LICENSE) for details.
