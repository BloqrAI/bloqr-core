# Bloqr Compiler (.NET)

A .NET 10 library and console application for compiling AdGuard-syntax filter rules, built on [`@bloqr/compiler-core`](https://jsr.io/@bloqr/compiler-core) — this project shells out to it via Deno rather than reimplementing compilation logic. `@bloqr/compiler-core` is itself loosely based on AdGuard's [hostlist-compiler](https://github.com/AdguardTeam/HostlistCompiler).

## Features

- **Full compilation support**: All configuration options for filter compilation
- **JSON/JSONC configuration**: reads JSON, and detects/tolerates comments and trailing commas in `.jsonc` files (see [Configuration](#configuration))
- **Configuration validation**: Validates configuration before compilation with detailed error/warning reporting
- **Interactive and CLI modes**: Use interactively with menus or from command line with arguments
- **Verbose mode**: Detailed output from the compiler for debugging
- **Cross-platform**: Runs on Windows, Linux, and macOS

## Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| .NET SDK | 10.0+ | Cross-platform runtime |
| Deno | 2.0+ | Required to invoke `@bloqr/compiler-core` |

## Installation

```bash
cd src/compilers/dotnet
dotnet restore CompilerDotnet.slnx
dotnet build CompilerDotnet.slnx
```

## Usage

### Interactive Mode

Run without arguments to start interactive mode:

```bash
dotnet run --project src/Bloqr.Compiler.Dotnet.Console
```

Interactive menu options:
- **View Configuration** - Display parsed configuration details
- **Validate Configuration** - Validate configuration without compiling
- **Compile Rules** - Compile filter rules
- **Compile Rules (Verbose)** - Compile with detailed output
- **Compile and Copy to Rules** - Compile and copy output to rules directory
- **Show Available Transformations** - List all supported transformations
- **Version Info** - Show version information for all components

### Command-Line Mode

```bash
# Basic compilation
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- --config path/to/config.json

# Compile with specific output path
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- -c config.json -o output.txt

# Compile and copy to rules directory
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- -c config.json --copy

# Verbose output
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- -c config.json --verbose

# Validate configuration only
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- -c config.json --validate

# Disable validation before compilation
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- -c config.json --no-validate-config

# Fail compilation on validation warnings
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- -c config.json --fail-on-warnings

# Show version information
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- --version
```

### Command-Line Options

| Option | Short | Description |
|--------|-------|-------------|
| `--config` | `-c` | Path to configuration file (JSON or JSONC) |
| `--output` | `-o` | Path to output file |
| `--copy` | | Copy output to rules directory |
| `--verbose` | | Enable verbose output from the compiler |
| `--validate` | | Validate configuration only (no compilation) |
| `--validate-config` | | Enable configuration validation before compilation (default: true) |
| `--no-validate-config` | | Disable configuration validation before compilation |
| `--fail-on-warnings` | | Fail compilation if configuration has validation warnings |
| `--version` | `-v` | Show version information |
| `--benchmark` | | Benchmark real compilation performance, chunked vs unchunked - see [Benchmarking](#benchmarking) |

## Benchmarking

`--benchmark` compiles the canned `benchmarks/data/{small,medium,large,xlarge}.txt` datasets
through the real `IBloqrCompilerService`/`IChunkingService` pipeline - not a simulation - once
unchunked and once chunked, and reports the actual elapsed time for both. Part of
[epic #415](https://github.com/BloqrAI/bloqr-core/issues/415)'s per-compiler benchmark work; see
that issue's other sub-issues for the equivalent subcommand/switch in each of the other four
language wrappers.

```bash
# Benchmark all four canned dataset sizes, chunked vs unchunked (auto-discovers benchmarks/data)
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- --benchmark

# Just one size, with 8 duplicated sources and 8 parallel workers for the chunked run
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- --benchmark --benchmark-size large --benchmark-sources 8 --benchmark-max-parallel 8

# Machine-readable output for the root comparison script (see benchmarks/)
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- --benchmark --benchmark-json

# Point at a benchmarks/data directory explicitly (e.g. when not run from a repo checkout)
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- --benchmark --benchmark-data-dir /path/to/benchmarks/data
```

| Option | Description |
|--------|-------------|
| `--benchmark-size` | Dataset size to benchmark: `small`, `medium`, `large`, `xlarge`, or `all` (default: `all`) |
| `--benchmark-data-dir` | Directory containing the canned benchmark data (default: auto-discovered) |
| `--benchmark-sources` | Number of identical duplicated sources for the chunked run (default: 4) |
| `--benchmark-max-parallel` | Max parallel workers for the chunked run (default: CPU count, max 8) |
| `--benchmark-json` | Emit machine-readable JSON instead of a human-readable table |

Both runs cover the same total workload (`--benchmark-sources` identical copies of the dataset
file, so the only intended variable is the chunking strategy). Both the unchunked and chunked
paths resolve their compiler command through the same shared
`CommandHelper.GetBloqrCompilerCoreCommand()` (Deno + `@bloqr/compiler-core`), so a benchmark's
speedup number measures chunking overhead alone — see [#424](https://github.com/BloqrAI/bloqr-core/issues/424),
fixed for this wrapper.

`BenchmarkSuite1/` (a separate `dotnet run`-able project, already built as part of this
solution/CI) holds internal micro-benchmarks (`OutputWriter`'s `CountRulesAsync`/
`CopyOutputAsync`/`ComputeHashAsync`) via BenchmarkDotNet - a different tier from `--benchmark`
above: micro-benchmarks of internal hot paths vs. an end-to-end real-compilation comparison.

## Configuration

Configuration schema, all properties, transformations, and pattern-matching syntax are documented once, canonically, in [`docs/configuration-reference.md`](../../../docs/configuration-reference.md) — this compiler reads the same JSON/JSONC schema every other compiler in this repo reads. `Config/compiler-config.json` in this project is a ready-to-edit starting point.

YAML and TOML remain readable by `ConfigurationReader` for backward compatibility, but JSON/JSONC is the only documented format — see [Supported Formats](../../../docs/configuration-reference.md#supported-formats) for which compilers tolerate `.jsonc` comments today.

## Library Usage

### Basic Usage

```csharp
using Bloqr.Compiler.Dotnet.Extensions;
using Bloqr.Compiler.Abstractions;
using Microsoft.Extensions.DependencyInjection;

// Setup DI
var services = new ServiceCollection();
services.AddLogging();
services.AddBloqrCompiler();
var provider = services.BuildServiceProvider();

// Get compiler service
var compiler = provider.GetRequiredService<IBloqrCompilerService>();

// Compile with options
var options = new CompilerOptions
{
    ConfigPath = "config.json",
    OutputPath = "output.txt",
    Verbose = true,
    ValidateConfig = true
};

var result = await compiler.RunAsync(options);

if (result.Success)
{
    Console.WriteLine($"Compiled {result.RuleCount} rules");
    Console.WriteLine($"Output: {result.OutputPath}");
}
```

### Configuration Validation

```csharp
// Validate configuration before compilation
var validation = await compiler.ValidateConfigurationAsync("config.json");

if (!validation.IsValid)
{
    foreach (var error in validation.Errors)
    {
        Console.WriteLine($"Error in {error.Field}: {error.Message}");
    }
}

foreach (var warning in validation.Warnings)
{
    Console.WriteLine($"Warning in {warning.Field}: {warning.Message}");
}
```

### Reading Configuration

```csharp
var config = await compiler.ReadConfigurationAsync("config.json");
Console.WriteLine($"Filter: {config.Name}");
Console.WriteLine($"Sources: {config.Sources.Count}");
Console.WriteLine($"Transformations: {string.Join(", ", config.Transformations)}");
```

### Using TransformationHelper

```csharp
using Bloqr.Compiler.Abstractions;

// Check if transformation is valid
bool isValid = TransformationHelper.IsValid("Deduplicate"); // true

// Get all transformations
var all = TransformationHelper.AllTransformations;

// Get recommended transformations for typical use
var recommended = TransformationHelper.RecommendedTransformations;

// Get transformations optimized for hosts file sources
var hostsTransforms = TransformationHelper.HostsFileTransformations;

// Validate a list of transformations
var invalid = TransformationHelper.GetInvalidTransformations(["Valid", "Invalid"]);
```

## Library Architecture

This project is a thin, compiler-specific layer over the shared `Bloqr.Compiler.Abstractions`/`Bloqr.Compiler.Core` library, which now lives in its own solution at [`src/common/dotnet/`](../../common/dotnet/) — see that project's README for the full API reference (interfaces, models, services) of the shared library.

- **`Bloqr.Compiler.Abstractions`/`Bloqr.Compiler.Core`** (`src/common/dotnet/`) — interfaces, event-args, models, and the common implementation (config reading/validation, chunking, file locking, hash verification, the compilation event pipeline, and the plugin system). Consumed via `<ProjectReference>`, not part of this project's solution.
- **`Bloqr.Compiler.Dotnet`** (this project) — the compiler-specific pieces: `FilterCompiler` (shells out to `@bloqr/compiler-core` via Deno), `OutputWriter`, `BloqrCompilerService` (top-level orchestration). References `Bloqr.Compiler.Core`.

### Services (`Bloqr.Compiler.Dotnet`)

| Service | Description |
|---------|-------------|
| `FilterCompiler` | Executes the compiler CLI with verbose support |
| `OutputWriter` | Copies output, computes hashes, counts rules |
| `BloqrCompilerService` | Orchestrates the full pipeline with validation |

## Dependency Injection

Register all services with a single extension method:

```csharp
services.AddBloqrCompiler();
```

This registers:
- `CommandHelper`
- `IConfigurationReader` -> `ConfigurationReader`
- `IFilterCompiler` -> `FilterCompiler`
- `IOutputWriter` -> `OutputWriter`
- `IBloqrCompilerService` -> `BloqrCompilerService`

## Running Tests

```bash
cd src/compilers/dotnet
dotnet test CompilerDotnet.slnx

# With verbose output
dotnet test CompilerDotnet.slnx --verbosity detailed

# Run specific test class
dotnet test --filter "FullyQualifiedName~BloqrCompilerServiceTests"
dotnet test --filter "FullyQualifiedName~OutputWriterTests"
```

Tests for the shared library (`ConfigurationValidator`, `TransformationHelper`, etc.) live in `Bloqr.Compiler.Core.Tests` — see [`src/common/dotnet/README.md`](../../common/dotnet/README.md#running-tests).

## Environment Variables

| Variable | Description |
|----------|-------------|
| `BLOQR_COMPILER_config` | Default configuration file path |
| `BLOQR_COMPILER_Logging__LogLevel__Default` | Log level (Debug, Information, Warning, Error) |

## Project Structure

```
src/compilers/dotnet/
├── Config/                          # Default configuration files
│   ├── compiler-config.json         # JSON format (documented)
│   ├── compiler-config.yaml         # YAML format (undocumented, backward compat)
│   ├── compiler-config.toml         # TOML format (undocumented, backward compat)
│   └── compiler-config-advanced.yaml # Advanced example (YAML, undocumented)
├── src/
│   ├── Bloqr.Compiler.Dotnet/       # Compiler-specific library
│   │   ├── Extensions/              # DI extensions
│   │   └── Services/                # FilterCompiler, OutputWriter, BloqrCompilerService
│   ├── Bloqr.Compiler.Dotnet.Console/       # Console application
│   └── Bloqr.Compiler.Dotnet.Tests/         # Unit tests
└── CompilerDotnet.slnx               # Solution file
```

## Cross-Platform Notes

- Uses `System.Runtime.InteropServices.RuntimeInformation` for platform detection
- Path handling via `Path.Combine` and `Path.GetFullPath`
- UTF-8 encoding for all file operations
- Requires `deno` on PATH (invokes `@bloqr/compiler-core` via `deno run jsr:@bloqr/compiler-core/cli`)

## Related Projects

- [Compiler Common (.NET)](../../common/dotnet/) - `Bloqr.Compiler.Abstractions`/`Bloqr.Compiler.Core`, the shared library this project builds on
- [Bloqr Compiler (TypeScript)](../typescript/) - `@bloqr/compiler-core`, the canonical compilation engine this project shells out to
- [Rules Compiler (Python)](../python/) - Python implementation
- [Rules Compiler (Rust)](../rust/) - Rust implementation
- [@adguard/hostlist-compiler](https://github.com/AdguardTeam/HostlistCompiler) - the compiler `@bloqr/compiler-core` is loosely based on

## License

GPLv3 - See [LICENSE](../../../LICENSE) for details.
