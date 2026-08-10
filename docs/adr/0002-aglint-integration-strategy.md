# ADR 0002: AGLint/tsurlfilter/ExtendedCss/ecsstree/DiffBuilder Integration Strategy

**Status**: Accepted
**Date**: 2026-08-10
**Related**: #256 (epic), #265 (this ADR's tracking issue and spike)

## Context

The epic asks to integrate AdGuard's linting/tokenization/AST/diff tooling — AGLint, tsurlfilter, ExtendedCss, ecsstree, DiffBuilder (SafariConverterLib explicitly called out as possibly unnecessary) — "so rules/list errors can be caught early." All five are **npm/TypeScript packages**. There is no NuGet-package-reference path to consuming them from .NET without rewriting them, which the epic explicitly rules out.

For the **TypeScript compiler** (`@bloqr/compiler-core`) this isn't an integration question — it can `import` these packages directly, in-process, like any other npm dependency. This ADR is specifically about the **.NET/Dashboard side**, where two realistic paths exist:

1. **Subprocess wrapper** — consistent with the existing pattern already used to invoke `@bloqr/compiler-core` itself (`deno run jsr:@bloqr/compiler-core/cli`, see ADR 0001): spawn a process per lint pass, read structured output back.
2. **Embedded JS engine** (ClearScript/V8, or Jint) — run the linter in-process from .NET. No subprocess overhead, but a real embedded-runtime dependency and packaging cost, which matters given #277's "fat binary, no dependencies to install" distribution goal for the same apps.

## Spike: AGLint, empirically

AGLint is the epic's highest-value target ("AGLint can fix some rules on its own, so allow it to do so"), so the spike targeted it specifically rather than staying abstract. Everything below was run for real, not inferred from documentation.

### The CLI works, but needs a config file and has no JSON output

```
deno run --allow-read --allow-env --allow-run --allow-sys npm:@adguard/aglint@3.0.2 --no-colors bad-rules.txt
```

fails immediately with **"AGLint couldn't find the config file"** — AGLint refuses to run at all without a `.aglintrc.{json,yaml,yml}` present, even for a single-file lint pass. A minimal one (`root: true`, `extends: [aglint:recommended]`, `syntax: [Common]`) fixes that. With it, run against a fixture containing two intentionally broken rules:

```
$ deno run ... npm:@adguard/aglint@3.0.2 --no-colors bad-rules.txt
bad-rules.txt
  2:14  error  Non-existent modifier: 'badmodifier'                                  invalid-modifiers
  3:17  error  Cannot parse CSS due to the following error: Unexpected end of input  no-invalid-css-syntax

Found 2 problems (0 warnings, 2 errors and 0 fatal errors).
exit code: 1
```

It works, and the exit code is scriptable (0 = clean, non-zero = findings). But `aglint --help` confirms there is **no `--json`/`--format` flag** — output is a human-readable, column-aligned text table, not a stable machine-parseable contract. A subprocess wrapper has to either regex-parse that table (fragile — no format guarantee across AGLint versions) or find another way to get structured data. Also worth knowing before wiring this up for real: **the report is written to `stderr`, not `stdout`** — even for a normal "problems found" run, not just fatal errors — so a naive wrapper that only captures stdout gets nothing.

### The programmatic Node/library API is currently broken in the published package

AGLint's README documents a `Linter` class (`new Linter(true).lint(content)`) returning a structured report object — exactly what would be needed for reliable JSON output, and the obvious thing to try for an embedded-engine approach too. Tested directly, twice, independently:

```
// under Deno
import { Linter } from "npm:@adguard/aglint@3.0.2";
```
```
error: Uncaught SyntaxError: The requested module './linter/cli/config-finder.js' does not provide an
export named 'findNextConfig'
```

```
// under plain Node.js 22, package installed via npm
import { Linter } from "@adguard/aglint";
```
```
SyntaxError: The requested module './linter/cli/config-finder.js' does not provide an export named
'findNextConfig'
```

**Identical failure under both runtimes.** This rules out "it's a Deno/npm-compat quirk" — it's a bug in `@adguard/aglint@3.0.2`'s own published `dist/index.node.js` (a broken re-export chain), independent of which JavaScript engine consumes it. This is decisive for the embedded-JS-engine option specifically: **ClearScript/V8 or Jint would hit this exact same broken import** trying to load the library module, since the bug is in the package's bundled output, not in runtime compatibility. Choosing an embedded engine would not sidestep this — it would just delay hitting the same wall until integration time.

