# Epic #415 Retrospective (2026-08)

A record of the epic that replaced this repo's scattered, mostly-fake benchmark tooling
with a real, comparable "how fast does each wrapper actually compile" number across all
five compiler wrappers — and of the four real bugs that dogfooding that work turned up
along the way (#424, #426, #427, #428). Companion to `docs/RESTRUCTURING_RETROSPECTIVE.md`
and `docs/EPIC_256_RETROSPECTIVE.md`, which cover the two epics before this one.

## Why this epic existed

`#256` (closed 2026-08-14) called for Launcher/Dashboard to "run diagnostics and
benchmarks," but an audit found benchmark code scattered across five inconsistent,
mostly-disconnected places — a real Python orchestrator not wired into CI/Launcher/
Dashboard, a *simulated* `time.sleep`-based benchmark that Dashboard's Diagnostics menu
actually called, a Rust `--benchmark` flag that was pure `thread::sleep` theater, a dead
BenchmarkDotNet project not in any solution, and a `docs/RUST_WORKSPACE.md` claim that
`cargo bench` worked when no `[[bench]]`/criterion target existed at all. No CI job, root
script, or Launcher/Dashboard path produced a real, comparable number.

## What actually happened, in order

Eight sub-issues, Rust first as the smallest/most self-contained case per the epic's own
design decision, then the rest:

1. **#416 — Rust real `benchmark` subcommand.** Compiles the canned
   `benchmarks/data/{small,medium,large,xlarge}.txt` datasets through the real
   `compile_rules()`/`compile_chunks_async()` pipeline, chunked vs unchunked, with `--json`
   output. Merged in PR #425 (which also fixed #428, found along the way — see below).
2. **#417 — .NET real `benchmark` subcommand.** Same shape via `Bloqr.Compiler.Dotnet`'s
   CLI. Found #426 in the process (see below) and shipped a scoped workaround for the
   `--benchmark-*` flag group only, flagging the general fix as follow-up.
3. **#418 — TypeScript real benchmark CLI mode.** Found and fixed #427 in the process — a
   severe, externally-facing bug in the *reference implementation* every other wrapper
   shells out to (see below).
4. **#419 — Python real benchmark CLI mode.** Same shape via `bloqr_compiler`'s CLI.
5. **#420 — PowerShell real chunking + benchmark cmdlets.** `Invoke-BloqrCompilerChunked`
   didn't exist before this; added alongside `Invoke-BloqrCompilerBenchmark`.
6. **#421 — Root `benchmark-all.sh`/`.ps1` scripts, formalized `benchmarks/` contract.**
   Documented the shared JSON contract (`benchmarks/README.md`) all five commands follow,
   retired the old synthetic `run_benchmarks.py`/`quick_benchmark.py` orchestrators.
7. **#422 — Launcher benchmark menu option** (all or selected compilers). Found #428 in the
   process (see below).
8. **#423 — Dashboard Diagnostics benchmark via shared `IBenchmarkService`.** Replaced the
   `time.sleep`-equivalent synthetic call with a real in-process benchmark, scoped to the
   .NET compiler only per the epic's design decision (cross-language comparison stays in
   `benchmark-all.sh`/`.ps1` and the Launcher menu).

Then, once all eight closed and the epic itself was marked complete, four bugs *found
while building the above* got their own tracking issues per the epic's stated convention
("filing for visibility") and were resolved in a follow-up pass: **#424**, **#426**,
**#427** (already fixed as part of #418's PR — confirmed via git log/JSR publish history
and closed without new code), **#428** (already fixed in PR #425 — confirmed the same way
and closed without new code). #424 and #426 needed real fixes, landed together in PR #430.

## The bugs this epic's dogfooding actually caught

Building a *real* benchmark — one that runs the actual compile pipeline chunked and
unchunked and compares real timings — is a forcing function no amount of code review finds
on its own: it's the first time many of these paths get exercised end-to-end with a
from-scratch, non-fixture config. All four bugs below were found exactly that way, not by
someone going looking for them.

