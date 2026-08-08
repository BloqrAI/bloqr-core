# Rules Validator Library

Centralized Rust-based validation library for AdGuard filter compilation with comprehensive security features.

## Overview

This library provides a unified, high-performance validation layer that can be used across all four rules compilers (TypeScript, .NET, Python, Rust) through native bindings, FFI, or WebAssembly.

## Features

- **At-Rest Hash Verification**: SHA-384 hashing for local files with automatic database management
- **In-Flight Hash Verification**: SHA-384 verification for downloaded files (prevents MITM attacks)
- **URL Security Validation**: HTTPS enforcement, domain validation, content verification
- **Syntax Validation**: Automatic linting for adblock and hosts file formats
- **File Conflict Handling**: Automatic renaming, overwrite, or error strategies
- **Archiving**: Timestamped archiving with manifest tracking and retention policies
- **Multi-Platform**: Native libraries + WebAssembly for cross-platform support

## Architecture

```
┌─────────────────────────────────────────────────┐
│  Compiler Frontends (TS, .NET, Python, Rust)   │
└────────────────┬────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────┐
│  rules-validator-core (Rust)                │
│  - URL security validation                     │
│  - Hash verification (at-rest & in-flight)     │
│  - Syntax validation                           │
│  - File conflict handling                      │
│  - Archiving logic                             │
└────────────────┬────────────────────────────────┘
                 │
                 ▼
┌──────────────────────────────────────────────┐
│  @bloqr/compiler-core                    │
│  (via wrapper/CLI)                           │
└──────────────────────────────────────────────┘
```

## Building

### Prerequisites

- Rust 1.75 or later
- Cargo

### Build Commands

```bash
# Build all workspace members
cargo build --release

# Build only the core library
cargo build --release -p rules-validator-core

# Build CLI tool
cargo build --release -p rules-validator-cli

# Run tests
cargo test --all

# Build with WebAssembly target
rustup target add wasm32-unknown-unknown
cargo build --release --target wasm32-unknown-unknown -p rules-validator-core
```

### Build Outputs

After running `cargo build --release`:

- **Native libraries**:
  - Linux: `target/release/librules_validator.so`
  - macOS: `target/release/librules_validator.dylib`
  - Windows: `target/release/rules_validator.dll`

- **CLI tool**: `target/release/rules-validate`

- **WebAssembly** (with wasm target):
  - `target/wasm32-unknown-unknown/release/rules_validator.wasm`

## Usage

### Rust (Native)

```rust
use rules_validator::{Validator, ValidationConfig, VerificationMode};

let config = ValidationConfig::default()
    .with_verification_mode(VerificationMode::Strict);

let mut validator = Validator::new(config);

// Validate local file
let result = validator.validate_local_file("../bloqr-blocklists/input/custom-rules.txt")?;
println!("Valid rules: {}", result.valid_rules);

// Validate remote URL
let result = validator.validate_remote_url(
    "https://example.com/list.txt",
    Some("expected_sha384_hash_here")
)?;
println!("Content hash: {:?}", result.content_hash);
```

### CLI Tool

```bash
# Validate a local file
rules-validate file ../bloqr-blocklists/input/custom-rules.txt --mode strict

# Validate a remote URL
rules-validate url https://example.com/list.txt

# Validate with hash verification
rules-validate url https://example.com/list.txt \
  --hash abc123def456...

# View hash database
rules-validate hash-db
```

## Integration Examples

### TypeScript / Node.js

#### Option 1: WebAssembly (Recommended)

```typescript
import { Validator, ValidationConfig } from './rules_validator.js';

// Load WASM module
const wasm = await import('./rules_validator_bg.wasm');

const config = new ValidationConfig();
config.set_verification_mode('strict');

const validator = new Validator(config);
const result = await validator.validate_local_file('../bloqr-blocklists/input/custom-rules.txt');

console.log(`Valid rules: ${result.valid_rules}`);
```

#### Option 2: Shell out to CLI

```typescript
import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

async function validateFile(path: string): Promise<void> {
  try {
    const { stdout } = await execAsync(`rules-validate file ${path} --mode strict`);
    console.log(stdout);
  } catch (error) {
    console.error('Validation failed:', error);
    throw error;
  }
}
```

### .NET / C#

#### Option 1: P/Invoke to Native Library

`rules-validator-core` ships a real `extern "C"` FFI surface (`src/ffi.rs`), not just this illustrative pattern — see `rules_validator.h` (generated via `cbindgen`) for the authoritative signatures. The API uses an opaque handle plus JSON-encoded results, so there's no per-field struct marshaling to keep in sync:

