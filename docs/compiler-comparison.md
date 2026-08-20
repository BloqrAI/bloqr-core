# Rules Compiler Comparison

This guide helps you choose the right rules compiler for your use case. All compilers produce identical output and support the same configuration schema.

## Quick Comparison

| Feature | TypeScript | .NET | Python | Rust | PowerShell | Shell |
|---------|------------|------|--------|------|------------|-------|
| Language | TypeScript | C# | Python | Rust | PowerShell | Bash/PS1 |
| Runtime | Deno 2.0+ | .NET 10 | Python 3.9+ | None | PowerShell 7+ | Bash/PowerShell |
| Config Formats | JSON/JSONC | JSON/JSONC | JSON | JSON | JSON | JSON |
| Library API | Yes | Yes | Yes | Yes | Yes | No |
| CLI | Yes | Yes | Yes | Yes | Yes | Yes |
| Interactive Mode | Yes | Yes | No | No | Yes | No |
| Tests | Deno test | xUnit | pytest | cargo test | Pester | No |
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
- Library API via `@bloqr/compiler-core/lib` (`RulesCompiler`, `ConfigurationBuilder`)

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
cd src/compilers/rust
cargo build --release
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

### Shell Scripts

**Best for**: Simple automation, CI/CD, Unix environments

```bash
./src/rules-compiler-shell/bash/compile-rules.sh -c config.json -r
```

**Pros**:
- No additional runtime (just Bash or PowerShell)
- Simple and portable
- Easy to customize

**Cons**:
- Limited error handling
- No library API

**Scripts**:

| Script | Platform |
|--------|----------|
| `compile-rules.sh` | Linux/macOS (Bash) |
| `compile-rules.ps1` | Cross-platform (PowerShell Core) |
| `compile-rules.cmd` | Windows (Batch) |

## Performance Comparison

| Compiler | Startup Time | Memory Usage | Build Time |
|----------|--------------|--------------|------------|
| TypeScript | Medium | Medium | Fast (deno cache) |
| .NET | Medium | Medium | Medium (dotnet restore) |
| Python | Medium | Low | Fast (pip install) |
| Rust | Fast | Low | Slow (cargo build) |
| PowerShell | Fast | Medium | None |
| Shell | Fast | Low | None |

*Note: Actual compilation time depends on `@bloqr/compiler-core` (the shared engine all four compilers dogfood), which is the same for all.*

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

### Choose PowerShell if:
- You're on Windows
- You need automation scripts
- You want interactive testing
- Your team uses PowerShell

### Choose Shell Scripts if:
- You need simplicity
- You're in a Unix environment
- You want easy customization
- You're setting up CI/CD

## Feature Matrix

| Feature | TypeScript | .NET | Python | Rust |
|---------|:----------:|:----:|:------:|:----:|
| **Configuration** |
| JSON | Yes | Yes | Yes | Yes |
| JSONC | Yes | Yes | No | No |
| Validation | No | Yes | No | No |
| **CLI** |
| Config file | Yes | Yes | Yes | Yes |
| Output file | Yes | Yes | Yes | Yes |
| Copy to rules | Yes | Yes | Yes | Yes |
| Debug/Verbose | Yes | Yes | Yes | Yes |
| Version | Yes | Yes | Yes | Yes |
| Help | Yes | Yes | Yes | Yes |
| **Advanced** |
| Library API | Yes | Yes | Yes | Yes |
| Interactive | Yes | Yes | No | No |
| Tests | Deno test | xUnit | pytest | cargo test |
| DI Support | No | Yes | No | No |
| Async | Yes | Yes | No | Planned |

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

# Automation with PowerShell
Invoke-BloqrCompiler -ConfigPath config.json
```
