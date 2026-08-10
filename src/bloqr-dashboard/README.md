# Bloqr Dashboard

A .NET 10 console application that acts as the single pane of glass for generating and
consuming Bloqr filter-rules compiler configs, running compilations, and managing Dashboard
profiles, backups, and logs. Menu- and wizard-driven, and — per design — never terminates on
its own, even on unexpected exceptions: every error is logged and rendered, and control always
returns to the main menu.

See [`ARCHITECTURE.md`](./ARCHITECTURE.md) for the design patterns and project layout, and
[`docs/guides/consoleui-architecture.md`](../../docs/guides/consoleui-architecture.md) for the
sibling console app this one's architecture is modeled after.

This is the scaffold plus first slice of functionality (issues #266, #267, #275 of
[epic #256](https://github.com/BloqrAI/bloqr-core/issues/256)): the app shell, structured JSON
logging with rollover, and the Dashboard's own `.jsonc` configuration with profiles and
corruption recovery. The compiler-config generation wizard, round-trip config editing, rich
compilation progress UI, full CLI automation surface, and AdGuard API stubs are separate,
later issues (#268–#272) that build on this foundation.

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

This slice ships a minimal, script-safe CLI surface; full CLI-switch parity with interactive
operations is issue #271's scope.

```
--help, -h            Show help
--version, -v          Show version information
--config <path>        Use a specific Dashboard configuration file
--profile <name>       Activate a specific profile for this run
--log-level <level>    Override the configured log level (trace|debug|info|warn|error|silent)
--non-interactive       Load config and print status instead of prompting
```

The Dashboard also auto-detects redirected/piped stdin and switches to non-interactive
behavior, so it's safe to invoke from scripts or CI without hanging on a prompt.

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
