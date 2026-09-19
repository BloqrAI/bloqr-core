# Rules Compiler Comparison

This guide helps you choose the right rules compiler for your use case. All compilers produce identical output and support the same configuration schema.

## Quick Comparison

| Feature | TypeScript | .NET | Python | Rust | Swift | PowerShell |
|---------|------------|------|--------|------|-------|------------|
| Language | TypeScript | C# | Python | Rust | Swift | PowerShell |
| Runtime | Deno 2.0+ | .NET 10 | Python 3.9+ | None | None (macOS/Xcode 15+) | PowerShell 7+ |
| Config Formats | JSON/JSONC | JSON/JSONC | JSON | JSON | JSON | JSON |
| Library API | Yes | Yes | Yes | Yes | Yes | Yes |
| CLI | Yes | Yes | Yes | Yes | Yes | Yes |
| Interactive Mode | Yes | Yes | No | No | No | Yes |
| Tests | Deno test | xUnit | pytest | cargo test | XCTest | Pester |
| Binary Distribution | No | No | No | Yes | No | No |

## Detailed Comparison

### TypeScript Compiler

**Best for**: TypeScript/JavaScript developers, CI/CD pipelines

```bash
cd src/compilers/typescript
deno task compile
```

**Pros**:
- Native TypeScript execution with Deno
- Built-in npm compatibility
- Canonical source of `@bloqr/compiler-core` — no subprocess overhead, no other compiler shells out to more than this
- Secure by default (explicit permissions)

**Cons**:
- Requires Deno runtime
- Slower startup than compiled languages

**Features**:
- CLI with argument parsing
- JSON configuration
- Debug output mode
- Copy to rules directory option
- Library API via `@bloqr/compiler-core/lib` (`BloqrCompiler`, `ConfigurationBuilder`)

**Library Usage**:

```typescript
import { compile } from '@bloqr/compiler-core';

const rules = await compile({
  name: 'My Filter List',
  sources: [{ source: 'https://example.com/list.txt', type: 'adblock' }],
  transformations: ['RemoveComments', 'Deduplicate'],
});
console.log(`Compiled ${rules.length} rules`);
```

### .NET Compiler

**Best for**: C# developers, enterprise environments, interactive use

```bash
cd src/compilers/dotnet
dotnet run --project src/Bloqr.Compiler.Dotnet.Console
```

**Pros**:
- Full library with dependency injection
- Interactive menu-driven mode
- Configuration validation before compilation
- Verbose mode for debugging
- Strong typing and comprehensive API

**Cons**:
- Requires .NET 10 runtime
- Larger deployment footprint

**Features**:
- Interactive Spectre.Console UI
- CLI mode with all options
- Configuration validation (`--validate`)
- Verbose output (`--verbose`)
- Library API for embedding

**Library Usage**:

```csharp
using Bloqr.Compiler.Dotnet.Extensions;
using Bloqr.Compiler.Abstractions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging();
services.AddBloqrCompiler();
var provider = services.BuildServiceProvider();

var compiler = provider.GetRequiredService<IBloqrCompilerService>();
var result = await compiler.RunAsync(new CompilerOptions
{
    ConfigPath = "config.json",
    OutputPath = "output.txt"
});
```

### Python Compiler

**Best for**: Python developers, data scientists, scripting

```bash
cd src/compilers/python
pip install -e .
bloqr-compiler -c config.json
```

**Pros**:
- Easy installation via pip
- Python API for integration
- Type hints for IDE support
- Familiar Python tooling (pytest, mypy, ruff)

**Cons**:
- Requires Python 3.9+ runtime
- Slightly slower than compiled languages

**Features**:
- CLI with argparse
- Python API for programmatic use
- Type annotations
- PyPI-ready packaging

**Library Usage**:

```python
from bloqr_compiler import BloqrCompiler, compile_rules

# Simple function
result = compile_rules("config.json")
print(f"Compiled {result.rule_count} rules")

# Class-based
compiler = BloqrCompiler()
result = compiler.compile("config.json", output_path="output.txt")
```

### Rust Compiler

**Best for**: Performance-critical use, single-binary deployment, systems integration

```bash
cargo build --release -p bloqr-compiler
./target/release/bloqr-compiler -c config.json
```

**Pros**:
- Single statically-linked binary
- Zero runtime dependencies (except Deno, required for the `@bloqr/compiler-core` engine it shells out to)
- Fastest startup time
- Small binary size with LTO
- Memory safe

**Cons**:
- Requires Rust toolchain to build
- Less familiar for non-Rust developers

**Features**:
- clap-based CLI
- Library crate for embedding
- JSON configuration
- Release builds with LTO optimization

**Library Usage**:

```rust
use bloqr_compiler::{BloqrCompiler, CompilerConfiguration};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let compiler = BloqrCompiler::new();
    let result = compiler.compile("config.json", None)?;
    println!("Compiled {} rules", result.rule_count);
    Ok(())
}
```

### Swift Compiler

**Best for**: macOS/Apple-platform developers, Swift toolchains, Xcode-based workflows

```bash
cd src/compilers/swift
swift build
swift run bloqr-compiler -c config.json
```

**Pros**:
- Native Swift Package Manager package (library + CLI)
- swift-argument-parser-based CLI with `compile`/`config`/`version` subcommands
- Type-safe `CompilerConfig`/`FilterSource` model
- SHA-384 hashing via CryptoKit
- `async`/`await` library API alongside the synchronous one, via Swift Concurrency

