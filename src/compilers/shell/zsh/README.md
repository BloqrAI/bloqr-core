# Zsh Script - AdGuard Filter Compiler

Zsh-specific implementation of the AdGuard filter rules compiler.

## Overview

This script provides a Z Shell (zsh) interface to the filter compilation CLI tool, with zsh-specific optimizations and modern shell features.

## Features

- **Zsh-native syntax**: Uses zsh-specific features for better performance
- **Extended globbing**: Advanced pattern matching capabilities
- **Rich parameter expansion**: Zsh's powerful string manipulation
- **Environment variables**: Full support for configuration via env vars
- **Multiple formats**: JSON, YAML, and TOML configuration support
- **Color output**: Beautiful, color-coded console output
- **Debug mode**: Comprehensive debugging with `DEBUG` or `-d` flag

## Usage

```zsh
./compile.zsh [OPTIONS]
```

### Options

| Option | Long Form | Description |
|--------|-----------|-------------|
| `-c PATH` | `--config PATH` | Path to configuration file |
| `-o PATH` | `--output PATH` | Path to output file |
| `-r` | `--copy-to-rules` | Copy output to rules directory |
| `-f FORMAT` | `--format FORMAT` | Force config format (json/yaml/toml) |
| `-v` | `--version` | Show version information |
| `-h` | `--help` | Show help message |
| `-d` | `--debug` | Enable debug output |

## Environment Variables

The script supports comprehensive environment variable configuration:

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `ADGUARD_COMPILER_CONFIG` | Path | `compiler-config.json` | Configuration file path |
| `ADGUARD_COMPILER_OUTPUT` | Path | `output/compiled-{timestamp}.txt` | Output file location |
| `ADGUARD_COMPILER_RULES_DIR` | Path | `../../rules` | Rules directory |
| `ADGUARD_COMPILER_FORMAT` | String | auto-detect | Config format (json/yaml/toml) |
| `ADGUARD_COMPILER_VERBOSE` | Boolean | `false` | Enable verbose mode (`1` or `true`) |
| `ADGUARD_COMPILER_COPY_TO_RULES` | Boolean | `false` | Auto-copy to rules (`1` or `true`) |
| `DEBUG` | Boolean | `false` | Enable debug mode (`1` or `true`) |

### Priority Order
```
CLI Parameters > Config Files > Environment Variables > Defaults
```

## Examples

### Basic Usage
```zsh
# Use default configuration
./compile.zsh

# Show version
./compile.zsh -v

# Show help
./compile.zsh -h
```

### With CLI Parameters
```zsh
# Use custom config
./compile.zsh -c my-config.yaml

# Compile and copy to rules directory
./compile.zsh -c config.json -r

# Specify output location
./compile.zsh -o /tmp/custom-output.txt

# Debug mode
./compile.zsh -c config.json -d
```

### With Environment Variables
```zsh
# Set environment variables
export ADGUARD_COMPILER_CONFIG="production-config.yaml"
export ADGUARD_COMPILER_OUTPUT="/var/www/filter.txt"
export ADGUARD_COMPILER_COPY_TO_RULES=1
export ADGUARD_COMPILER_VERBOSE=true

# Run with environment configuration
./compile.zsh
```

### Mixed Usage (CLI overrides env vars)
```zsh
# Set defaults via environment
export ADGUARD_COMPILER_CONFIG="default-config.yaml"
export ADGUARD_COMPILER_COPY_TO_RULES=1

# Override config path, keep COPY_TO_RULES from env
./compile.zsh -c special-config.json
```

### Debug Mode
```zsh
# Via environment variable
DEBUG=1 ./compile.zsh

# Via CLI flag
./compile.zsh -d

# With verbose mode
ADGUARD_COMPILER_VERBOSE=1 ./compile.zsh
```

## Configuration Formats

### JSON (`.json`)
```json
{
  "name": "My Filter List",
  "version": "1.0.0",
  "description": "Custom filter rules",
  "sources": [
    {
      "name": "Source 1",
      "source": "https://example.com/rules.txt",
      "type": "adblock",
      "transformations": ["RemoveComments"]
    }
  ]
}
```

### YAML (`.yaml`, `.yml`)
```yaml
name: My Filter List
version: 1.0.0
description: Custom filter rules
sources:
  - name: Source 1
    source: https://example.com/rules.txt
    type: adblock
    transformations:
      - RemoveComments
```

### TOML (`.toml`)
```toml
name = "My Filter List"
version = "1.0.0"
description = "Custom filter rules"

[[sources]]
name = "Source 1"
source = "https://example.com/rules.txt"
type = "adblock"
transformations = ["RemoveComments"]
```