## Decision

**Subprocess wrapper, invoking AGLint's CLI (not its library API) via `deno run npm:@adguard/aglint`, matching the exact pattern `FilterCompiler` already uses for `@bloqr/compiler-core`.**

Reasoning:

- The embedded-JS-engine option is not just "more packaging cost" — it's currently **non-functional** for AGLint specifically, since the library API it would need to call is broken in the published package. That's a hard blocker today, not a tradeoff to weigh against subprocess overhead.
- The subprocess/CLI path works right now, empirically confirmed above, using the exact same toolchain (Deno, npm-compat) every other cross-language integration in this repo already depends on. No new runtime dependency, no new packaging burden for #277's fat-binary goal.
- Consistency: `RulesCompilerService`, `ChunkingService`, and `FilterCompiler` all already shell out to Deno-hosted npm/JSR tools via `CommandHelper`. An AGLint wrapper following the identical shape (locate `deno`, build args, run, parse stdout, map to a result type) is a small, well-understood addition rather than a new architectural pattern.
- Once AGLint's published bug is fixed upstream (or a `--format json` CLI flag ships, which the "Ideas & Questions" section of AGLint's own docs invites requesting), the wrapper's output-parsing layer is the *only* thing that needs to change — CLI invocation, config-file handling, and the .NET-side interface all stay the same.

### Extending this decision to tsurlfilter/ExtendedCss/ecsstree/DiffBuilder

Not independently spiked (out of this issue's scope — AGLint was the named highest-value target), but the same reasoning applies by default: all four are npm/TypeScript packages in the same ecosystem, none currently have a stated reason to prefer an embedded engine over CLI/subprocess invocation, and none should be assumed compatible with an in-process JS engine without their own empirical check first — this ADR's finding that "documented as a library API" does not mean "actually works when imported" should generalize as a standing caution, not just apply to AGLint. Whoever picks up the follow-up implementation issue for each of these should re-run an equivalent quick empirical check (does the CLI/library actually load and run under the intended invocation path) before assuming subprocess-wrapper is automatically fine for that specific package.

## The prototype

`spikes/aglint-integration/` (not wired into `RulesCompiler.slnx`/`BloqrDashboard.slnx` or any DI container — a standalone, throwaway console app, per this issue's "prototype only" scope) demonstrates the recommended path end-to-end from .NET:

1. Locates `deno` on `PATH`.
2. Writes a minimal `.aglintrc.yaml` alongside the target file if one isn't already present (AGLint requires it, as found above).
3. Runs `deno run --allow-read --allow-env --allow-run --allow-sys --allow-write npm:@adguard/aglint@3.0.2 --no-colors <file>`.
4. Parses the CLI's tabular text output (`line:col  severity  message  rule-id`) into a structured `AglintFinding` record.
5. Prints the findings as JSON, and the process exit code reflects AGLint's own (0 = clean, non-zero = findings/error).

Run against the same `bad-rules.txt` fixture used in the spike above, it reproduces both findings (`invalid-modifiers`, `no-invalid-css-syntax`) as structured data. See `spikes/aglint-integration/README.md` for exact run instructions.

## Consequences

- Confirms .NET/Dashboard's existing "shell out to Deno-hosted npm/JSR tooling" pattern extends cleanly to AGLint, without introducing a second cross-language integration mechanism.
- The output-parsing layer is genuinely fragile (regex over an undocumented text format) until either AGLint ships `--format json` or its library API bug is fixed upstream — this should be filed as an upstream issue/PR against `@adguard/aglint`, and the parsing layer should be isolated behind its own small, well-tested class so a future switch to JSON output (or a fixed library API) is a contained change.
- Full production integration — wiring this into `RulesCompilerService`'s pipeline, exporting broken rules to their own list, surfacing findings in the Dashboard's live-progress UI (#270's `CompilationProgressEventHandler`) — is explicitly **not** this issue's scope, per the epic's own split (implementation belongs with the Dashboard progress-UI work). This ADR unblocks that follow-up by settling the integration shape; the follow-up issue should reference this ADR rather than re-litigating subprocess vs. embedded-engine.
