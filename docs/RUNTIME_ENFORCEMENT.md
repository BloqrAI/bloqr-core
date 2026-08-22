# Runtime Enforcement of Validation Library

This document previously described an aspirational design — a mandatory
`compile_with_validation()` wrapper around a fictional `@adguard/validation`
package, with cryptographic signatures proving validation occurred. That
design was never built, and no compiler ever depended on `@adguard/validation`
or any other AdGuard-owned package. It has been replaced below with what
actually ships.

For the full per-language integration points, the opt-out flags, and the CI
gate, see **`docs/VALIDATION_ENFORCEMENT.md`** — this document only covers
the "how is bypassing actually prevented at runtime" question.

## What actually enforces validation

There is no separate wrapper function, signature, or audit-log format.
Enforcement lives directly in each compiler's own compilation pipeline, at
the point where output is about to be reported as successful:

- **Rust**: `compile_rules()` in `src/compilers/rust/core/src/compiler.rs` — the
  function the shipped `bloqr-compiler` CLI calls — runs the validator
  against the just-written output before returning `Ok`.
- **.NET**: `BloqrCompilerService.RunAsyncCore` in
  `src/compilers/dotnet/src/Bloqr.Compiler.Dotnet/Services/BloqrCompilerService.cs`
  calls `ValidateOutputSyntaxAsync` as part of its own run, before returning
  a successful `CompilerResult`.
- **TypeScript**: `runCompiler()` in
  `src/compilers/typescript/src/orchestration/compiler.ts` calls
  `runRulesValidator()` after `hostlistCompiler.compile()` and before
  reporting success.
- **Python**: `BloqrCompiler.compile()`/`compile_async()` in
  `src/compilers/python/bloqr_compiler/compiler.py` call
  `_run_rules_validator()` the same way.
- **PowerShell**: `Invoke-BloqrCompiler` in
  `src/compilers/powershell/BloqrCompiler/Public/Invoke-BloqrCompiler.ps1`
  calls `Invoke-RulesValidator` before constructing a success result.

In every case, the function that runs the validator returns a
can-continue/should-abort decision (or raises/throws), and that decision is
on the direct path to the value the compiler returns — there's no
intermediate "trust me, it validated" flag a caller could set without the
validator actually having run. The default, in all five, is **fail closed**:
a validator that can't be found or fails to run is treated the same as a
validator that found errors — the compilation aborts. The one way to change
that is the explicit `allow_unvalidated_output`-style flag documented in
`docs/VALIDATION_ENFORCEMENT.md`, which is off by default and logs a warning
whenever it's used.

## Preventing bypass at the call site

There is no bespoke ESLint rule, custom lint pass, or forged-signature
detection. Bypass is prevented more simply: **the validator call is inside
the same function that produces the compiler's result**, not a wrapper a
caller could choose to skip by calling something else instead. A caller
using `BloqrCompiler`/`hostlistCompiler`/`Invoke-BloqrCompiler` — the actual,
only public entry points each language ships — gets the validation check
whether they think about it or not. Someone could still delete or comment
out the validator call itself in a PR, which is why:

- `tools/check-validation-compliance.sh` (see `docs/VALIDATION_ENFORCEMENT.md`
  for what it checks) greps for both "the validator is invoked" and "the
  fail-closed opt-out symbol exists," and gates CI on both, for every
  language, on every PR that touches `src/validation/**`, `src/compilers/**`,
  or `src/common/dotnet/**`.
- Each language's test suite includes explicit fail-closed-by-default
  regression tests (e.g. `test_compile_options_default_is_fail_closed` in
  Rust, `RunAsync_WhenRulesValidatorUnavailable_FailsClosedByDefault` in
  .NET) that assert compilation aborts when the validator is unavailable or
  finds errors, with no handler registered.

That combination — CI grepping for the enforcement wiring itself, plus tests
asserting the fail-closed behavior — is what stands in for the
signature/audit-log mechanism this document originally described.
