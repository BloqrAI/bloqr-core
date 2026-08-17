# AGLint Integration Spike (#265)

Throwaway prototype backing [ADR 0002](../../docs/adr/0002-aglint-integration-strategy.md). **Not** part of `CompilerDotnet.slnx` or `BloqrDashboard.slnx`, not wired into any DI container, not shipped. It exists to prove the recommended "subprocess wrapper, invoke AGLint's CLI via `deno run`" integration path actually works end-to-end from .NET, with real output, before any other issue assumes a specific approach.

## Prerequisites

- .NET 10 SDK
- Deno 2.x on `PATH`
- Network access (fetches `@adguard/aglint` and its dependencies from npm on first run)

## Running it

```bash
cd spikes/aglint-integration
dotnet run -- bad-rules.txt
```

`bad-rules.txt` contains two intentionally broken rules. Expected output (findings parsed from AGLint's CLI text report into structured JSON):

```json
[
  {
    "Line": 2,
    "Column": 14,
    "Severity": "error",
    "Message": "Non-existent modifier: 'badmodifier'",
    "RuleId": "invalid-modifiers"
  },
  {
    "Line": 3,
    "Column": 17,
    "Severity": "error",
    "Message": "Cannot parse CSS due to the following error: Unexpected end of input",
    "RuleId": "no-invalid-css-syntax"
  }
]
```

The process exit code passes through AGLint's own (`0` = clean, non-zero = findings or a run failure).

## What this does and doesn't prove

**Proves**: the .NET side can locate `deno`, shell out to AGLint's CLI via `npm:@adguard/aglint`, and turn its text output into structured findings — the whole path the ADR recommends.

**Doesn't attempt**: AGLint's `--fix` flag (autofix), wiring into `BloqrCompilerService`'s pipeline, exporting broken rules to their own list, or surfacing findings in the Dashboard UI. All of that is explicitly out of scope for #265 (see the ADR) and belongs to the follow-up implementation issue once this integration-strategy decision was settled.

A `.aglintrc.yaml` gets written next to the target file automatically if one isn't already present — AGLint refuses to run without one (see the ADR's "Spike" section for how that was discovered).
