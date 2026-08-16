# bloqr-validator-core-cli

Part of the [Bloqr](https://github.com/BloqrAI) open-source ad-blocking toolkit — the `bloqr-validate` CLI, published from [`BloqrAI/bloqr-core`](https://github.com/BloqrAI/bloqr-core/tree/main/src/validation). A thin binary wrapper around [`bloqr-validator-core`](https://crates.io/crates/bloqr-validator-core) (hash verification, URL security, and syntax validation for adblock/hosts-format filter lists), used as a subprocess by the non-Rust language wrappers in `bloqr-core` (TypeScript, .NET, Python, PowerShell, shell) that don't link the native library directly.

## Install

```sh
cargo install bloqr-validator-core-cli
```

This installs the `bloqr-validate` binary.

## Usage

```sh
# Validate a local file
bloqr-validate file path/to/filter-list.txt --mode strict

# Validate a remote URL
bloqr-validate url https://example.com/list.txt

# Validate with hash verification
bloqr-validate url https://example.com/list.txt --hash <sha384-hex>

# Machine-readable output for scripting/subprocess integration
bloqr-validate file path/to/filter-list.txt --json

# View the hash database
bloqr-validate hash-db
```

## More context

- Full source, the `bloqr-validator-core` library, and integration examples for every language wrapper: [`bloqr-core/src/validation`](https://github.com/BloqrAI/bloqr-core/tree/main/src/validation)
- Naming/versioning conventions this crate follows: [`docs/architecture/versioning-strategy.md`](https://github.com/BloqrAI/bloqr-core/blob/main/docs/architecture/versioning-strategy.md)

## License

GPL-3.0 — see [LICENSE](https://github.com/BloqrAI/bloqr-core/blob/main/LICENSE) in the `bloqr-core` repository.
