# Environment Variables Reference

Comprehensive guide to environment variables supported by AdGuard scripts and modules.

## Overview

All AdGuard scripts and modules support environment variables for configuration. This allows for:
- Easy CI/CD integration
- Containerized deployments
- User-specific defaults
- Cross-platform consistency

## Naming Convention Standards

This project follows modern environment variable naming conventions based on the [12-Factor App](https://12factor.net/) methodology:

### Standard Format
**One format that works across all languages (TypeScript, Rust, C#, Python, PowerShell):**

- `ADGUARD_API_KEY` - API authentication key
- `ADGUARD_API_BASE_URL` - API base URL (optional)
- `ADGUARD_WEBHOOK_URL` - Webhook endpoint
- `ADGUARD_LINEAR_API_KEY` - Linear API key
- `ADGUARD_LINEAR_TEAM_ID` - Linear team ID
- `ADGUARD_LINEAR_PROJECT_NAME` - Linear project name
- `DEBUG` - Debug mode (common standard, no prefix needed)

### Why This Format?
- **Universal**: Works identically in TypeScript, Rust, C#, PowerShell, and Python
- **Clear**: SCREAMING_SNAKE_CASE with descriptive names
- **Namespaced**: `ADGUARD_` prefix prevents conflicts
- **Simple**: No language-specific variations

### Backward Compatibility
Legacy formats are supported but deprecated:
- `ADGUARD_AdGuard__ApiKey` → Use `ADGUARD_API_KEY` (C#/.NET hierarchical format)
- `ADGUARD_AdGuard__BaseUrl` → Use `ADGUARD_API_BASE_URL`
- `ADGUARD_API_TOKEN` → Use `ADGUARD_API_KEY`
- `ADGUARD_API_URL` → Use `ADGUARD_API_BASE_URL`
- `LINEAR_API_KEY` → Use `ADGUARD_LINEAR_API_KEY`
- `LINEAR_TEAM_ID` → Use `ADGUARD_LINEAR_TEAM_ID`
- `LINEAR_PROJECT_NAME` → Use `ADGUARD_LINEAR_PROJECT_NAME`

All code supports both new and legacy formats for smooth migration.

## Rules Compiler

### ADGUARD_COMPILER_CONFIG
**Description**: Default configuration file path  
**Type**: String (file path)  
**Default**: `compiler-config.json`  
**Example**:
```bash
export ADGUARD_COMPILER_CONFIG="$HOME/.adguard/my-config.yaml"
```

### ADGUARD_COMPILER_OUTPUT
**Description**: Default output directory  
**Type**: String (directory path)  
**Default**: `./output`  
**Example**:
```bash
export ADGUARD_COMPILER_OUTPUT="/var/adguard/rules"
```

### ADGUARD_COMPILER_RULES_DIR
**Description**: Default rules directory for copying compiled output  
**Type**: String (directory path)  
**Default**: `./rules`  
**Example**:
```bash
export ADGUARD_COMPILER_RULES_DIR="/etc/adguard/rules"
```

### ADGUARD_COMPILER_FORMAT
**Description**: Default configuration format  
**Type**: String (`json`, `yaml`, `toml`)  
**Default**: Auto-detected from file extension  
**Example**:
```bash
export ADGUARD_COMPILER_FORMAT="yaml"
```

### ADGUARD_COMPILER_VERBOSE
**Description**: Enable verbose logging  
**Type**: Boolean (`true`, `false`, `1`, `0`)  
**Default**: `false`  
**Example**:
```bash
export ADGUARD_COMPILER_VERBOSE=true
```

### DEBUG
**Description**: Enable debug mode (affects all modules)  
**Type**: Boolean (`true`, `false`, `1`, `0`)  
**Default**: `false`  
**Example**:
```bash
export DEBUG=1
```

## Webhook Module

### ADGUARD_WEBHOOK_URL
**Description**: Default webhook endpoint URL  
**Type**: String (URL)  
**Default**: None (required if not specified via parameter)  
**Example**:
```bash
export ADGUARD_WEBHOOK_URL="https://api.adguard-dns.io/webhook/xxx"
```

### ADGUARD_WEBHOOK_CONFIG
**Description**: Path to webhook configuration file  
**Type**: String (file path)  
**Default**: None  
**Example**:
```bash
export ADGUARD_WEBHOOK_CONFIG="$HOME/.adguard/webhook-config.json"
```

### ADGUARD_WEBHOOK_WAIT_TIME
**Description**: Default wait time between invocations (milliseconds)  
**Type**: Integer (200-∞)  
**Default**: `200`  
**Example**:
```bash
export ADGUARD_WEBHOOK_WAIT_TIME=500
```

### ADGUARD_WEBHOOK_RETRY_COUNT
**Description**: Default number of retry attempts  
**Type**: Integer (0-100)  
**Default**: `10`  
**Example**:
```bash
export ADGUARD_WEBHOOK_RETRY_COUNT=5
```

### ADGUARD_WEBHOOK_RETRY_INTERVAL
**Description**: Default retry interval (seconds)  
**Type**: Integer (1-60)  
**Default**: `5`  
**Example**:
```bash
export ADGUARD_WEBHOOK_RETRY_INTERVAL=10
```

### ADGUARD_WEBHOOK_FORMAT
**Description**: Default output format for statistics  
**Type**: String (`Table`, `List`, `Json`)  
**Default**: `Table`  
**Example**:
```bash
export ADGUARD_WEBHOOK_FORMAT="Json"
```

## API Client (C#/.NET)

> The AdGuard DNS API clients (.NET, TypeScript, Rust, PowerShell) moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients). These variables apply to the clients in that repo.

### ADGUARD_API_KEY
**Description**: AdGuard DNS API key (recommended cross-platform format)  
**Type**: String  
**Default**: None  
**Example**:
```bash
export ADGUARD_API_KEY="your-api-key-here"
```
**Note**: All API clients (TypeScript, Rust, .NET) support this format for consistency.

### ADGUARD_AdGuard__ApiKey
**Description**: AdGuard DNS API key (.NET hierarchical configuration format)  
**Type**: String  
**Default**: None  
**Example**:
```bash
export ADGUARD_AdGuard__ApiKey="your-api-key-here"
```
**Note**: This format uses double underscores to represent hierarchical configuration keys (e.g., `AdGuard:ApiKey` in appsettings.json).

### ADGUARD_API_BASE_URL
**Description**: API base URL (cross-platform format)  
**Type**: String (URL)  
**Default**: `https://api.adguard-dns.io`  
**Example**:
```bash
export ADGUARD_API_BASE_URL="https://api.adguard-dns.io"
```

### ADGUARD_AdGuard__BaseUrl
**Description**: API base URL (.NET hierarchical configuration format)  
**Type**: String (URL)  
**Default**: `https://api.adguard-dns.io`  
**Example**:
```bash
export ADGUARD_AdGuard__BaseUrl="https://api.adguard-dns.io"
```

## Linear Integration

> The Linear import tool moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) (formerly `src/linear/` in this repo). These variables apply to the tool in that repo.

### ADGUARD_LINEAR_API_KEY
**Description**: Linear API key for project management integration  
**Type**: String  
**Default**: None (required for Linear scripts)  
**Example**:
```bash
export ADGUARD_LINEAR_API_KEY="lin_api_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
```
**Note**: The Linear scripts support both `ADGUARD_LINEAR_API_KEY` (recommended) and `LINEAR_API_KEY` (legacy) for backward compatibility.

### ADGUARD_LINEAR_TEAM_ID
**Description**: Specific Linear team ID to use  
**Type**: String  
**Default**: First team found  
**Example**:
```bash
export ADGUARD_LINEAR_TEAM_ID="team_xxxxxxxxxxxxxxxx"
```

### ADGUARD_LINEAR_PROJECT_NAME
**Description**: Project name for imported documentation  
**Type**: String  
**Default**: None  
**Example**:
```bash
export ADGUARD_LINEAR_PROJECT_NAME="Ad-Blocking Documentation"
```

## Cross-Platform Usage

### PowerShell (Windows/Linux/macOS)
```powershell
# Set for current session
$env:ADGUARD_COMPILER_CONFIG = "config.yaml"
$env:ADGUARD_WEBHOOK_URL = "https://example.com/webhook"

# Set permanently (Windows)
[System.Environment]::SetEnvironmentVariable('ADGUARD_COMPILER_CONFIG', 'config.yaml', 'User')

# Set permanently (Linux/macOS) - add to ~/.profile or ~/.zshrc
```

### Bash/Zsh (Linux/macOS)
```bash
# Set for current session
export ADGUARD_COMPILER_CONFIG="config.yaml"
export ADGUARD_WEBHOOK_URL="https://example.com/webhook"

# Set permanently - add to ~/.bashrc or ~/.zshrc
echo 'export ADGUARD_COMPILER_CONFIG="config.yaml"' >> ~/.bashrc
```

### Docker
```dockerfile
ENV ADGUARD_COMPILER_CONFIG=/app/config.yaml \
    ADGUARD_WEBHOOK_URL=https://api.adguard-dns.io/webhook/xxx \
    DEBUG=1
```

### Docker Compose
```yaml
services:
  adguard-compiler:
    image: adguard-compiler
    environment:
      - ADGUARD_COMPILER_CONFIG=/app/config.yaml
      - ADGUARD_COMPILER_OUTPUT=/app/output
      - ADGUARD_COMPILER_VERBOSE=true
```

### CI/CD (GitHub Actions)
```yaml
env:
  ADGUARD_COMPILER_CONFIG: ${{ secrets.COMPILER_CONFIG }}
  ADGUARD_WEBHOOK_URL: ${{ secrets.WEBHOOK_URL }}
  DEBUG: true

steps:
  - name: Compile rules
    run: |
      ./src/rules-compiler-shell/bash/compile-rules.sh -r
```

## Priority Order

When multiple configuration sources are available, they are applied in this order (later overrides earlier):

1. **Default values** (hardcoded in scripts)
2. **Environment variables** (system or session)
3. **Configuration files** (if specified)
4. **Command-line parameters** (highest priority)

## Best Practices

### Development
- Use configuration files for complex setups
- Use environment variables for simple overrides
- Use command-line parameters for one-off changes

### Production
- Store sensitive values in environment variables (not in config files)
- Use a `.env` file or secrets manager
- Document required environment variables in deployment guides

### CI/CD
- Use secrets management for sensitive values
- Set non-sensitive defaults in workflow files
- Validate required environment variables before execution

## Validation

Most scripts will validate environment variables and provide helpful error messages:

```bash
# Missing required variable
$ ./compile-rules.sh
Error: ADGUARD_COMPILER_CONFIG not set and no config file found

# Invalid value
$ ADGUARD_WEBHOOK_WAIT_TIME=50 ./webhook.ps1
Error: ADGUARD_WEBHOOK_WAIT_TIME must be >= 200 milliseconds

# Type mismatch
$ ADGUARD_COMPILER_VERBOSE=maybe ./compile-rules.sh
Warning: Invalid boolean value 'maybe', using default 'false'
```

## Troubleshooting

### Check if variable is set
```bash
# Bash/Zsh
echo $ADGUARD_COMPILER_CONFIG

# PowerShell
$env:ADGUARD_COMPILER_CONFIG

# Show all AdGuard variables
env | grep ADGUARD  # Bash/Zsh
Get-ChildItem env: | Where-Object Name -like "ADGUARD*"  # PowerShell
```

### Clear variable
```bash
# Bash/Zsh
unset ADGUARD_COMPILER_CONFIG

# PowerShell
Remove-Item env:ADGUARD_COMPILER_CONFIG
```

### Debug mode
Enable debug mode to see which environment variables are being used:

```bash
DEBUG=1 ./compile-rules.sh

# Output will show:
# [DEBUG] Loading environment variables...
# [DEBUG] ADGUARD_COMPILER_CONFIG=/home/user/config.yaml
# [DEBUG] ADGUARD_COMPILER_VERBOSE=true
```

## See Also

- [PowerShell Modules README](../src/rules-compiler-powershell/README.md)
- [Shell Scripts README](../src/rules-compiler-shell/README.md)
- [Configuration Reference](./configuration-reference.md)
