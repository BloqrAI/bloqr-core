# ADR 0004: `src/` reorg naming blueprint — the `validation` pilot

## Status

Accepted. First slice landed: `src/rules-validator/{rules-validator-core,rules-validator-cli}` →
`src/validation/{core,cli}`. All six `compilers/*` slices have since landed
(`dotnet`, `typescript`, `python`, `rust`, `powershell`, `shell`) — see the addendum below for the
naming-scope decision made across the last three. `common/dotnet` (landed under #360) and
`apps/dashboard` (landed under #403) have also since landed — every category in #331's mapping
table is now migrated.

## Context

#331/#372 established the target `src/` taxonomy (`compilers/`, `validation/`, `common/`, `apps/`,
organized by library type first and language second) and a per-language naming table for dropping
now-redundant `rules-`/`-core`/`-cli` qualifiers once the category directory carries that meaning.
Both issues asked for the actual migration to be sequenced one pilot language/component at a time,
proving the pattern end-to-end (directory move, internal identifier rename, CI paths, docs) before
the remaining five components go through the same move.

This ADR is that write-up: it records what the pilot actually did, what changed from the plan once
real code was involved, and the scope boundary this and future migrations in this repo should follow.

## Scope: this is a FOSS-repo standard, not an org-wide one

**This naming/reorg standard applies to `bloqr-core` and future FOSS libraries extracted from it —
it does not apply to internal or commercial repos** (e.g. `bloqr-compiler`). Those repos are
structured around internal org processes, teams, and product boundaries, and are expected to keep
their own conventions. Do not cite this ADR as justification for renaming anything in a
private/commercial repo; it is scoped to public, community-facing code where directory layout and
naming *are* the documentation a new contributor reads first.

Within that FOSS scope, naming must stay **intuitive**: a contributor should be able to guess where
a new library type or language wrapper belongs from the category taxonomy alone, and guess an
internal identifier's shape from the language's own conventions — never from a repo-specific rule
that only make sense if you've read this doc.

## Decision: follow each language's own idioms, not one cross-language pattern

The naming table in #331 gives a *result* per language (what the new identifier is), not a *rule*
that overrides how each language names things. Concretely, this pilot:

- **Rust**: renamed the `[lib] name` (`rules_validator` → `bloqr_validator`, snake_case) and the
  `[[bin]]` name (`rules-validate` → `bloqr-validate`, kebab-case) — both idiomatic Rust casing for
  their respective contexts. The published crate name (`bloqr-validator-core`,
  `bloqr-validator-core-cli`) was already correct before this pilot and is untouched, per the
  hard constraint both issues restate: internal identifiers and published package names are
  decoupled and must stay that way.
- **.NET**: left `RulesValidatorService`, `IRulesValidatorService`, and
  `RulesValidatorNativeMethods` (all PascalCase, .NET idiomatic) unchanged in this pilot. Only the
  P/Invoke library name and the FFI function names they call were renamed, because those are the
  actual cross-language contract with the renamed Rust library — see "FFI is a real ABI boundary"
  below.
  > **Superseded**: the repo owner later hardened #331/#372's "Rules"/"lists" ban into an explicit
  > "substitute Bloqr" rule that reaches internal class/interface names too, not just
  > directories/namespaces. The `src/compilers/dotnet` migration PR renamed these three to
  > `BloqrValidatorService`/`IBloqrValidatorService`/`BloqrValidatorNativeMethods` accordingly. The
  > "rule of thumb" below (leave a language's own internal-only identifiers alone) still holds as
  > the general default — this is a narrower, explicit exception the owner called out for this
  > specific vague-noun case, not a reversal of the rule of thumb itself.
- **Python, TypeScript, PowerShell, bash/zsh**: left every wrapper's own internal function, module,
  and class names unchanged (`find_rules_validate_binary`, `findRulesValidateBinary`,
  `Invoke-RulesValidator`, `RulesValidatorResult`, `find_rules_validate_binary`/
  `run_rules_validator` in shell). Only the literal string these wrappers shell out to
  (`"rules-validate"` → `"bloqr-validate"`) was updated, since that string is the actual
  cross-process contract with the renamed CLI binary — the identifiers around it are each
  language's own naming, not something this migration should touch.

**Rule of thumb for future migrations**: rename what crosses a language/process boundary (published
package names — never; ABI/CLI-invocation surface — yes, in lockstep with the producing side);
leave a language's own internal-only identifiers to that language's own future pilot.

## FFI is a real ABI boundary, not just an internal rename

`src/validation/core/src/ffi.rs` exports six `#[no_mangle] pub extern "C" fn` symbols
(`bloqr_validator_new`, `_free`, `_validate_local_file`, `_validate_remote_url`, `_version`,
`_free_string`). These are genuine linker-visible ABI symbols, not Rust-internal names — .NET's
`RulesValidatorNativeMethods` resolves them by default-name `[DllImport]` matching, with no
`EntryPoint` override. Renaming the Rust `[lib] name` alone already changes the compiled artifact's
filename (`librules_validator.so` → `libbloqr_validator.so`), which forces the .NET
`LibraryName` constant to change regardless — so this pilot renamed the FFI symbols and the .NET
P/Invoke declarations together, in one PR, rather than splitting into a Rust-only PR followed by a
separate .NET PR that touches the same file twice. Future migrations that cross an FFI or subprocess
boundary should make the same call: find every consumer of the renamed surface first, and land the
rename atomically across all of them.

## What the plan got wrong, corrected by reading actual code

#331's naming table listed the CLI's current binary name as `rules-validator-cli`. The actual
`[[bin]] name` in `src/rules-validator/rules-validator-cli/Cargo.toml` (now
`src/validation/cli/Cargo.toml`) was `rules-validate`, and `#[command(name = "rules-validate")]` in
`main.rs` confirmed it — the table was written from memory in an earlier planning pass, before this
pilot actually opened the file. The renamed binary is `bloqr-validate` (drop "rules", add the brand
prefix, consistent with how the crate name itself transforms), not `bloqr-validator-cli` as
originally tabled. #331 has been corrected to match; this is the general lesson for the remaining
migrations — verify the naming table's "current identifier" column against the real file before
planning the "new identifier" column, since a plan written from memory can be wrong in ways that
only surface once you're inside the code.

## Addendum: the .NET "harden" exception generalized to Rust and PowerShell's own `RulesCompiler`

The `.NET` bullet above records a narrow, explicit exception: the repo owner hardened the general
"leave a language's own internal-only identifiers alone" rule of thumb specifically for
`RulesValidatorService`/`IRulesValidatorService`/`RulesValidatorNativeMethods`, because "Rules" as a
bare product-branding noun (as opposed to "rules" the domain concept — filter rules, rule counts,
etc.) was judged to need to disappear everywhere, not just from directory names. That exception was
written up during the `validation` pilot, before any `compilers/*` slice had touched a language with
its own literal `RulesCompiler`-named identifier.

Three of the six `compilers/*` slices later hit that exact case for the *compiler* (not validator)
side of the naming:

- **Rust** (`src/compilers/rust`): the crate's own `RulesCompiler` struct — the direct
  compiler-side analog of `RulesValidatorService` — was renamed to `BloqrCompiler`, alongside the
  already-decided `[lib]`/`[[bin]]` name changes.
- **PowerShell** (`src/compilers/powershell`): the `RulesCompiler` module itself (directory,
  `.psd1`/`.psm1`, and its public `Invoke-RulesCompiler` cmdlet) was renamed to `BloqrCompiler`/
  `Invoke-BloqrCompiler`, plus the parallel private helper `Get-RulesCompilerCommand` →
  `Get-BloqrCompilerCommand`.
- **Shell** (`src/compilers/shell`): by contrast, hit no such case. Its own function names
  (`compile_rules`, `get_compiler_command`, `count_rules`) were already domain-accurate — "rules"
  meaning filter rules, never a `RulesCompiler`-branded noun — so nothing there needed the harden
  exception. Only the *files* (`compile-rules.sh`/`.zsh` → `compile.sh`/`.zsh`) were renamed, and for
  an unrelated reason: dropping a qualifier the `compilers/` category directory already makes
  redundant, the same principle `rules-validate` → `bloqr-validate` used in the original pilot.

Because renaming a language's own public module/cmdlet name is a real breaking change for anyone
already importing it — unlike a directory move, which is invisible to existing callers — the
PowerShell call in particular was confirmed with the repo owner via an explicit question before
being made, rather than inferred silently from the .NET precedent. The answer: yes, extend it,
matching Rust and .NET.

Each of these three per-language slices' own validator-side wrappers (`Invoke-RulesValidator`,
`RulesValidatorResult`, `Find-RulesValidateBinary` in PowerShell; `find_rules_validate_binary`/
`run_rules_validator` in shell) were left exactly as this ADR's original text already decided —
the harden exception is scoped to the *compiler*-branded `RulesCompiler` noun specifically, not a
blanket re-opening of every already-settled validator-side name.

**Updated rule of thumb**: the harden exception isn't .NET-specific — it applies to any language
where the compiler slice finds its own literal `RulesCompiler`-branded identifier (a class, module,
struct, or public command whose name *is* the old product noun, not domain language describing what
it does). A future migration that finds no such identifier — as the shell slice didn't — needs no
exception at all; the original "leave a language's own internal-only identifiers alone" default
still governs everything else.

## Consequences

- Future migrations (`compilers/*`, `common/dotnet`, `apps/dashboard`) should each produce a short
  addendum here (or a new ADR) only if they hit a decision not already covered by this one — e.g. a
  new kind of cross-language boundary, or a naming-table entry that turns out to be wrong. Routine
  moves that just apply this ADR's already-decided rules don't need their own ADR.
- `tools/check-validation-compliance.sh` and every CI workflow's `paths:`/`working-directory` values
  now reference `src/validation/**` — any future category move must update the equivalent
  path-filtered workflows and compliance scripts in the same PR, not as a follow-up.
- `docs/architecture/versioning-strategy.md`'s independent-per-package versioning stayed unaffected:
  this migration never touches `[package] name`/`version`, only `[lib]`/`[[bin]]` names and
  directory layout, which is exactly the decoupling that versioning strategy already relies on.

## References

- #331 — the taxonomy and naming-table epic.
- #372 — the migration-mechanics sub-issue this pilot fulfills the first slice of.
- `docs/architecture/versioning-strategy.md` — the independent-per-package versioning this
  migration's `[package] name` stability depends on.
- `src/validation/README.md`, `src/validation/core/README.md`, `src/validation/cli/README.md` — the
  post-migration state this ADR describes the reasoning behind.
- `src/compilers/rust/README.md`, `src/compilers/powershell/README.md`,
  `src/compilers/shell/README.md` — the post-migration state the addendum above describes the
  reasoning behind.