- **#424 — Chunked and unchunked paths invoked different compilers (Rust, .NET, Python).**
  Each of these three languages had *two independently-implemented* "resolve the compiler
  command" functions — one for the unchunked path (Deno + JSR `@bloqr/compiler-core`), one
  for the chunked path (a leftover call to `hostlist-compiler`/`npx` from before this repo
  had its own JSR package). A chunked and unchunked compile of the same config could invoke
  genuinely different tools with potentially different behavior — and a benchmark's
  "speedup" number was partly measuring *which compiler ran*, not chunking overhead.
  TypeScript and PowerShell never had this bug (both already routed every path through one
  shared function). Fixed by unifying on the existing unchunked-path function in all three
  languages — Rust (`pub(crate) fn get_compiler_command`, chunking.rs's duplicate deleted),
  .NET (extracted to a new shared `CommandHelper.GetBloqrCompilerCoreCommand()` in
  `Bloqr.Compiler.Core` since `FilterCompiler`/`ChunkingService` live in two different
  projects with no direct reference to each other), Python (`chunking.py`'s
  `_get_compiler_command` deleted, call site now imports and calls `compiler.py`'s
  directly). Each language's error message and doc/comment/README text describing this as
  a live caveat needed a matching pass once the code was fixed — an easy thing to miss,
  since the code fix alone leaves the docs actively wrong instead of just outdated.
- **#426 — .NET CLI: any bare boolean flag before another `--key value` pair corrupted
  parsing.** `.AddCommandLine(args)` unconditionally treats the token after any `--key`
  (no `=`) as that key's value, even when the next token is itself another `--key`. A bare
  switch like `--verbose`, `--copy`, or `--benchmark` swallows the next flag's *name* as its
  own "value," silently dropping data with no error — the worst kind of bug. #417's PR
  shipped a narrow workaround (parse the `--benchmark-*` group directly off raw `args`,
  bypassing `IConfiguration` for just that group); this epic's follow-up generalized it:
  `CommandLineArgumentHelper.SplitBareBooleanFlags()` pre-scans all eleven known bare-boolean
  flags out of `args` before `AddCommandLine` ever sees them, feeding their presence back in
  via `.AddInMemoryCollection()` — fixing every flag combination, not just the one group
  that happened to get exercised first, and finally testable in isolation (six new
  `[Fact]`s) instead of only reachable through a full CLI invocation.
- **#427 — TypeScript: standard `--compile` was broken system-wide.** `readConfiguration()`
  tags the parsed config object with orchestration-only metadata (`_sourceFormat`,
  `_sourcePath`) *in place*, and that same object reference flowed straight into the core
  engine's `compile()`, which validates against a strict `additionalProperties: false`
  schema — rejecting fields it never expected. Every real compile via `runCompiler()` was
  failing on the very first schema-validation step. This is the reference implementation
  every other wrapper shells out to via `deno run jsr:@bloqr/compiler-core/cli` — so this
  wasn't an in-repo-only issue, it was a live regression in the **published `@bloqr/compiler-core`
  JSR package** itself. 1016 existing TS unit tests all passed anyway, because none of them
  exercised the full `readConfiguration()` → `compile()` handoff with a config actually read
  from a file — each layer was tested in isolation. Fixed by adding
  `stripInternalMetadata()` and calling it at both handoff points (unchunked and chunked).
  **The generalizable lesson: unit-testing each layer in isolation doesn't catch a
  contract violation at the seam between them — only an end-to-end integration test with
  real I/O does, and this repo didn't have one for its own most-depended-on code path.**
- **#428 — `launcher.sh`'s `show_menu_simple` broke every menu selection.** The
  whiptail/dialog-less fallback menu renderer `echo`'d its title and options straight to
  its own stdout, and every caller invoked it via `choice=$(show_menu ...)` — command
  substitution captures *all* stdout, not just the final `echo "$choice"`. So `$choice`
  was always the entire rendered menu text with the real selection appended at the end,
  and every `case $choice in 1) ...` downstream fell through to the `*) Invalid choice`
  default on every real invocation, in every menu, not just the new benchmark one. Fixed
  by redirecting the rendering `echo`/`echo -e` calls to stderr, leaving only the final
  selection on stdout — verified under a real pty (`script`) both before and after.
  `launcher.ps1`'s equivalent uses `Write-Host`, which doesn't get captured by PowerShell's
  `$var = ...`, so it never had this bug.

## Tricky decisions worth remembering the reasoning for

- **File bugs found via dogfooding as their own tracked issues, even after the epic that
  found them is done.** #424/#426/#427/#428 didn't block their parent sub-issue from
  closing — #417 and #422 shipped scoped/local workarounds and moved on, #418's fix
  happened to be complete already. Filing them separately (rather than silently fixing
  inline or letting them get lost in a PR description) is what made them visible enough to
  actually get the general fix in a follow-up pass instead of staying as narrow,
  single-call-site workarounds forever.
- **Rust ships first, deliberately, when a pattern needs proving out five times.** Smallest
  and most self-contained means the fastest feedback loop on whether the overall shape
  (real subprocess timing, canned fixture data, JSON contract) actually works before
  committing to it in four more languages and their very different CLI/config idioms.
- **"Native benchmark app" = a subcommand on the existing CLI, not a new binary.** Scoped
  explicitly in the epic body to avoid the BenchmarkSuite1 mistake repeating — a whole
  separate project that's easy to leave out of the solution file, CI, and every real
  invocation path, and does.
- **Dashboard's benchmark stays .NET-only; cross-language comparison lives in
  `benchmark-all.sh`/`.ps1` and the Launcher menu.** Dashboard's `IBenchmarkService` runs
  in-process against the one compiler it embeds; asking it to shell out to four other
  languages' toolchains would make it a worse, slower reimplementation of what the root
  scripts already do well.

## Documentation completeness pass (this session, after the epic closed)

Requested explicitly, separate from "knock out #424–428": a retrospective, a documentation
completeness check, and a gap sweep. Two real gaps turned up:

- **Python still had #424's bug.** The original #424 issue text called out Rust and .NET
  explicitly and said "Python/PowerShell not yet checked for the same pattern" — it turned
  out Python had it too (PowerShell didn't). `benchmarks/README.md`, `benchmark.py`, and
  `cli.py` already carried comments flagging this as a known Python caveat, which is what
  surfaced it. Fixed the same way as Rust/.NET: `chunking.py`'s duplicate
  `_get_compiler_command` deleted, call site now uses `compiler.py`'s directly. Added a
  regression test asserting the two modules share one function object, not just similar
  behavior. While in there: `CompilerNotFoundError`'s message was hardcoded to
  "hostlist-compiler not found" regardless of what was actually searched for — wrong every
  time it fired from the (now sole) Deno-only lookup path. Fixed to build the message from
  `searched_commands` instead of a hardcoded string, mirroring the equivalent fix Rust's
  `error.rs` got during the original #424 work.
- **Every "fixed" language's own docs still described the bug as live.** `docs/chunking-guide.md`,
  `benchmarks/README.md`, each affected language's `README.md`, and Dashboard's
  `DiagnosticsMenuService` benchmark-output text all still said "the unchunked and chunked
  paths currently shell out to two different underlying compilers" — true when #424 was
  filed, false the moment the code fix landed. A code fix that doesn't get its accompanying
  docs updated in the same pass leaves the docs actively lying rather than just stale; this
  is the same category of drift `docs/RESTRUCTURING_RETROSPECTIVE.md`'s environment-variable
  audit and `EPIC_256_RETROSPECTIVE.md`'s ENVIRONMENT_VARIABLES.md rewrite both called out
  independently — worth treating as a standing checklist item ("did the docs get the memo")
  on every bugfix, not just a one-off lesson.

Separately requested: replace every ASCII-art architecture diagram in the repo's
documentation with Mermaid, and make sure component relationships/dependencies are
actually diagrammed, not just described in prose. Four files had real ASCII box-and-arrow
diagrams (`docs/event-pipeline.md`, `docs/HASH_VERIFICATION.md`,
`docs/guides/consoleui-architecture.md`, `src/validation/README.md`) — all converted to
Mermaid `flowchart`s. Directory-tree listings (`├──`/`└──`, used throughout READMEs to show
literal file paths) were deliberately left as plain text — they're not architecture
diagrams and Mermaid has no idiomatic equivalent for "here is a folder structure." New
Mermaid diagrams were added where relationships were previously prose-only and easy to
get wrong by reading quickly: the root `README.md` (repo-wide component/dependency graph —
which wrapper shells out to `@bloqr/compiler-core`, which links `bloqr-validator-core` via
Cargo vs. FFI vs. subprocess, how `common/dotnet` and `apps/dashboard` relate), Dashboard's
`ARCHITECTURE.md` (project-reference graph across its four projects plus
`Bloqr.Compiler.Core`), and `src/common/dotnet/README.md` (the same graph from the shared
library's own point of view, including both in-repo consumers).

## Where things stand now

- Epic #415: **8/8 sub-issues closed**, plus **4/4 dogfooding bugs** (#424, #426, #427,
  #428) resolved or confirmed-already-fixed.
- All five language wrappers have a real `benchmark` subcommand on their existing CLI,
  following one shared JSON contract (`benchmarks/README.md`), all comparable against the
  same canned fixture data, and — as of this session — all five free of the
  divergent-compiler bug that made cross-language "speedup" numbers partly meaningless for
  three of them.
- `benchmark-all.sh`/`.ps1` at the repo root run all five and produce one comparison table;
  Launcher has a benchmark menu; Dashboard's Diagnostics menu benchmarks the .NET compiler
  in-process via `IBenchmarkService`.
- `docs/RUST_WORKSPACE.md`'s false `cargo bench` claim is gone, replaced with the accurate
  description of the real `benchmark` subcommand.

## Still open, worth carrying into the next pass

- **#427's suggested follow-up wasn't picked up in this pass**: an integration test
  exercising `readConfiguration()` → `compileFilters()`/`compile()` end-to-end against a
  real file-based TypeScript config, so a future layering bug at that seam gets caught by
  CI instead of requiring a from-scratch reproduction. Still genuinely worth doing — the
  1016 existing unit tests demonstrably didn't catch this class of bug once, and nothing
  added since closes that gap.
- **The rest of the repo's directory-tree-only "Architecture" sections could still gain a
  real relationship diagram**, following the pattern this session added to the root
  README, Dashboard's ARCHITECTURE.md, and `common/dotnet`'s README. Not done exhaustively
  here — scoped to the highest-value, most-referenced architecture docs rather than every
  per-language README, which mostly describe one wrapper's own internals rather than
  cross-component relationships.
- **`@bloqr/compiler-core`'s JSR version history has a real "compile was broken" gap**
  (#427's suggested follow-up) worth a release-notes callout for downstream consumers who
  may have silently hit schema-validation failures on every compile before the fix shipped
  — not yet done.
