# Bloqr Dashboard

A .NET 10 console application that acts as the single pane of glass for generating and
consuming Bloqr filter-rules compiler configs, running compilations, and managing Dashboard
profiles, backups, and logs. Menu- and wizard-driven, and — per design — never terminates on
its own, even on unexpected exceptions: every error is logged and rendered, and control always
returns to the main menu.

See [`ARCHITECTURE.md`](./ARCHITECTURE.md) for the design patterns and project layout, and
[`docs/guides/consoleui-architecture.md`](../../docs/guides/consoleui-architecture.md) for the
sibling console app this one's architecture is modeled after.

Part of [epic #256](https://github.com/BloqrAI/bloqr-core/issues/256): the app shell (#266),
structured JSON logging with rollover (#275), the Dashboard's own `.jsonc` configuration with
profiles and corruption recovery (#267), the compiler-config generation wizard (#268),
round-trip config editing and git-based versioning (#269), rules-validator integration (#264),
a rich live compilation-progress display (#270), CLI-switch parity plus an embeddable
library API boundary (#271), and lightweight AdGuard API client / benchmarks stubs (#272)
are all implemented. Full `adguard-api-dotnet` integration remains a separate, later issue —
#272 only wires configuration extension points and diagnostics, not a real client.

## Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| .NET SDK | 10.0+ | Cross-platform runtime |
| Deno | 2.0+ | Required by the underlying rules compiler |

## Building

```bash
cd src/bloqr-dashboard
dotnet restore BloqrDashboard.slnx
dotnet build BloqrDashboard.slnx
```

Or from the repo root: `./build.sh --dotnet` / `./build.ps1 -DotNet` (builds both
`rules-compiler-dotnet` and `bloqr-dashboard`).

## Running

```bash
dotnet run --project src/Bloqr.Dashboard.Console
```

Interactive mode (the default) shows the main menu: Compile Rules, Configuration, Profile
Management, Logs, and Diagnostics.

### CLI surface

Full CLI-switch parity with the interactive menu's compile/validate/profile-management
operations (#271):

```
--help, -h              Show help
--version, -v           Show version information
--config <path>         Use a specific Dashboard configuration file
--profile <name>        Activate a specific profile for this run
--log-level <level>     Override the configured log level (trace|debug|info|warn|error|silent)
--non-interactive       Load config and print status instead of prompting
--compile [path]        Compile a specific compiler config, or the active profile's
                        config(s) if no path is given
--validate-config <path>
                        Validate a compiler config file without compiling it
--list-profiles         List Dashboard profiles (* marks the active one)
--activate-profile <name>
                        Activate a profile and persist the change
```

The Dashboard also auto-detects redirected/piped stdin and switches to non-interactive
behavior, so it's safe to invoke from scripts or CI without hanging on a prompt.

Config *generation* (the wizard) remains interactive-only — mirroring its entire prompt tree as
CLI flags is a materially larger, separate effort. Script-driven config creation means
hand-writing or generating a compiler-config JSON/JSONC file some other way and checking it with
`--validate-config`.

### Embedding as a library

Every CLI command above (and every interactive menu action) is backed by `IDashboardService`
(`Bloqr.Dashboard.Abstractions`) — a single, Spectre.Console-free facade over compile, validate,
and profile-management operations. A future .NET MAUI host embeds the Dashboard by depending on
`Bloqr.Dashboard.Abstractions`/`Bloqr.Dashboard.Core` and resolving `IDashboardService` (backed
by `DashboardService` in `Bloqr.Dashboard.Core`) from DI, without pulling in anything from
`Bloqr.Dashboard.Console` (the only project that references Spectre.Console for terminal
rendering).

## Configuration

The Dashboard's own settings live in a heavily-commented `.jsonc` file, generated on first run
and validated against [`schemas/dashboard-config.schema.json`](../../schemas/dashboard-config.schema.json):

- **Windows**: `%APPDATA%\bloqr-dashboard\dashboard-config.jsonc`
- **Linux/macOS**: `$XDG_CONFIG_HOME/bloqr-dashboard/dashboard-config.jsonc` (falls back to
  `~/.config/bloqr-dashboard/`)

Override the whole tree with `BLOQR_DASHBOARD_CONFIG_DIR`, just the config file with
`--config`/`BLOQR_DASHBOARD_CONFIG`. A corrupt or schema-invalid file is quarantined
(`dashboard-config.corrupt-<timestamp>.jsonc`) and recovered automatically in interactive mode
(restored from the newest valid backup, or regenerated as defaults if none exists);
non-interactive mode fails fast with a distinct exit code instead of guessing.

## Logging

Structured JSON logs (one JSON object per line, matching
[`schemas/log-entry.schema.json`](../../schemas/log-entry.schema.json)) are written to the
Dashboard's log directory (sibling of the config directory above), rolling over at 24 hours or
1024 KB, whichever comes first. Default level is `error`; view them from the Logs menu or read
the `.jsonl` files directly.

## Testing

```bash
dotnet test BloqrDashboard.slnx
```
