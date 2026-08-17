# Environment Variables Reference

Comprehensive guide to environment variables actually read by Bloqr Core's shell/PowerShell wrappers and .NET apps today. Each section below is verified against the code that reads it — nothing here is aspirational.

## Overview

Environment variables give the shell/PowerShell wrappers and .NET apps configuration without editing files, useful for:
- CI/CD integration
- Containerized deployments
- User-specific defaults
- Cross-platform consistency

**There is no single unified naming scheme across languages.** Each surface uses its own prefix, for historical reasons:
- The bash/zsh/PowerShell rules-compiler wrappers use `ADGUARD_COMPILER_*` — a naming holdover from before the Bloqr rebrand. It's accurate to what's in code today, but is a real, known inconsistency with the rest of the `Bloqr.*`/`BLOQR_*` naming this repo otherwise uses (flagging for the repo owner rather than unilaterally renaming vars real scripts/CI depend on).
- The .NET compiler and Dashboard each bind a whole environment-variable prefix into `IConfiguration` (`BLOQR_COMPILER_` and `BLOQR_DASHBOARD_` respectively), following ASP.NET Core's standard double-underscore-for-nesting convention, rather than documenting one variable per setting.

## Rules Compiler (Shell/PowerShell wrappers)

Read by `src/rules-compiler-shell/bash/compile-rules.sh`, `src/rules-compiler-shell/zsh/compile-rules.zsh`, and `src/rules-compiler-powershell/RulesCompiler/Public/Invoke-RulesCompiler.ps1`.

### ADGUARD_COMPILER_CONFIG
**Description**: Configuration file path
**Type**: String (file path)
**Example**:
```bash
export ADGUARD_COMPILER_CONFIG="$HOME/.config/bloqr/compiler-config.json"
```

### ADGUARD_COMPILER_OUTPUT
**Description**: Output file/directory path
**Type**: String (path)
**Example**:
```bash
export ADGUARD_COMPILER_OUTPUT="/var/bloqr/rules"
```

