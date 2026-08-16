# Bloqr Dashboard Guide

The Bloqr Dashboard is a unified .NET console application for managing ad-blocking filter compilation, configuration, and operations.

## What is the Dashboard?

The Dashboard provides a menu-driven interface for:

- **Compiling filter rules** from multiple sources with transformations and validation
- **Managing configurations** with built-in wizard for generating compiler configs
- **Profile management** for switching between different compilation configurations
- **Live progress monitoring** with structured JSON logging and rollover
- **Config backups and corruption recovery** with automatic versioning
- **Rules validation** integrated throughout
- **Diagnostics** for troubleshooting compilation issues

It's built on .NET 10 with a design that prioritizes resilience: errors never terminate the app, every exception is logged and displayed, and control always returns to the main menu.

## Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| .NET SDK | 10.0+ | Cross-platform runtime |
| Deno | 2.0+ | Required by the underlying TypeScript rules compiler |

## Installation

### Build from Source

From the repository root:

```bash
# Option 1: Using build script
./build.sh --dotnet       # Linux/macOS
./build.ps1 -DotNet       # Windows

# Option 2: Direct .NET build
cd src/bloqr-dashboard
dotnet restore BloqrDashboard.slnx
dotnet build BloqrDashboard.slnx
```

### Run

```bash
cd src/bloqr-dashboard
dotnet run --project src/Bloqr.Dashboard.Console
```

## Interactive Mode

The default mode shows the main menu with these options:

- **Compile Rules** - Run a compilation with the active profile or specified config
- **Configuration** - View and edit the active compiler configuration
- **Profiles** - Create, switch, and manage named profiles
- **Logs** - View structured compilation logs with filtering by app and time range
- **Diagnostics** - Validate configs, check component status, and troubleshoot issues

Navigate the menu using arrow keys and Enter. The app never terminates on error — every exception is caught, logged, and the menu returns for the next action.

## CLI Mode

Use the Dashboard from scripts or CI/CD pipelines:

```bash
# Show help
dotnet run --project src/Bloqr.Dashboard.Console -- --help

# Show version
dotnet run --project src/Bloqr.Dashboard.Console -- --version

# Compile using default profile
dotnet run --project src/Bloqr.Dashboard.Console -- --compile

# Compile using specific profile
dotnet run --project src/Bloqr.Dashboard.Console -- --profile production --compile

# Compile specific config file
dotnet run --project src/Bloqr.Dashboard.Console -- --compile /path/to/config.json

# Validate a config without compiling
dotnet run --project src/Bloqr.Dashboard.Console -- --validate-config /path/to/config.json

# List all profiles
dotnet run --project src/Bloqr.Dashboard.Console -- --list-profiles

# Activate a profile
dotnet run --project src/Bloqr.Dashboard.Console -- --activate-profile production

# Use a specific Dashboard config file
dotnet run --project src/Bloqr.Dashboard.Console -- --config ~/.config/bloqr-dashboard/custom-dashboard-config.jsonc --compile

# Set log level
dotnet run --project src/Bloqr.Dashboard.Console -- --log-level debug --compile

# Non-interactive mode (check status, no prompts)
dotnet run --project src/Bloqr.Dashboard.Console -- --non-interactive
```

### CLI Options Reference

| Option | Short | Description |
|--------|-------|-------------|
| `--help` | `-h` | Show help message |
| `--version` | `-v` | Show version information |
| `--config PATH` | | Use a specific Dashboard configuration file |
| `--profile NAME` | | Activate a specific profile for this run |
| `--log-level LEVEL` | | Override log level (trace, debug, info, warn, error, silent) |
| `--non-interactive` | | Load config and exit without interactive prompts |
| `--compile [PATH]` | | Compile with optional config path |
| `--validate-config PATH` | | Validate a config without compiling |
| `--list-profiles` | | List all profiles |
| `--activate-profile NAME` | | Activate and persist a profile |

The Dashboard auto-detects redirected/piped stdin and switches to non-interactive mode automatically, so it's safe to invoke from scripts or CI without hanging.

## Configuration

### Dashboard Configuration File

The Dashboard stores its own settings in a `.jsonc` file, created automatically on first run:

- **Windows**: `%APPDATA%\bloqr-dashboard\dashboard-config.jsonc`
- **Linux/macOS**: `$XDG_CONFIG_HOME/bloqr-dashboard/dashboard-config.jsonc` (defaults to `~/.config/bloqr-dashboard/`)

The file includes:
- Log level and output settings
- Profile management
- Backup and recovery settings
- Active profile tracking
- AdGuard API configuration (if configured)

### Environment Variables

| Variable | Description |
|----------|-------------|
| `BLOQR_DASHBOARD_CONFIG_DIR` | Override the entire Dashboard configuration directory |
| `BLOQR_DASHBOARD_CONFIG` | Override just the config file path |
| `BLOQR_DASHBOARD_LOG_LEVEL` | Override log level (trace, debug, info, warn, error, silent) |

