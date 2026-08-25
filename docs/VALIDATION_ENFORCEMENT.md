# Validation Library Integration Requirements

This document defines the integration requirements for all rules compilers to
ensure consistent security validation across the codebase, and describes the
actual, currently-implemented enforcement mechanism.

There is no AdGuard-owned dependency anywhere in this pipeline.
`bloqr-validator-core` (`src/validation/core/`) and its CLI,
`bloqr-validator-core-cli` (`src/validation/cli/`, binary name
`bloqr-validate`), are Bloqr-authored Rust crates published to crates.io.
Their only external crates are general-purpose ones (`serde`, `reqwest`,
`clap`, `thiserror`, etc.) — see that crate's `Cargo.toml`, which `cargo deny
check` pins to approved licenses/registries. The `syntax` validation module
deliberately *reimplements* AdGuard's open-source `HostlistCompiler`
validation semantics for output compatibility (see
`docs/adr/0003-adguard-hostlist-compatibility.md`) — that is a
behavioral-compatibility choice, not a code or package dependency.

## Distribution model

Every compiler consumes `bloqr-validator-core` one of two ways, chosen per
language for architectural fit:

- **Rust rules compiler** (`src/compilers/rust/`): a direct Cargo dependency
  on `bloqr-validator-core` (workspace path locally, crates.io version in
  published builds) — same language, no FFI or subprocess needed.
- **.NET, TypeScript, Python, PowerShell**: all four consume the validator
  through the `bloqr-validate` CLI binary, built from
  `bloqr-validator-core-cli` and published as a static, dependency-free
  binary via `cargo install bloqr-validator-core-cli` or the repo's release
  binaries. .NET additionally P/Invokes the native `bloqr_validator` cdylib
  directly (see "`.NET` P/Invoke" below) for the syntax-validation call in
  its own compilation pipeline; the native lib is packaged as a
  `runtimes/{rid}/native/` NuGet asset on `Bloqr.Compiler.Core` (see
  `src/common/dotnet/src/Bloqr.Compiler.Core/Bloqr.Compiler.Core.csproj`) so
  it ships automatically with any consumer that references that package —
  no manual copy step, no separate install.

Whichever integration path a given language uses, **the validator is always
either statically linked/vendored as a real dependency (Rust), distributed as
a packaged native asset (.NET), or invoked as a standalone binary resolved at
runtime (TypeScript/Python/PowerShell)** — never a fetched/optional/soft
dependency that can silently be absent from a production build.

## Enforcement strategy: fail-closed by default

Validation is enforced at the point where each compiler writes its final
output, via a common pattern implemented independently — but consistently —
in all five languages:

1. **Missing or failing validator invocation aborts compilation.** If the
   `bloqr-validate` binary can't be found (TS/Python/PowerShell), the native
   library can't be loaded (.NET), the validator run fails, or its output
   can't be parsed, that is treated as a validation **failure**, not a
   skip — compilation aborts by default.
2. **Any Error/Critical finding aborts compilation.** Each language's
   `ValidationEventArgs`/`ValidationArgs` type has always exposed a
   `passed`/`Passed` property (`true` iff no Error/Critical findings); it
   is now actually wired into the abort decision (`!passed` triggers
   abort), rather than being computed and left for an optional handler to
   notice.
3. **Warnings can be escalated.** Each language's existing
   `fail_on_warnings`/`FailOnWarnings`/`failOnWarnings` option (also used
   for config-validation warnings) additionally escalates rules-validator
   Warning-severity findings to an abort when set.
4. **A registered event handler can still override.** Setting
   `abort`/`Abort` on the validation event from a custom
   `CompilationEventHandler` continues to work exactly as before, and takes
   priority over the default fail-closed decision either direction.
5. **Bypass is opt-in, explicit, and logged.** Each language exposes exactly
   one escape hatch — an `allow_unvalidated_output`-style flag, set to
   `false`/off by default — that reverts to "findings are informational
   only." Every implementation logs a loud, explicit warning whenever this
   flag is used. There is no other way to produce compiler output without a
   validator run being attempted.

**This opt-out is no longer needed *for browser-mode compiles specifically*.**
Before #434, `bloqr-validator-core`'s validator only understood DNS-level
syntax and rejected every cosmetic/browser-only rule outright, so a
browser-engine compile (epic #432) had 100% of its cosmetic rules flagged
invalid and required this flag just to complete. Since #434, the validator
is engine-aware (see "Engine-aware validation" below) and validates browser
artifacts against a grammar that actually understands them — fail-closed,
with no opt-out required. The flag itself was not removed: it remains a
real escape hatch for deliberate debugging (e.g. a corrupted/experimental
output a developer wants to inspect without the validator blocking it,
or the "native library/binary unavailable" degraded-mode case in the
Integration points below) — it is simply no longer *load-bearing* for the
common case of "I compiled a browser-syntax filter list."

## Engine-aware validation (#434)

`bloqr-validator-core::syntax` validates against one of two grammars,
selected by an `engine` parameter (`dns` or `browser`, default `dns` for
full backward compatibility):

- **`dns`** — the original grammar (`docs/adr/0003-adguard-hostlist-compatibility.md`):
  server-side/DNS-blocker syntax only. Cosmetic rules, extended CSS,
  scriptlet injection, and browser-only `$` modifiers (`$script`,
  `$third-party`, …) are rejected, exactly as before #434.
- **`browser`** — client-side/browser-engine syntax: accepts everything
  `dns` does, plus cosmetic rules and browser-only modifiers. See
  `docs/adr/0005-browser-syntax-validation-engine.md` for the grammar's
  scope and the build-vs-adopt decision behind how it's implemented
  (hand-rolled, not a third-party crate dependency).

Each language surfaces this the same way it surfaces everything else in
this document — consistently, but via its own idiom:

| Language   | API                                              | CLI                    |
|------------|---------------------------------------------------|-------------------------|
| Rust       | `Validator::validate_local_file_with_engine`, `ValidationEngine` | n/a (library) |
| .NET       | `IBloqrValidatorService.ValidateLocalFileAsync(path, engine, ct)` | n/a (library) |
| TypeScript | `runRulesValidator(..., engine)`                   | `bloqr-validate file --engine <dns\|browser>` |
| Python/PowerShell | shell out to `bloqr-validate`, same CLI flag | `bloqr-validate file --engine <dns\|browser>` |

Every wrapper's dual-engine compile path (epic #432) passes `browser` when
validating the browser-syntax output artifact and `dns` (the default) for
the DNS/server-side artifact — never the reverse, and never the opt-out
flag as a substitute for picking the right engine.

| Language   | Opt-out flag (default `false`)   | CLI flag                        |
|------------|-----------------------------------|----------------------------------|
| Rust       | `allow_unvalidated_output` (`CompileOptions`) | `--allow-unvalidated-output` |
| .NET       | `AllowUnvalidatedOutput` (`CompilerOptions`)  | `--allow-unvalidated-output` |
| TypeScript | `allowUnvalidatedOutput` (`ExtendedCompileOptions`) | `--allow-unvalidated-output` |
| Python     | `allow_unvalidated_output` (`compile()`/CLI) | `--allow-unvalidated-output` |
| PowerShell | `-AllowUnvalidatedOutput` switch (`Invoke-BloqrCompiler`) | `-AllowUnvalidatedOutput` |

### Integration points

**Rust** (`src/compilers/rust/core/src/compiler.rs`): `compile_rules()` — the
function the shipped `bloqr-compiler` CLI actually calls — runs
`validate_output_with_events()` against `bloqr-validator-core` directly after
writing output, before returning success.

**.NET** (`src/compilers/dotnet/src/Bloqr.Compiler.Dotnet/Services/BloqrCompilerService.cs`):
`ValidateOutputSyntaxAsync` calls `IBloqrValidatorService.ValidateLocalFileAsync`
(P/Invoke into `bloqr_validator`) on the compiled output and raises
`ValidationEventArgs` (code `RV001`) through the same zero-trust event
pipeline documented in `docs/event-pipeline.md`.

**TypeScript** (`src/compilers/typescript/src/orchestration/compiler.ts`):
`runRulesValidator()` shells out to the `bloqr-validate` binary (resolved via
`findRulesValidateBinary()`) and dispatches a `ValidationEvent`.

**Python** (`src/compilers/python/bloqr_compiler/compiler.py`):
`_run_rules_validator()` shells out to `bloqr-validate` the same way, via
`find_rules_validate_binary()`.

**PowerShell** (`src/compilers/powershell/BloqrCompiler/Public/Invoke-BloqrCompiler.ps1`):
`Invoke-RulesValidator` shells out to `bloqr-validate` via
`Find-RulesValidateBinary`.

### Example (TypeScript)

```typescript
// runCompiler() in src/orchestration/compiler.ts
const result = await hostlistCompiler.compile(config);

