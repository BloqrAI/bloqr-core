# Bloqr Compiler (Swift)

macOS-native Swift wrapper for compiling AdGuard filter rules, in the same vein as the
Rust/.NET/Python wrappers: it does not reimplement compilation logic. It shells out to
[Deno](https://deno.com/) + the `@bloqr/compiler-core` JSR package (the canonical
TypeScript/Deno implementation under [`src/compilers/typescript/`](../typescript/)), the same
underlying compiler every non-TypeScript wrapper in this repository invokes.

## Package layout

A single Swift package with a library target and a thin executable target:

| Target | Directory | Contains |
|--------|-----------|----------|
| Library | [`Sources/BloqrCompilerCore/`](Sources/BloqrCompilerCore/) | `BloqrCompiler`, config reading (JSON/YAML/TOML), `VersionInfo`, hashing/rule-counting helpers |
| Executable | [`Sources/bloqr-compiler/`](Sources/bloqr-compiler/) | The `bloqr-compiler` binary (swift-argument-parser-based CLI) |

## Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| Swift | 6.0+ (Xcode 16+) | Core language, built in the Swift 6 language mode (`swift-tools-version: 6.0`); deploys back to `platforms: [.macOS(.v13)]` in `Package.swift` |
| Deno | 2.0+ | For the compilation engine (`@bloqr/compiler-core`, via `deno run jsr:@bloqr/compiler-core/cli`) |
| [`bloqr-validate`](../../validation/cli/) | latest | **Required for a successful compile by default.** This wrapper runs a mandatory, fail-closed rules-validator syntax check on compiled output (matching the other wrappers - see [Security & hardening](#security--hardening) below); install it with `cargo install bloqr-validator-core-cli`. Pass `compile --allow-unvalidated-output` to opt out instead (not recommended - compiled output then goes unchecked) |

## Building

```bash
cd src/compilers/swift
swift build                 # Debug build
swift build -c release      # Release build

swift test                  # Run all tests
```

## CLI Usage

### Configuration File Discovery

Like the other wrappers, `bloqr-compiler` searches for a configuration file when
`-c/--config` isn't given:

1. **Explicit path**: if `-c/--config` is provided, uses that file
2. **Current directory**: looks for `compiler-config.{json,yaml,toml}`
3. **Repository-specific path**: `src/compilers/typescript/compiler-config.json`
4. **Parent directories**: traverses up the directory tree looking for
   `compiler-config.{json,yaml,toml}` (like git)

### Examples

```bash
# Use default config (auto-discovery)
swift run bloqr-compiler

# Use a specific configuration file
swift run bloqr-compiler -c compiler-config.json

# Compile and copy to the rules directory
swift run bloqr-compiler -c config.json -r

# Show version info
swift run bloqr-compiler version

# Show configuration only
swift run bloqr-compiler config -c config.json

# Enable debug output
swift run bloqr-compiler -c config.json -d

# Validate configuration before compiling
swift run bloqr-compiler compile --validate

# Fail on validation warnings
swift run bloqr-compiler compile --validate --fail-on-warnings

# Show help
swift run bloqr-compiler --help
```

Once built (`swift build -c release`), the binary lives at
`.build/release/bloqr-compiler` and can be invoked directly without `swift run`.

### CLI Options

| Option | Short | Description |
|--------|-------|--------------|
| `--config PATH` | `-c` | Path to configuration file |
| `--output PATH` | `-o` | Path to output file |
| `--copy-to-rules` | `-r` | Copy output to rules directory |
| `--format FORMAT` | `-f` | Force format (`json`, `yaml`, `toml`) |
| `--debug` | `-d` | Enable debug output |
| `--version` | | Show this wrapper's own version |
| `--help` | `-h` | Show help message |

See [Dual-Engine Compilation](../../../docs/architecture/dual-engine-compilation.md) for
what `dns`/`browser` mean and how sources are routed.

### `compile` Subcommand Options

| Option | Description |
|--------|-------------|
| `--validate` | Validate configuration before compiling |
| `--fail-on-warnings` | Fail compilation if configuration has validation warnings |
| `--allow-unvalidated-output` | Opt out of the mandatory rules-validator syntax check on compiled output |
| `--engine ENGINE` | Compilation engine/grammar to use (`dns` or `browser`). Omit (or `auto`) to use the configuration's own `defaultEngine`/per-source `engine` resolution |
| `--browser-output PATH` | Output path for the browser-syntax artifact, when the configuration mixes engines. Defaults to the DNS output path with a `.browser.txt` suffix |

## Library Usage

Add to your `Package.swift`:

```swift
dependencies: [
    .package(path: "../swift") // or a git URL once published
]
```

### Basic Usage

```swift
import BloqrCompilerCore
import Foundation

let options = CompileOptions(copyToRules: true, validate: true)
let compiler = BloqrCompiler(options: options)
let result = try compiler.compile(configPath: URL(fileURLWithPath: "compiler-config.json"))

if result.success {
    print("Compiled \(result.ruleCount) rules")
    print("Output: \(result.outputPath.path)")
} else {
    print("Error: \(result.errorMessage ?? "unknown")")
}
```

### Async Usage

`BloqrCompiler` also exposes an `async` entry point alongside the synchronous one above, for
callers on Swift Concurrency's cooperative thread pool (a SwiftUI view, a Vapor route handler,
an `async` CLI command) that shouldn't block a cooperative thread on the underlying Deno
subprocess:

```swift
import BloqrCompilerCore
import Foundation

let options = CompileOptions(copyToRules: true, validate: true)
let compiler = BloqrCompiler(options: options)
let result = try await compiler.compile(configPath: URL(fileURLWithPath: "compiler-config.json"))

if result.success {
    print("Compiled \(result.ruleCount) rules")
}
```

### Reading Configuration

```swift
import BloqrCompilerCore
import Foundation

// Auto-detect format from extension
let config = try ConfigReader.readConfig(path: URL(fileURLWithPath: "config.json"))
print("Name: \(config.name)")
print("Sources: \(config.sources.count)")

// Force a specific format
let forced = try ConfigReader.readConfig(
    path: URL(fileURLWithPath: "config.txt"),
    format: .json
)
```

### Version Information

```swift
import BloqrCompilerCore

let info = VersionInfo.collect()
print("Module: \(info.moduleVersion)")
print("Platform: \(info.platform.osName) (\(info.platform.architecture))")
if let deno = info.denoVersion {
    print("Deno: \(deno)")
}
```

## Configuration Formats

JSON (and JSONC, JSON with `//` and `/* */` comments - this wrapper strips them before decoding,
so `.json`/`.jsonc` files with comments work directly, not only once handed to the underlying
`@bloqr/compiler-core` step) is the only documented configuration format. YAML and TOML remain
readable for backward compatibility but are undocumented - see
[`docs/guides/migration-guide.md`](../../../docs/guides/migration-guide.md) for converting
legacy configs to JSON.

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

### Types

| Type | Description |
|------|-------------|
| `BloqrCompiler` | Main compiler struct |
| `CompileOptions` | Compilation options |
| `CompilerResult` | Result of a compilation operation |
| `CompilerConfig` | Configuration file model |
| `FilterSource` | Source filter list definition |
| `VersionInfo` / `PlatformInfo` | Component/platform version information |
| `ConfigFormat` | `.json`, `.yaml`, `.toml` (`.yaml`/`.toml` supported for backward compatibility only) |
| `SourceType` | `.adblock`, `.hosts` |
| `CompilerError` | Error cases surfaced by this wrapper |

### Functions

| Function | Description |
|----------|-------------|
| `BloqrCompiler.compileRules(configPath:options:)` | Compile filter rules |
| `ConfigReader.readConfig(path:format:)` | Read configuration from file (auto-detects format from extension when `format` is `nil`) |
| `ConfigReader.toJSON(_:)` / `toYAML(_:)` / `toTOML(_:)` | Serialize a `CompilerConfig` back to text |
| `VersionInfo.collect()` | Collect version information for all components |
| `BloqrCompiler.countRules(path:)` | Count rules in a file |
| `BloqrCompiler.computeHash(path:)` | Compute SHA-384 hash (via `CryptoKit`) |

## Dependencies

| Package | Purpose |
|---------|---------|
| [`swift-argument-parser`](https://github.com/apple/swift-argument-parser) | CLI argument parsing |
| [`Yams`](https://github.com/jpsim/Yams) | YAML configuration support (backward compatibility) |
| [`TOMLKit`](https://github.com/LebJe/TOMLKit) | TOML configuration support (backward compatibility) |

`CryptoKit` (SHA-384 hashing) is provided by the platform SDK, not a package dependency.

## Security & hardening

The compiler subprocess is invoked via `Process` with an explicit argument array (never a
shell string), so it isn't subject to shell injection - matching the Rust wrapper's
`std::process::Command` usage. Filter-list URL fetching itself is delegated to
`@bloqr/compiler-core`; SSRF-class hardening for URL validation lives in that package and in
`bloqr-validator-core`'s `url_security.rs`.

## License

GPLv3 - See [LICENSE](../../../LICENSE) for details.
