# Bloqr Compiler (Python)

Python API for compiling AdGuard filter rules.

## Installation

```bash
# Install from source
cd src/compilers/python
pip install -e .

# Install with development dependencies
pip install -e ".[dev]"
```

## Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| Python | 3.9+ | Core language |
| Node.js | 18+ | For compilation engine |

## CLI Usage

```bash
# Use default config (compiler-config.json)
bloqr-compiler

# Use specific configuration file
bloqr-compiler -c compiler-config.json

# Compile and copy to rules directory
bloqr-compiler -c config.json -r

# Show version info
bloqr-compiler -v

# Enable debug output
bloqr-compiler -c config.json -d

# Show help
bloqr-compiler -h

# Disable validation before compilation
bloqr-compiler -c config.json --no-validate-config

# Fail on validation warnings
bloqr-compiler -c config.json --fail-on-warnings
```

### CLI Options

| Option | Short | Description |
|--------|-------|-------------|
| `--config PATH` | `-c` | Path to configuration file |
| `--output PATH` | `-o` | Path to output file |
| `--copy-to-rules` | `-r` | Copy output to rules directory |
| `--format FORMAT` | `-f` | Force format (`json`; `yaml`/`toml` accepted for backward compatibility only) |
| `--version` | `-v` | Show version information |
| `--debug` | `-d` | Enable debug output |
| `--validate` | | Validate configuration only (no compilation) |
| `--validate-config` | | Enable configuration validation before compilation (default: true) |
| `--no-validate-config` | | Disable configuration validation before compilation |
| `--fail-on-warnings` | | Fail compilation if configuration has validation warnings |
| `--help` | `-h` | Show help message |

## Python API

### Basic Usage (Synchronous)

```python
from bloqr_compiler import BloqrCompiler

# Create compiler
compiler = BloqrCompiler()

# Compile rules
result = compiler.compile("compiler-config.json", copy_to_rules=True)

if result.success:
    print(f"Compiled {result.rule_count} rules")
    print(f"Output: {result.output_path}")
else:
    print(f"Error: {result.error_message}")
```

### Async/Await Usage (Python 3.9+)

The Python compiler now supports asynchronous operations for better performance in I/O-bound scenarios:

```python
import asyncio
from bloqr_compiler import BloqrCompiler

async def main():
    compiler = BloqrCompiler()
    
    # Use async API for better performance
    result = await compiler.compile_async(
        "compiler-config.json",
        copy_to_rules=True
    )
    
    if result.success:
        print(f"Compiled {result.rule_count} rules")
        print(f"Hash: {result.hash_short()}")
        print(f"Time: {result.elapsed_formatted()}")

# Run async function
asyncio.run(main())
```

### Parallel Processing with Async

Compile multiple configurations in parallel:

```python
import asyncio
from bloqr_compiler import compile_rules_async

async def compile_all():
    configs = ["config1.json", "config2.json", "config3.json"]
    
    # Compile all configurations in parallel
    tasks = [compile_rules_async(config) for config in configs]
    results = await asyncio.gather(*tasks)
    
    for result in results:
        if result.success:
            print(f"{result.config_name}: {result.rule_count} rules")
        else:
            print(f"Failed: {result.error_message}")

asyncio.run(compile_all())
```

### Async File Operations

Use async functions for file operations:

```python
import asyncio
from bloqr_compiler import count_rules_async, compute_hash_async

async def analyze_file(path):
    # Count rules and compute hash in parallel
    count, hash_value = await asyncio.gather(
        count_rules_async(path),
        compute_hash_async(path)
    )
    
    print(f"File: {path}")
    print(f"Rules: {count}")
    print(f"Hash: {hash_value[:32]}...")

asyncio.run(analyze_file("rules.txt"))
```

### Performance Considerations

- **Async APIs** are recommended for:
  - Large file operations
  - Processing multiple configurations
  - Integration with async frameworks (FastAPI, aiohttp, etc.)
  
- **Sync APIs** are simpler for:
  - Single compilation tasks
  - Simple scripts
  - Interactive use

**Note**: The async APIs require the `aiofiles` package for optimal performance. If not installed, they will fall back to running sync operations in a thread pool.

### Reading Configuration

```python
from bloqr_compiler import read_configuration, ConfigurationFormat

# Auto-detect format from extension
config = read_configuration("config.json")
print(f"Name: {config.name}")
print(f"Sources: {len(config.sources)}")

# Force specific format
config = read_configuration("config.txt", format=ConfigurationFormat.JSON)
```

### Version Information

```python
from bloqr_compiler import get_version_info

info = get_version_info()
print(f"Module: {info.module_version}")
print(f"Python: {info.python_version}")
print(f"Node.js: {info.node_version}")
print(f"Platform: {info.platform.os_name}")
```

### Using the Compiler Class

```python
from bloqr_compiler import BloqrCompiler, ConfigurationFormat

compiler = BloqrCompiler(debug=True)

# Read and inspect configuration
config = compiler.read_config("config.json")
print(f"Will compile {len(config.sources)} sources")

# Compile with options
result = compiler.compile(
    config_path="config.json",
    output_path="my-rules.txt",
    copy_to_rules=True,
    format=ConfigurationFormat.JSON,
)

# Access result details
print(f"Success: {result.success}")
print(f"Rules: {result.rule_count}")
print(f"Hash: {result.output_hash}")
print(f"Time: {result.elapsed_ms}ms")
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

## Running Tests

```bash
cd src/compilers/python

# Install dev dependencies
pip install -e ".[dev]"

# Run tests
pytest

# Run with coverage
pytest --cov=bloqr_compiler --cov-report=term-missing

# Run specific test file
pytest tests/test_config.py

# Run with verbose output
pytest -v
```

## Type Checking

```bash
# Run mypy
mypy bloqr_compiler
```

## Linting

```bash
# Run ruff
ruff check bloqr_compiler

# Auto-fix issues
ruff check --fix bloqr_compiler
```

## API Reference

### Classes

| Class | Description |
|-------|-------------|
| `BloqrCompiler` | Main compiler class |
| `CompilerResult` | Result of a compilation operation |
| `CompilerConfiguration` | Configuration file model |
| `FilterSource` | Source filter list definition |
| `VersionInfo` | Component version information |
| `PlatformInfo` | Platform-specific information |

### Enums

| Enum | Values |
|------|--------|
| `ConfigurationFormat` | `JSON`, `YAML`, `TOML` (YAML/TOML supported for backward compatibility only) |

### Functions

| Function | Description |
|----------|-------------|
| `compile_rules()` | Compile filter rules (functional API) |
| `read_configuration()` | Read configuration from file |
| `detect_format()` | Detect format from file extension |
| `to_json()` | Convert configuration to JSON |
| `get_version_info()` | Get version information |

## License

GPLv3 - See [LICENSE](../../LICENSE) for details.