// Aborts by default unless options.allowUnvalidatedOutput is set;
// escalates Warning findings to an abort when options.failOnWarnings is set.
await runRulesValidator(
  outputPath,
  callbacks,
  logger,
  options.allowUnvalidatedOutput ?? false,
  options.failOnWarnings ?? false,
);
```

The equivalent call sites in the other four languages follow the same shape:
run the validator, fail closed unless explicitly opted out, honor an
existing `fail_on_warnings`-style escalation, and let a registered handler
override the outcome either direction.

## CI enforcement

`tools/check-validation-compliance.sh`, run by the `integration-status` job
in `.github/workflows/validation-compliance.yml`, is the real, exit-code-gated
source of truth (not a document-level checklist). For each language it
checks two things:

1. **Integration is present** — the validator is actually invoked from that
   language's compilation pipeline (e.g. `grep`s for `runRulesValidator`,
   `IBloqrValidatorService`, `_run_rules_validator`, the Rust
   `bloqr-validator`/`bloqr_validator` Cargo dependency, or
   `Invoke-RulesValidator`).
2. **Enforcement is fail-closed** — a regression guard that greps for that
   language's exact opt-out symbol (`allowUnvalidatedOutput`,
   `AllowUnvalidatedOutput`, `allow_unvalidated`/`allow_unvalidated_output`)
   as proof the default path is enforced, not merely wired in. If a future
   change makes validation informational-only again without reintroducing
   the explicit opt-out, this check catches it.

The workflow also builds and tests `bloqr-validator-core`/
`bloqr-validator-core-cli` directly. A non-zero exit from the script fails
the job — there is no warnings-only mode; every language is expected to pass
both checks on `main`.

Run it locally the same way CI does:

```bash
./tools/check-validation-compliance.sh
```

## Pull request expectations

Any change that touches a compiler's output path should keep the fail-closed
default intact:

- [ ] The rules-validator is invoked on the compiled output before success is
      reported
- [ ] A missing/failing validator run, or an Error/Critical finding, aborts
      by default
- [ ] The `allow_unvalidated_output`-style opt-out (if used) is explicit,
      off by default, and logged loudly when set
- [ ] `tools/check-validation-compliance.sh` passes locally
