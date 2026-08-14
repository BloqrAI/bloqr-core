# bloqr-validator-core

Part of the [Bloqr](https://github.com/BloqrAI) open-source ad-blocking toolkit — the core validation library for adblock- and hosts-format filter list compilation, published from [`BloqrAI/bloqr-core`](https://github.com/BloqrAI/bloqr-core/tree/main/src/rules-validator).

Provides a unified, high-performance validation layer used across every language wrapper in `bloqr-core`:

- [`@bloqr/compiler-core`](https://jsr.io/@bloqr/compiler-core) (TypeScript/Deno, JSR)
- [`Bloqr.Compiler.Core`](https://github.com/orgs/BloqrAI/packages?repo_name=bloqr-core) (.NET, NuGet via GitHub Packages)
- the Python and Rust rules compilers in `bloqr-core`

— via native Rust bindings, C FFI, or a CLI shellout, depending on the consumer's language.

## Features

- **At-Rest Hash Verification**: SHA-384 hashing for local files with automatic database management
- **In-Flight Hash Verification**: SHA-384 verification for downloaded files (prevents MITM attacks)
- **URL Security Validation**: HTTPS enforcement, domain validation, content verification
- **Syntax Validation**: Automatic linting for adblock and hosts file formats
- **File Conflict Handling**: Automatic renaming, overwrite, or error strategies
- **Archiving**: Timestamped archiving with manifest tracking and retention policies

## Usage

```toml
[dependencies]
bloqr-validator-core = "1"
```

```rust
use rules_validator::{Validator, ValidationConfig, VerificationMode};

let config = ValidationConfig::default()
    .with_verification_mode(VerificationMode::Strict);

let mut validator = Validator::new(config);

let result = validator.validate_local_file("path/to/filter-list.txt")?;
println!("Valid rules: {}", result.valid_rules);
```

The crate name is `bloqr-validator-core`; the Rust import path is `rules_validator` (the `[lib] name` in `Cargo.toml` — decoupled from the published package name so internal code and the crates.io identity can evolve independently).

## FFI

This crate ships a real `extern "C"` FFI surface (`src/ffi.rs`) — an opaque-handle-plus-JSON-string boundary designed for .NET P/Invoke or any other FFI consumer — not just a Rust-only API. See the generated `rules_validator.h` (via `cbindgen`) for the authoritative signatures.

## More context

- Full source, the companion `bloqr-validator-core-cli` binary, and integration examples for every language wrapper: [`bloqr-core/src/rules-validator`](https://github.com/BloqrAI/bloqr-core/tree/main/src/rules-validator)
- Naming/versioning conventions this crate follows: [`docs/architecture/versioning-strategy.md`](https://github.com/BloqrAI/bloqr-core/blob/main/docs/architecture/versioning-strategy.md)

## License

GPL-3.0 — see [LICENSE](https://github.com/BloqrAI/bloqr-core/blob/main/LICENSE) in the `bloqr-core` repository.