```csharp
using System;
using System.Runtime.InteropServices;
using System.Text.Json;

public static class NativeMethods
{
    [DllImport("rules_validator")]
    public static extern IntPtr rules_validator_new(string? configJson);

    [DllImport("rules_validator")]
    public static extern void rules_validator_free(IntPtr validator);

    [DllImport("rules_validator")]
    public static extern int rules_validator_validate_local_file(
        IntPtr validator, string path, out IntPtr outResultJson);

    [DllImport("rules_validator")]
    public static extern void rules_validator_free_string(IntPtr s);
}

public class AdGuardValidator : IDisposable
{
    private readonly IntPtr _handle;

    // Pass null for default ValidationConfig, or a JSON-serialized config.
    public AdGuardValidator(string? configJson = null)
    {
        _handle = NativeMethods.rules_validator_new(configJson);
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create validator (invalid config JSON?)");
    }

    public JsonDocument ValidateFile(string path)
    {
        var status = NativeMethods.rules_validator_validate_local_file(_handle, path, out var resultPtr);
        try
        {
            var json = Marshal.PtrToStringUTF8(resultPtr)
                ?? throw new InvalidOperationException("Null result from validator");
            var doc = JsonDocument.Parse(json);
            // status == 0 (Success): doc is a SyntaxValidationResult.
            // status == -4 (ValidationFailed): doc is {"error": "..."}.
            return doc;
        }
        finally
        {
            NativeMethods.rules_validator_free_string(resultPtr);
        }
    }

    public void Dispose() => NativeMethods.rules_validator_free(_handle);
}
```

#### Option 2: Process Execution

```csharp
using System.Diagnostics;

public class AdGuardValidator
{
    public async Task<bool> ValidateFileAsync(string path)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "rules-validate",
            Arguments = $"file {path} --mode strict",
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        
        using var process = Process.Start(psi);
        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        
        return process.ExitCode == 0;
    }
}
```

### Python

#### Option 1: PyO3 Bindings (Best Performance)

```python
# Requires building Python bindings with PyO3

from rules_validator import Validator, ValidationConfig, VerificationMode

config = ValidationConfig()
config.verification_mode = VerificationMode.STRICT

validator = Validator(config)
result = validator.validate_local_file("../bloqr-blocklists/input/custom-rules.txt")

print(f"Valid rules: {result.valid_rules}")
```

#### Option 2: Subprocess

```python
import subprocess
import json

def validate_file(path: str) -> dict:
    result = subprocess.run(
        ["rules-validate", "file", path, "--mode", "strict"],
        capture_output=True,
        text=True,
        check=True
    )
    return {"success": result.returncode == 0, "output": result.stdout}
```

## Configuration

### JSON Configuration Example

```json
{
  "hashVerification": {
    "mode": "strict",
    "requireHashesForRemote": true,
    "failOnMismatch": true,
    "hashDatabasePath": "../bloqr-blocklists/input/.hashes.json"
  },
  "archiving": {
    "enabled": true,
    "mode": "automatic",
    "retentionDays": 90,
    "archivePath": "../bloqr-blocklists/archive"
  },
  "output": {
    "path": "../bloqr-blocklists/output/adguard_user_filter.txt",
    "conflictStrategy": "rename"
  }
}
```

## Security Features

### Hash Verification Modes

- **Strict**: All remote sources must include hashes; any mismatch fails compilation
- **Warning**: Hash mismatches generate warnings but don't fail
- **Disabled**: No hash verification (testing only)

### URL Validation Checks

1. **Protocol Enforcement**: Only HTTPS allowed (HTTP rejected)
2. **Domain Validation**: DNS verification before download
3. **Content-Type Verification**: Must be text/plain
4. **Content Scanning**: First 1KB scanned for valid filter syntax
5. **Hash Verification**: Optional SHA-384 hash verification

### File Conflict Strategies

- **Rename**: Auto-increment filename (file.txt → file-1.txt → file-2.txt)
- **Overwrite**: Replace existing file
- **Error**: Fail if file exists

## Performance

- **SHA-384 hashing**: ~500 MB/s on modern hardware
- **Syntax validation**: ~1M rules/second
- **URL validation**: Limited by network speed
- **Minimal overhead**: Rust native performance

## Development

### Run Tests

```bash
cargo test --all --verbose
```

### Lint

```bash
cargo clippy --all-targets --all-features
```

### Format Code

```bash
cargo fmt --all
```

### Generate Documentation

```bash
cargo doc --no-deps --open
```

## License

GPL-3.0 - See LICENSE file for details

## Contributing

Contributions are welcome! Please ensure:
- All tests pass (`cargo test --all`)
- Code is formatted (`cargo fmt --all`)
- No clippy warnings (`cargo clippy --all-targets`)
- Documentation is updated
