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

## Security & hardening

This crate fetches URLs supplied via untrusted filter-list configuration, so `url_security.rs` guards against SSRF: HTTPS is enforced, and both the initial URL and every redirect hop are checked (via a custom `reqwest` redirect policy, capped at 5 hops) against loopback/private/link-local/metadata address ranges before a connection is made. Downloaded content is capped at 50MB via a streaming read limit (not after buffering the full response), since a lying or absent `Content-Length` header can't be trusted.

CI enforces, on every PR touching this crate:

- `cargo clippy -D clippy::all -D clippy::correctness -D clippy::suspicious` (a hard gate, not just warnings)
- `cargo deny check` (`deny.toml` at the repo root) — dependency license compliance, `RUSTSEC` advisory/yanked-crate checks, and source-registry pinning
- A 60-second smoke-fuzz run (via `cargo-fuzz`/libFuzzer) of each target in `fuzz/fuzz_targets/` against the syntax-validation, `ValidationConfig` JSON, and `HashDatabase` JSON parsing paths — the untrusted-input surfaces this crate exposes (including across the FFI boundary from other-language callers)

To fuzz locally for longer than the CI smoke test: `cd fuzz && cargo +nightly fuzz run fuzz_syntax_content -- -max_total_time=600` (swap the target name for `fuzz_config_json` or `fuzz_hash_database_json`).

## More context

- Full source, the companion `bloqr-validator-core-cli` binary, and integration examples for every language wrapper: [`bloqr-core/src/rules-validator`](https://github.com/BloqrAI/bloqr-core/tree/main/src/rules-validator)
- Naming/versioning conventions this crate follows: [`docs/architecture/versioning-strategy.md`](https://github.com/BloqrAI/bloqr-core/blob/main/docs/architecture/versioning-strategy.md)

## License

GPL-3.0 — see [LICENSE](https://github.com/BloqrAI/bloqr-core/blob/main/LICENSE) in the `bloqr-core` repository.