**Cons**:
- macOS-only (Xcode 15+) — does not build on Linux or Windows
- Requires Deno, since it shells out to the `@bloqr/compiler-core` engine like the .NET/Python/Rust wrappers
- The CLI itself still runs the synchronous API (a single sequential invocation has nothing to gain from `async`); only the library API is async

**Features**:
- swift-argument-parser CLI
- JSON configuration (YAML/TOML supported for backward compatibility only)
- Library API via `BloqrCompilerCore` (`BloqrCompiler`, `CompilerConfig`, `CompileOptions`), with both synchronous and `async` entry points
- XCTest suite

**Library Usage**:

```swift
import BloqrCompilerCore
import Foundation

let compiler = BloqrCompiler()
let configPath = URL(fileURLWithPath: "config.json")
let result = try compiler.compile(configPath: configPath)
print("Compiled \(result.ruleCount) rules")
```

Or asynchronously, from a Swift Concurrency context (a SwiftUI view, a Vapor route handler, an
`async` CLI command):

```swift
import BloqrCompilerCore
import Foundation

let compiler = BloqrCompiler()
let configPath = URL(fileURLWithPath: "config.json")
let result = try await compiler.compile(configPath: configPath)
print("Compiled \(result.ruleCount) rules")
```

### PowerShell Module

**Best for**: Windows administrators, automation scripts, cross-platform PowerShell users

```powershell
Import-Module ./src/compilers/powershell/BloqrCompiler/BloqrCompiler.psd1
Invoke-BloqrCompiler -CopyToRules
```

**Pros**:
- Native PowerShell integration
- Cross-platform (Windows, Linux, macOS)
- Interactive harness for testing
- Pester tests included
- Pipeline-friendly output

**Cons**:
- Requires PowerShell 7+

**Features**:
- Exported module functions
- Interactive test harness
- Version information
- Pipeline support

**Functions**:

```powershell
# Read configuration
$config = Read-CompilerConfiguration -Path "config.json"

# Compile rules
$result = Invoke-FilterCompiler -Config $config

# Write output
Write-CompiledOutput -Content $result.Content -Path "output.txt"

# All-in-one
Invoke-BloqrCompiler -CopyToRules

# Get version info
Get-CompilerVersion | Format-List
```

## Performance Comparison

| Compiler | Startup Time | Memory Usage | Build Time |
|----------|--------------|--------------|------------|
| TypeScript | Medium | Medium | Fast (deno cache) |
| .NET | Medium | Medium | Medium (dotnet restore) |
| Python | Medium | Low | Fast (pip install) |
| Rust | Fast | Low | Slow (cargo build) |
| Swift | Fast | Low | Medium (swift build) |
| PowerShell | Fast | Medium | None |

*Note: Actual compilation time depends on `@bloqr/compiler-core` (the shared engine all wrapper compilers dogfood), which is the same for all.*

## Decision Matrix

### Choose TypeScript if:
- You're already using Deno or TypeScript
- You want native TypeScript execution
- Your team knows JavaScript/TypeScript
- You need secure, sandboxed execution

### Choose .NET if:
- You're in a C#/.NET environment
- You want interactive menu mode
- You need configuration validation
- You want a library with DI support

### Choose Python if:
- You're in a Python environment
- You need a pip-installable package
- You want to integrate with Python scripts
- You need type hints and mypy support

### Choose Rust if:
- You need a single binary deployment
- Performance is critical
- You want zero runtime dependencies
- You're embedding in a Rust application

### Choose Swift if:
- You're on macOS and already have Xcode installed
- You're embedding compilation in a Swift/Apple-platform app or tool
- You want a type-safe Swift Package Manager library and CLI
- You need SHA-384 hashing via CryptoKit rather than a third-party crypto library

### Choose PowerShell if:
- You need cross-platform automation scripts (Windows, Linux, or macOS — PowerShell 7+ runs on all three)
- You want interactive testing
- Your team uses PowerShell
- You need simple CI/CD scripting without a language runtime to install

## Feature Matrix

| Feature | TypeScript | .NET | Python | Rust | Swift |
|---------|:----------:|:----:|:------:|:----:|:-----:|
| **Configuration** |
| JSON | Yes | Yes | Yes | Yes | Yes |
| JSONC | Yes | Yes | No | No | Yes |
| Validation | No | Yes | No | No | No |
| **CLI** |
| Config file | Yes | Yes | Yes | Yes | Yes |
| Output file | Yes | Yes | Yes | Yes | Yes |
| Copy to rules | Yes | Yes | Yes | Yes | Yes |
| Debug/Verbose | Yes | Yes | Yes | Yes | Yes |
| Version | Yes | Yes | Yes | Yes | Yes |
| Help | Yes | Yes | Yes | Yes | Yes |
| **Advanced** |
| Library API | Yes | Yes | Yes | Yes | Yes |
| Interactive | Yes | Yes | No | No | No |
| Tests | Deno test | xUnit | pytest | cargo test | XCTest |
| DI Support | No | Yes | No | No | No |
| Async | Yes | Yes | Yes | Yes | Yes |

## Migration Between Compilers

All compilers use the same configuration format, so you can:

1. Use the same config file with any compiler
2. Generate output that's identical across compilers
3. Switch compilers without changing configuration

Example workflow:
```bash
# Development with TypeScript (Deno)
deno task compile -- -c config.json -o output.txt

# CI/CD with Rust for speed
./target/release/bloqr-compiler -c config.json -o output.txt

# macOS-native workflows with Swift
swift run bloqr-compiler -c config.json

# Automation with PowerShell
Invoke-BloqrCompiler -ConfigPath config.json
```
