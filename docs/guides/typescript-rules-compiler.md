# TypeScript Rules Compiler Guide

A comprehensive guide to using the TypeScript rules compiler with Deno 2.0+.

## Overview

The TypeScript rules compiler (`src/adblock-compiler-core/`) is a Deno-based implementation that *is* [`@bloqr/compiler-core`](https://jsr.io/@bloqr/compiler-core) — this repo's own dependency-free filter compilation engine — compiling filter lists from multiple sources with transformations, inclusions, and exclusions. See the [`@bloqr/compiler-core` Guide](adblock-compiler-guide.md) for the full architecture.

## Features

- **Native TypeScript Execution**: No build step required with Deno 2.0+
- **Multi-Format Configuration**: Support for JSON and JSONC (JSON with Comments)
- **Interactive CLI Mode**: Menu-driven interface for easy use
- **Chunked Parallel Compilation**: For large rule lists (10M+ entries)
- **Secure by Default**: Explicit permissions required with Deno
- **Comprehensive Testing**: 1008 passing tests with Deno test

## Prerequisites

| Requirement | Version | Installation |
|-------------|---------|--------------|
| Deno | 2.0+ | [deno.land](https://deno.land/) |
| @bloqr/compiler-core | 1.0.0 | Via JSR (this is `src/adblock-compiler-core/` itself, no separate install needed) |

## Installation

### Install Deno

```bash
# macOS/Linux
curl -fsSL https://deno.land/install.sh | sh

# Windows (PowerShell)
irm https://deno.land/install.ps1 | iex

# Verify installation
deno --version
```

### Clone and Setup

```bash
git clone https://github.com/BloqrAI/bloqr-core.git
cd bloqr-core/src/adblock-compiler-core

# Cache dependencies
deno cache src/mod.ts
```

## Usage

### Interactive Mode

Run without arguments to start interactive mode with a menu interface:

```bash
deno task interactive
```

**Menu Options:**
- View Configuration
- Compile Rules
- Compile Rules (Verbose)
- Compile and Copy to Rules Directory
- Show Version Info
- Exit

### CLI Mode

#### Basic Commands

```bash
# Compile with default config
deno task compile

# Compile with specific config (JSON)
deno task compile -- -c path/to/config.json

# Compile with JSONC config
deno task compile -- -c path/to/config.jsonc

# Custom output file
deno task compile -- -c config.json -o output.txt

# Compile and copy to rules directory
deno task compile -- -c config.json -r

# Debug mode with verbose output
deno task compile -- -c config.json -d

# Show version information
deno task compile -- --version

# Show help
deno task compile -- --help
```

### CLI Options

| Option | Short | Description |
|--------|-------|-------------|
| `--config PATH` | `-c` | Path to configuration file |
| `--output PATH` | `-o` | Path to output file |
| `--copy-to-rules` | `-r` | Copy output to rules directory |
| `--format FORMAT` | `-f` | Force format (json or jsonc) |
| `--debug` | `-d` | Enable debug output |
| `--version` | | Show version information |
| `--help` | | Show help message |

### Direct Script Execution

You can also run the compiler directly:

```bash
# Run directly
deno run --allow-read --allow-write --allow-run --allow-net --allow-env \
  src/mod.ts -c config.json

# Or with all permissions
deno run -A src/mod.ts -c config.json
```

## Configuration

The compiler supports JSON and JSONC (JSON with Comments) configuration formats with the same schema as all other compilers.

### Basic Configuration Example

```json
{
  "name": "My Filter List",
  "description": "Custom ad-blocking filter",
  "version": "1.0.0",
  "sources": [
    {
      "name": "EasyList",
      "source": "https://easylist.to/easylist/easylist.txt",
      "type": "adblock",
      "transformations": [
        "Validate",
        "RemoveModifiers"
      ]
    },
    {
      "name": "Local Rules",
      "source": "./my-rules.txt",
      "type": "adblock"
    }
  ],
  "transformations": [
    "Deduplicate",
    "RemoveEmptyLines",
    "TrimLines"
  - InsertFinalNewLine

exclusions:
  - "*.google.com"
  - "*facebook*"
```

### Configuration Properties

See [Configuration Reference](../configuration-reference.md) for complete documentation of all properties.

## Development

### Project Structure

```
src/adblock-compiler-core/
├── src/
│   ├── mod.ts              # Main entry point
│   ├── cli.ts              # CLI argument parsing
│   ├── compiler.ts         # Compiler implementation
│   ├── config.ts           # Configuration reader
│   ├── interactive.ts      # Interactive mode
│   └── types.ts            # TypeScript types
├── tests/
│   ├── compiler.test.ts    # Compiler tests
│   ├── config.test.ts      # Configuration tests
│   └── cli.test.ts         # CLI tests
├── Config/
│   ├── compiler-config.json
│   ├── compiler-config.yaml
│   └── compiler-config.toml
└── deno.json               # Deno configuration
```

### Running Tests

```bash
# Run all tests
deno task test

# Run specific test file
deno test tests/compiler.test.ts

# Run with coverage
deno task test:coverage

# Watch mode for development
deno task dev
```

### Linting

```bash
# Lint code
deno task lint

# Format code
deno task fmt

# Check formatting
deno fmt --check src/
```

## Available Tasks

The `deno.json` file defines these tasks:

| Task | Command | Description |
|------|---------|-------------|
| `compile` | Default compilation | Compile with default config |
| `compile:yaml` | Compile with YAML | Use YAML configuration |
| `compile:toml` | Compile with TOML | Use TOML configuration |
| `interactive` | Interactive mode | Launch interactive CLI |
| `dev` | Watch mode | Development with auto-reload |
| `test` | Run tests | Execute test suite |
| `test:coverage` | Run with coverage | Generate coverage report |
| `lint` | Lint code | Check code quality |
| `fmt` | Format code | Auto-format TypeScript |
| `fmt:check` | Check formatting | Verify formatting |

## Permissions

Deno requires explicit permissions. The compiler needs:

- `--allow-read`: Read configuration and source files
- `--allow-write`: Write output files
- `--allow-run`: Execute subprocesses (used by orchestration, not by the core compiler itself)
- `--allow-net`: Download remote filter lists
- `--allow-env`: Access environment variables (optional)

You can grant all permissions with `-A` or be more restrictive:

```bash
deno run --allow-read=. --allow-write=. --allow-run=deno,npm --allow-net \
  src/mod.ts -c config.yaml
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `DEBUG` | Enable debug logging |
| `CONFIG_PATH` | Default configuration file path |
| `RULES_DIR` | Default rules output directory |

## Examples

### Compile for AdGuard DNS

```yaml
name: AdGuard DNS Filter
description: Optimized for AdGuard DNS

sources:
  - name: AdGuard Base
    source: https://raw.githubusercontent.com/AdguardTeam/FiltersRegistry/master/filters/filter_2_Base/filter.txt
    transformations:
      - RemoveModifiers
      - Validate

transformations:
  - Deduplicate
  - RemoveEmptyLines
  - InsertFinalNewLine
```

```bash
deno task compile -- -c adguard-config.yaml -r
```

### Compile with Multiple Sources

```yaml
name: Combined Filter
version: "2.0.0"

sources:
  - name: EasyList
    source: https://easylist.to/easylist/easylist.txt
    type: adblock

  - name: EasyPrivacy
    source: https://easylist.to/easylist/easyprivacy.txt
    type: adblock

  - name: Steven Black Hosts
    source: https://raw.githubusercontent.com/StevenBlack/hosts/master/hosts
    type: hosts
    transformations:
      - Compress  # Convert hosts to adblock format

transformations:
  - Validate
  - Deduplicate
  - RemoveEmptyLines
  - InsertFinalNewLine
```

### Custom Transformations per Source

```yaml
name: Selective Transformations

sources:
  - name: Strict Source
    source: https://example.com/strict-list.txt
    transformations:
      - RemoveComments
      - Validate
      - RemoveModifiers
    exclusions:
      - "*.facebook.com"

  - name: Permissive Source
    source: https://example.com/permissive-list.txt
    inclusions:
      - "*ad*"
      - "*tracker*"

transformations:
  - Deduplicate
  - RemoveEmptyLines
  - InsertFinalNewLine
```

## Troubleshooting

### @bloqr/compiler-core not found

Ensure Deno 2.0+ is installed:

```bash
deno --version
```

Test the compiler CLI directly:

```bash
deno run --allow-read --allow-write --allow-env --allow-net --allow-run jsr:@bloqr/compiler-core/cli --version
```

### Permission Denied

If you encounter permission errors on Linux/macOS, ensure scripts are executable:

```bash
chmod +x src/mod.ts
```

Or run via deno explicitly:

```bash
deno task compile
```

### Module Not Found

If dependencies aren't cached:

```bash
# Clear cache
rm -rf ~/.cache/deno

# Re-cache dependencies
deno cache src/mod.ts
```

### Configuration Parse Errors

Verify your configuration format:

```bash
# Test YAML syntax
deno task compile -- -c config.yaml -d

# Force specific format
deno task compile -- -c config.txt -f yaml
```

## Integration with Other Tools

### Using in CI/CD

```yaml
# GitHub Actions example
- name: Setup Deno
  uses: denoland/setup-deno@v1
  with:
    deno-version: v2.x

- name: Compile Rules
  run: |
    cd src/adblock-compiler-core
    deno task compile -- -c config.yaml -r
```

### Docker

```dockerfile
FROM denoland/deno:2.0.0

WORKDIR /app
COPY src/adblock-compiler-core .

RUN deno cache src/mod.ts

CMD ["deno", "task", "compile"]
```

### npm scripts

If you're in a Node.js project, you can add to `package.json`:

```json
{
  "scripts": {
    "compile-rules": "cd src/adblock-compiler-core && deno task compile",
    "compile-interactive": "cd src/adblock-compiler-core && deno task interactive"
  }
}
```

## Performance Tips

1. **Use local copies** of frequently-used filter lists to avoid network delays
2. **Enable caching** for remote sources in your configuration
3. **Minimize transformations** - only use what you need
4. **Use TOML** for the fastest config parsing
5. **Run in release mode** in production environments

## Comparison with Other Implementations

| Feature | TypeScript | .NET | Python | Rust |
|---------|:----------:|:----:|:------:|:----:|
| Startup Time | Fast | Medium | Medium | Fastest |
| Memory Usage | Medium | Medium | Low | Low |
| Configuration | JSON, YAML, TOML | JSON, YAML, TOML | JSON, YAML, TOML | JSON, YAML, TOML |
| Interactive Mode | Yes | Yes | No | No |
| Library API | No | Yes | Yes | Yes |
| Testing | Deno test | xUnit | pytest | cargo test |

## Related Documentation

- [Configuration Reference](../configuration-reference.md)
- [Compiler Comparison](../compiler-comparison.md)
- [.NET Compiler](../../src/rules-compiler-dotnet/README.md)
- [Python Compiler](../../src/rules-compiler-python/README.md)
- [Rust Compiler](../../src/rules-compiler-rust/README.md)

## License

GPLv3 - See [LICENSE](../../LICENSE) for details.