### Compiler Configuration

The Dashboard compiles using compiler configuration files in JSON or JSONC format. See [Configuration Reference](../configuration-reference.md) for the complete schema.

Example compiler config:

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
      "transformations": ["Validate", "RemoveModifiers"]
    }
  ],
  "transformations": [
    "Deduplicate",
    "RemoveEmptyLines",
    "TrimLines",
    "InsertFinalNewLine"
  ]
}
```

## Key Features

### Configuration Wizard

Generate compiler configs interactively without hand-editing JSON:

1. From the Dashboard menu, select **Configuration**
2. Choose **Generate New Configuration**
3. Answer prompts for sources, transformations, and output settings
4. Review the generated JSON/JSONC before saving

### Profile Management

Create named profiles to switch between different compilation setups:

```bash
# List profiles
dotnet run --project src/Bloqr.Dashboard.Console -- --list-profiles

# Activate a profile
dotnet run --project src/Bloqr.Dashboard.Console -- --activate-profile production

# Compile with a specific profile
dotnet run --project src/Bloqr.Dashboard.Console -- --profile production --compile
```

Each profile can have:
- Its own compiler configuration
- Custom log settings
- Independent backup/recovery state

### Backups and Recovery

The Dashboard automatically:
- Creates backups when configurations change
- Quarantines corrupt or invalid configs (`dashboard-config.corrupt-<timestamp>.jsonc`)
- Recovers from the newest valid backup in interactive mode
- Regenerates defaults if no backups exist

### Structured Logging

Compilations produce JSON-formatted logs with:
- Timestamp and severity (INFO, WARN, ERROR)
- Event details (compilation start/end, transformations, errors)
- Searchable via the Logs menu with filters by app and time range

### Validation

Validate compiler configurations before compilation:

```bash
# Check a config without compiling
dotnet run --project src/Bloqr.Dashboard.Console -- --validate-config config.json

# Validation checks against the schema
# - Required fields present
# - Proper types and formats
# - Valid transformation names
# - Source accessibility
```

### Diagnostics

The Diagnostics menu provides:
- Configuration status and schema validation
- Component health checks
- Deno and compiler availability
- Log file location and size
- Backup status and recovery options

## Embedding as a Library

The Dashboard can be embedded in other applications (e.g., a future .NET MAUI UI) by depending on:
- `Bloqr.Dashboard.Abstractions` (interfaces, no dependencies)
- `Bloqr.Dashboard.Core` (implementation)

The `IDashboardService` facade provides all compilation and profile operations without requiring the Spectre.Console terminal UI library:

```csharp
var dashboardService = serviceProvider.GetRequiredService<IDashboardService>();
var result = await dashboardService.CompileAsync(configPath, profileName);
```

## Troubleshooting

### Dashboard won't start

Check prerequisites:
```bash
dotnet --version    # Should be 10.0+
deno --version      # Should be 2.0+
```

### Configuration file corrupted

The Dashboard detects and automatically recovers corrupt configurations:
1. The corrupt file is renamed to `dashboard-config.corrupt-<timestamp>.jsonc`
2. Recovery uses the newest valid backup
3. If no backups exist, defaults are regenerated

### Compilation fails

1. Use Diagnostics menu to check component status
2. View Logs menu for detailed error messages
3. Use `--validate-config` to check the compiler configuration
4. Ensure sources are accessible and network is available

### Logs not appearing

Check log level:
```bash
# Increase verbosity
dotnet run --project src/Bloqr.Dashboard.Console -- --log-level debug
```

View logs location in Diagnostics menu (logs are stored in:
- **Windows**: `%APPDATA%\bloqr-dashboard\logs\`
- **Linux/macOS**: `~/.config/bloqr-dashboard/logs/`

## Architecture & Design

For details on the Dashboard's architecture, project structure, and design patterns, see:

- [`ARCHITECTURE.md`](../../src/bloqr-dashboard/ARCHITECTURE.md) - Technical architecture and patterns
- [`docs/guides/consoleui-architecture.md`](consoleui-architecture.md) - Console UI design template
- [Epic #256](https://github.com/BloqrAI/bloqr-core/issues/256) - Feature tracking and implementation status

## Next Steps

- [Configuration Reference](../configuration-reference.md) - Learn all configuration options
- [Compiler Comparison](../compiler-comparison.md) - Compare with CLI compilers
- [Deployment Guide](deployment-guide.md) - Deploy the Dashboard in production
- [Troubleshooting Guide](troubleshooting-guide.md) - Resolve common issues

---

**Note**: The AdGuard DNS API client integration remains a separate, later issue. The Dashboard's API configuration extension points are wired but not connected to a real client yet.
