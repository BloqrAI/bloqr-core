# Release Packaging Strategy: Source vs. Binary-Only, CRT/AOT, and Dependency Preflight

**Status**: Active standard.
**Scope**: Distribution of `Bloqr.Compiler.Dotnet.Console` and `Bloqr.Dashboard.Console` (the .NET apps this epic's release pipeline builds), plus the interactive launcher scripts.

## The question (#277)

The epic asks for two distinct release shapes and raises a specific technical requirement:

1. **Source release**: cloned/downloaded source, bootstrapped and compiled by the launcher and build scripts, operated by Dashboard.
2. **Binary-only release**: no launcher, no build scripts, Dashboard as the sole entrypoint, all dependencies statically included ("fat binary"). *"The CRT (C Runtime) should be statically linked by the artifact generation on GitHub ... The minimum .NET runtime is 10.0."*

This doc answers three things: whether the current publish approach satisfies the CRT requirement or whether Native AOT is needed, confirms the binary-only package layout, and documents the new dependency-preflight behavior in the launcher scripts.

## 1. Self-contained + SingleFile vs. Native AOT

### What's already in place

`release.yml`'s `build-compilers-dotnet` and `build-apps-dashboard` jobs already publish both apps as:

```
dotnet publish ... --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

This bundles the entire .NET runtime (not just the app) into a single executable per platform — no separate .NET runtime install is required on the target machine, and the `bloqr_validator` native library is copied alongside it (#276). This is **binary-only, zero-build-tooling deployment already**, for the runtime itself.

### The CRT question specifically

Self-contained deployment (what's above) is *not* the same claim as "the CRT is statically linked." Self-contained publish bundles the managed .NET runtime and its native hosting components, but those native components still dynamically link against the **OS-provided C runtime** — the Universal CRT (`ucrtbase.dll`) on Windows, glibc on Linux, libSystem on macOS. That's a real, if usually invisible, external dependency.

**Native AOT** (`-p:PublishAot=true`) is the actual mechanism that can statically link the CRT (on Windows in particular) and produce a single native executable with no managed runtime and no separate CRT dependency at all — this is the closest match to the epic's literal wording.

### Evaluated empirically: is Native AOT viable here today?

Rather than guess, this was tested directly against `Bloqr.Compiler.Dotnet.Console` (`dotnet publish -r linux-x64 -p:PublishAot=true`):

- **It builds.** `PublishAot=true` compiles successfully and produces a working native ELF/PE executable — this is not a hard blocker.
- **It runs for simple paths.** A smoke test (`--config compiler-config.json --validate`) executed without throwing and exited 0.
- **But it emits 65 trim/AOT analysis warnings**, concentrated in three places that matter:
  1. **`ConfigurationReader`'s YAML/TOML support is flatly unsupported.** `YamlDotNet.Serialization.DeserializerBuilder`/`SerializerBuilder` are reflection-based and explicitly documented by YamlDotNet itself as incompatible with AOT (`IL3050`/`IL3053` — "You need to use the code generator/analyzer to generate static code and use the `StaticDeserializerBuilder` object instead"). `Tomlyn` likewise produces trim warnings (`IL2104`). Since YAML/TOML remain functionally supported (just undocumented, per #259), an AOT build would need to either drop that support or adopt each library's static/source-generated variant — real, non-trivial work.
  2. **JSON (de)serialization throughout `Bloqr.Compiler.Core` uses reflection-based `JsonSerializer` calls with no `JsonSerializerContext`** (`ConfigurationReader`, `HashDatabaseService`, `CompilerConfigJsonSchemaValidator`, `BloqrValidatorService`, `StructuredJsonLogFormatter` — 19 `IL2026`/`IL3050` warning pairs across these files). This is `System.Text.Json`'s standard AOT guidance: switch to source-generated serialization contexts. Doable, but it's a real refactor across every model type these paths touch, not a flag flip.
  3. **`Spectre.Console`'s exception rendering is explicitly unsupported under AOT** (`AnsiConsole.WriteException` — "ExceptionFormatter is currently not supported for AOT"), hit in `ConsoleApplication`'s three top-level error-handling paths. Since "never crash, always render the error and return to the menu" is this app's core design contract (#266), this is a correctness-relevant gap, not cosmetic.

None of these are fatal to a *future* AOT migration — they're exactly the reflection/trimming constraints the issue itself anticipated. But they represent real engineering work (source-generated JSON contexts across ~7 files, a YAML/TOML decision, and an AOT-safe exception-rendering path) that shouldn't be done as a drive-by inside a packaging-strategy issue.

### Decision

**Keep self-contained + `PublishSingleFile` + `IncludeNativeLibrariesForSelfExtract` as the distribution mechanism.** It already delivers a true zero-install, xcopy-deployable binary for the .NET runtime itself, and in practice the "CRT" dependency it still carries is satisfied by the OS on every platform this repo already requires (Windows 10 1607+/Server 2016+ ship the Universal CRT in-box; every currently-supported Linux distribution and macOS version ships a compatible libc). Document this honestly as "no separate runtime *or CRT install required in practice on supported OS versions*," not as "statically linked," since that would overstate what's actually happening.

**Defer Native AOT** to a dedicated follow-up issue if/when the CRT guarantee needs to be literal rather than practical. That follow-up's scope is now concretely known from this evaluation: (1) decide whether to drop YAML/TOML from the AOT build variant or invest in each library's static-codegen path, (2) add `JsonSerializerContext` source generation across the ~7 files above, (3) replace `AnsiConsole.WriteException` with an AOT-safe exception rendering path in `ConsoleApplication`'s (and the Dashboard's equivalent) error handlers, (4) re-run this same empirical warning-count check and confirm zero `IL2026`/`IL3050`/`IL3053` warnings before shipping it as the default.

## 2. Binary-only package layout

Already correctly shaped by #276's `release.yml` changes — this section documents and confirms it rather than changing it:

```
Bloqr.Compiler.Dotnet.Console-<platform>.{zip,tar.gz}
├── Bloqr.Compiler.Dotnet.Console[.exe]      # single self-contained executable
├── libbloqr_validator.{so,dll,dylib}  # native validation library, copied in alongside
└── appsettings.json

Bloqr.Dashboard.Console-<platform>.{zip,tar.gz}
├── Bloqr.Dashboard.Console[.exe]
├── libbloqr_validator.{so,dll,dylib}
└── appsettings.json
```

Each archive is self-contained: no `build.sh`/`build.ps1`, no launcher script, no separately-installed .NET runtime, no separately-installed `bloqr-validator` binary. Extract and run — this already satisfies "binaries-only, no launcher, no build scripts, Dashboard as the sole entrypoint" for the .NET apps. The Rust `bloqr-compiler` and `bloqr-validator` CLI get the equivalent treatment via their own release-mode static binaries.

The **source release** shape (option 1 — bootstrapped by the launcher/build scripts) is simply this same repository checked out and run via `./build.sh`/`./launcher.sh` (or the `.ps1` equivalents) as documented in the root README — no new packaging work was needed for that side; it already exists and is exercised by `.github/workflows/build-scripts-tests.yml`.

## 3. Dependency preflight checks (launcher)

Before this change, `launcher.sh`/`launcher.ps1` had inconsistent (and in the .NET/Rust case, entirely absent) handling of missing tools: some paths (`deno`, `python3`) printed a red X and gave up; the `.NET` and `Rust` compile paths, and the Dashboard launch, called `dotnet`/`cargo` directly with no check at all, so a missing tool surfaced as a raw shell "command not found" instead of guidance.

Both launchers now have a shared preflight helper (`require_tool` in `launcher.sh`, `Request-Tool` in `launcher.ps1`) that, for any tool-invoking menu action:

1. Checks whether the tool is already on `PATH` — if so, proceeds with no interruption.
2. If missing, **states clearly what's missing and why it's needed**, and **shows the exact install command** before running anything.
3. **Asks for explicit confirmation** (`[y/N]`) before running the official installer (`dotnet-install.sh`/`rustup`/`deno`'s install script on bash; `winget` package installs on PowerShell) — a bare "no" or Enter aborts, matching the epic's "give the option to abort."
4. Re-checks after installing and reports success/failure; the calling menu action only proceeds if the tool ends up present.

This is wired into every menu action that shells out to `dotnet`, `cargo`, or `deno`: the Filter Rules Compilation submenu's `.NET`/`Rust`/`TypeScript` choices, and the Dashboard launch entry.

**Not changed**: `build.sh`/`build.ps1` themselves stay non-interactive (they already print a clear "not installed" message for `deno`/`python3` and fail fast) — those scripts are also invoked non-interactively in CI (`.github/workflows/build-scripts-tests.yml`), where a blocking `[y/N]` prompt would hang the job. Interactive preflight-with-install-prompt belongs at the launcher layer, where a human is present to answer it.

**Dashboard (compiled binary) side**: `DiagnosticsMenuService` already detects and reports on `dotnet`/`deno`/`cargo`/`hostlist-compiler` presence with remediation guidance (a URL to install from), and `FilterCompiler` already surfaces a clear "deno not found. Install from: https://deno.com/" message instead of a raw exception when a compile is attempted without it (both predate this issue). Adding an *interactive* install-and-run flow inside the compiled Dashboard app itself — i.e., having the running .NET process download and execute an install script on the user's behalf — is a materially different, security-sensitive feature (executing downloaded code from within an already-running trusted process) that deserves its own scoped review rather than being folded into this packaging-strategy issue. Tracked as a candidate follow-up, not implemented here; the existing detect-and-report behavior already satisfies "detect" and "inform users of what's being installed."