### ADGUARD_COMPILER_FORMAT
**Description**: Configuration format override
**Type**: String (`json` — `yaml`/`toml` remain functionally readable for backward compatibility per `docs/configuration-reference.md`, but aren't the documented default)
**Default**: Auto-detected from the config file's extension
**Example**:
```bash
export ADGUARD_COMPILER_FORMAT="json"
```

### ADGUARD_COMPILER_VERBOSE
**Description**: Enable verbose logging
**Type**: Boolean (`true`/`1` to enable — bash/PowerShell check for either; zsh's boolean check is case-insensitive)
**Default**: unset (disabled)
**Example**:
```bash
export ADGUARD_COMPILER_VERBOSE=true
```

### ADGUARD_COMPILER_COPY_TO_RULES
**Description**: Copy the compiled output into the default `rules/` directory after compilation (equivalent to the scripts' `-r`/`-Copy` flag)
**Type**: Boolean (`true`/`1` to enable)
**Default**: unset (disabled)
**Example**:
```bash
export ADGUARD_COMPILER_COPY_TO_RULES=true
```

## .NET Compiler

`Bloqr.Compiler.Dotnet.Console` binds the entire `BLOQR_COMPILER_` prefix into its `IConfiguration` (`.AddEnvironmentVariables("BLOQR_COMPILER_")` in `Program.cs`), so **any** setting reachable via `appsettings.json` can be overridden this way — not just a fixed list. Nested keys use the standard .NET double-underscore convention.

### BLOQR_COMPILER_config
**Description**: Default configuration file path
**Type**: String (file path)
**Example**:
```bash
export BLOQR_COMPILER_config="/etc/bloqr/compiler-config.json"
```

### BLOQR_COMPILER_Logging__LogLevel__Default
**Description**: Default log level (maps to `Logging:LogLevel:Default` in configuration)
**Type**: String (`Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`)
**Example**:
```bash
export BLOQR_COMPILER_Logging__LogLevel__Default="Warning"
```

## Bloqr Dashboard

`Bloqr.Dashboard.Console` similarly binds the `BLOQR_DASHBOARD_` prefix (`.AddEnvironmentVariables("BLOQR_DASHBOARD_")` in `Program.cs`), plus a few settings the Dashboard reads directly (see `Bloqr.Dashboard.Core/DashboardPaths.cs`):

### BLOQR_DASHBOARD_CONFIG
**Description**: Override the Dashboard's own `.jsonc` config file path (highest precedence, above the platform default)
**Type**: String (file path)

### BLOQR_DASHBOARD_CONFIG_DIR
**Description**: Override the directory the Dashboard looks in for its config/profiles (primarily used by tests)
**Type**: String (directory path)

### BLOQR_DASHBOARD_LOG_LEVEL
**Description**: Default log level, overridable per-run by the `--log-level` CLI switch
**Type**: String (`Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`)

## Common (multiple languages)

### DEBUG
**Description**: Enable debug-level output. Recognized by the shell/PowerShell wrappers and the TypeScript compiler (`@bloqr/compiler-core`)
**Type**: presence-based (any value enables it) or boolean depending on the reader — see each wrapper's own `--help`
**Example**:
```bash
export DEBUG=1
```

### LOG_LEVEL
**Description**: Structured-logging level. Read by `@bloqr/compiler-core` (`src/adblock-compiler-core/src/orchestration/logger.ts`) and `Bloqr.Dashboard.Console`
**Type**: String (`DEBUG`, `INFO`, `WARN`, `ERROR`, `SILENT`)

### LOG_FORMAT
**Description**: Set to `json` for structured JSON logging instead of human-readable console output. Read by `@bloqr/compiler-core`
**Type**: String (`json` or unset for default)

## Cross-Platform Usage

### PowerShell (Windows/Linux/macOS)
```powershell
# Set for current session
$env:ADGUARD_COMPILER_CONFIG = "compiler-config.json"

# Set permanently (Windows)
[System.Environment]::SetEnvironmentVariable('ADGUARD_COMPILER_CONFIG', 'compiler-config.json', 'User')
```

### Bash/Zsh (Linux/macOS)
```bash
# Set for current session
export ADGUARD_COMPILER_CONFIG="compiler-config.json"

# Set permanently — add to ~/.bashrc or ~/.zshrc
echo 'export ADGUARD_COMPILER_CONFIG="compiler-config.json"' >> ~/.bashrc
```

### Docker
```dockerfile
ENV ADGUARD_COMPILER_CONFIG=/app/compiler-config.json \
    ADGUARD_COMPILER_COPY_TO_RULES=true \
    DEBUG=1
```

### CI/CD (GitHub Actions)
```yaml
env:
  ADGUARD_COMPILER_CONFIG: ${{ secrets.COMPILER_CONFIG_PATH }}
  DEBUG: true

steps:
  - name: Compile rules
    run: |
      ./src/rules-compiler-shell/bash/compile-rules.sh -r
```

## Priority Order

When multiple configuration sources are available, later overrides earlier:

1. **Default values** (hardcoded in scripts, or in `appsettings.json` for the .NET apps)
2. **Environment variables**
3. **Configuration files** (if explicitly specified)
4. **Command-line parameters** (highest priority)

## Troubleshooting

### Check if a variable is set
```bash
# Bash/Zsh
echo $ADGUARD_COMPILER_CONFIG

# PowerShell
$env:ADGUARD_COMPILER_CONFIG

# Show all ADGUARD_COMPILER_* variables
env | grep ADGUARD_COMPILER  # Bash/Zsh
Get-ChildItem env: | Where-Object Name -like "ADGUARD_COMPILER*"  # PowerShell
```

### Clear a variable
```bash
# Bash/Zsh
unset ADGUARD_COMPILER_CONFIG

# PowerShell
Remove-Item env:ADGUARD_COMPILER_CONFIG
```

### Debug mode
```bash
DEBUG=1 ./compile-rules.sh
```

## API clients and Linear import tool

The AdGuard DNS API clients (.NET, TypeScript, Rust, PowerShell) and the Linear import tool moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) — their environment variables (API keys, webhook URLs, Linear credentials) are documented in that repo, not here, since this repo no longer contains their code.

## See Also

- [PowerShell Modules README](../src/rules-compiler-powershell/README.md)
- [Shell Scripts README](../src/rules-compiler-shell/README.md)
- [Configuration Reference](./configuration-reference.md)
- [Dashboard Guide](./guides/dashboard-guide.md)
