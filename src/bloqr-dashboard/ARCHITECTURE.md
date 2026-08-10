# Bloqr Dashboard Architecture

This app's structure mirrors
[`AdGuard.ConsoleUI`](https://github.com/BloqrAI/bloqr-apiclients/tree/main/adguard-api-dotnet/src/AdGuard.ConsoleUI)
(the console app in the private `BloqrAI/bloqr-apiclients` repo, described locally in
[`docs/guides/consoleui-architecture.md`](../../docs/guides/consoleui-architecture.md)) —
the two apps will eventually share a WPF host, so keeping their shapes consistent now avoids a
later reconciliation. One deliberate departure is called out below.

## Project layout

| Project | Depends on | Purpose |
|---|---|---|
| `Bloqr.Dashboard.Abstractions` | nothing | Rendering/prompting/menu interfaces (`IConsoleRenderer`, `IConsolePrompter`, `IMenuService`, `IDisplayStrategy<T>`, `ILiveProgressContext`/`ILiveProgressTask`), configuration/profile/log models, and `IDashboardService` - the embeddable-library API boundary (#271) a future WPF host depends on instead of Spectre.Console. |
| `Bloqr.Dashboard.Core` | Abstractions | Terminal-agnostic implementations: `DashboardConfigurationStore` (JSONC read/write, schema validation, backup, corruption recovery), `ProfileManager`, `DashboardPaths`, structured JSON logging (`AddDashboardLogging`, `DashboardJsonLogFormatter`, `LogEntryReader`), and `DashboardService : IDashboardService` (compile/validate/profile-management operations, wrapping `IRulesCompilerService` and the pieces above). No Spectre.Console reference. |
| `Bloqr.Dashboard.Console` | Abstractions, Core, `RulesCompiler` | The executable: `Program.cs`, the Spectre.Console-backed `IConsoleRenderer`/`IConsolePrompter` implementations, `DashboardApplication`'s main loop, and the menu services. |
| `Bloqr.Dashboard.Tests` | Abstractions, Core | xunit tests for the Core layer. |

## Design patterns

### Dependency Injection

Every service is registered in `Program.cs` and resolved through the DI container — same
pattern as `RulesCompiler.Console` and `AdGuard.ConsoleUI`.

### Template Method — `MenuServiceBase`

`MenuServiceBase.ShowAsync()` implements the show-menu-until-back loop once; concrete menu
services (`CompileMenuService`, `ConfigMenuService`, `ProfileMenuService`, `LogsMenuService`,
`DiagnosticsMenuService`) only implement `Title` and `GetMenuActions()`.

**Departure from the template**: `AdGuard.ConsoleUI`'s `BaseMenuService` calls `AnsiConsole`
directly even though `IConsoleRenderer`/`IConsolePrompter` exist alongside it, which defeats the
seam and makes menus hard to unit test. `MenuServiceBase` here takes both via constructor
injection and never references `AnsiConsole`; only `Rendering/SpectreConsoleRenderer.cs` and
`Rendering/SpectreConsolePrompter.cs` are allowed to.

### Strategy — `IDisplayStrategy<T>`

Reserved for the richer per-type display formatting the config-generation wizard (#268) and
config consumption (#269) will need (e.g. rendering a compiler config's sources list). Not yet
consumed by this slice's menu services, which render directly via `ConsoleTable`.

### Factory — `IMenuServiceFactory`

`MenuServiceFactory` resolves the top-level menu services from DI in a fixed, explicit order
(set once in `Program.cs`), so `DashboardApplication`'s main loop doesn't hard-code a switch
over every menu type — adding a new top-level menu is a one-line change in `Program.cs`.

### Rendering abstraction — `IConsoleRenderer` / `IConsolePrompter`

All console I/O goes through these two interfaces. `SpectreConsoleRenderer` and
`SpectreConsolePrompter` are the only classes referencing Spectre.Console directly; a future
WPF host supplies different implementations without touching any menu service.

## The application loop

`DashboardApplication.RunAsync` never terminates on its own:

- Every per-action exception thrown inside a menu (`MenuServiceBase.HandleError`) or at the
  main-loop level (`DashboardApplication.RunAsync`'s catch block) is logged and rendered, then
  control returns to the menu — it is never allowed to propagate and crash the process.
- `AppDomain.CurrentDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` are
  registered so a truly unhandled exception is logged rather than silently terminating.
- `Console.CancelKeyPress` flushes Serilog before the process exits on Ctrl+C — the one
  legitimate full-process exit path.
- Redirected stdin (`Console.IsInputRedirected`) or `--non-interactive` switches to a
  status-and-exit path instead of prompting, so the Dashboard is safe to invoke from scripts
  and CI without hanging.

## Configuration and logging

See the root [`README.md`](./README.md) for the config file locations, recovery behavior, and
log format — those are implementation details of `Bloqr.Dashboard.Core`, not architecture.

## Compilation

`Bloqr.Dashboard.Console` references `RulesCompiler` (from `src/rules-compiler-dotnet`)
directly and calls its `AddRulesCompiler()` DI extension — it does not duplicate or wrap the
compilation pipeline. `CompilationLoggingEventHandler` subscribes to the pipeline's compilation
events (from `Bloqr.Compiler.Abstractions`) purely to mirror them into the Dashboard's
structured JSON log; issue #270's live progress UI is a separate event subscriber layered on
top of the same event pipeline, not a replacement for this one.