## Zsh-Specific Features

### Extended Globbing
The script uses zsh's extended globbing (`EXTENDED_GLOB`) for advanced pattern matching.

### Parameter Expansion
Zsh-specific parameter expansion features:
- `:l` - Convert to lowercase
- `:e` - Extract extension
- `:h` - Remove trailing component (head)
- `:t` - Extract trailing component (tail)
- `:a` - Absolute path

### Arrays and Associative Arrays
Better array handling compared to bash, with native support for zero-indexed arrays.

### Error Handling
Uses zsh options:
- `ERR_EXIT` - Exit on error
- `PIPE_FAIL` - Fail if any command in pipeline fails
- `NO_UNSET` - Error on undefined variables

## Prerequisites

### Required
- **Zsh** 5.0 or later (check with `zsh --version`)
- **Node.js** 18 or later

### Optional (for YAML/TOML)
- **yq** - YAML processor:
  ```zsh
  brew install yq  # macOS
  ```
- **Python 3** with PyYAML:
  ```zsh
  pip install pyyaml
  ```
- **Python 3.11+** (tomllib built-in) or toml package:
  ```zsh
  pip install toml
  ```

## Platform Compatibility

| Platform | Status | Notes |
|----------|--------|-------|
| macOS 10.15+ | ✅ Excellent | Zsh is default shell |
| Linux (with zsh) | ✅ Excellent | Install zsh if needed |
| FreeBSD | ✅ Good | Should work |
| Windows (WSL) | ✅ Good | Install zsh in WSL |

## Installation

### macOS (Homebrew)
```zsh
# Zsh is pre-installed on macOS 10.15+
# If needed:
brew install zsh

# Set as default shell (if not already)
chsh -s $(which zsh)
```

### Linux (Ubuntu/Debian)
```bash
sudo apt update
sudo apt install zsh

# Set as default shell
chsh -s $(which zsh)
```

### Linux (Fedora/RHEL)
```bash
sudo dnf install zsh
chsh -s $(which zsh)
```

## Troubleshooting

### Script won't execute
```zsh
# Make executable
chmod +x compile.zsh

# Verify zsh location
which zsh
```

### "command not found: compilation tool"
```zsh
# Ensure Node.js is installed
which node
node --version
```

### YAML parsing issues
```zsh
# Option 1: Install yq
brew install yq  # macOS
sudo apt install yq  # Linux

# Option 2: Install PyYAML
pip install pyyaml

# Test
python3 -c "import yaml; print('PyYAML OK')"
```

### TOML parsing issues
```zsh
# For Python 3.11+, tomllib is built-in
python3 --version

# For older Python versions
pip install toml
python3 -c "import toml; print('TOML OK')"
```

### Zsh not found
```zsh
# Install zsh
brew install zsh  # macOS
sudo apt install zsh  # Ubuntu/Debian
sudo dnf install zsh  # Fedora/RHEL

# Verify
zsh --version
```

## Oh My Zsh Integration

If you're using Oh My Zsh, you can add custom aliases:

```zsh
# Add to ~/.zshrc
alias compile-adguard='~/path/to/compile.zsh'
alias compile-adguard-debug='DEBUG=1 ~/path/to/compile.zsh'
alias compile-adguard-prod='ADGUARD_COMPILER_CONFIG=prod.yaml ~/path/to/compile.zsh -r'

# Reload
source ~/.zshrc
```

## Performance

Zsh typically performs comparably to bash for script execution. The main benefits are:
- Better interactive features
- Cleaner syntax for complex operations
- Native features reducing need for external tools

## Migration from Bash

If migrating from bash scripts:
- Most bash scripts work in zsh with minor changes
- Zsh arrays are zero-indexed (bash arrays too, but zsh is more strict)
- Parameter expansion may differ slightly
- Extended globbing requires `setopt EXTENDED_GLOB`

## Related Documentation

- [Shell Scripts Overview](../README.md)
- [Environment Variables Reference](../../../../docs/ENVIRONMENT_VARIABLES.md)
- [PowerShell Modules](../../powershell/README.md)

## Contributing

When contributing to this script:
1. Follow zsh idioms and best practices
2. Test on multiple zsh versions if possible
3. Keep compatibility with zsh 5.0+
4. Document any Oh My Zsh-specific features separately
5. Ensure environment variable behavior matches bash version

## License

GPLv3 - See [LICENSE](../../../../LICENSE) for details.
