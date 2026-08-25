# ADR 0003: AdGuard `HostlistCompiler` Compatibility for `bloqr-validator-core`

**Status**: Accepted (Phase 1 and Phase 2 both implemented)
**Date**: 2026-08-16
**Related**: #380 (Phase 1, closed via PR #384), #385 (Phase 2, this update), #331/#372 (repo reorg epic — `validation/` category)

## Context

#380 asked, with no more detail than two links, to "make the Rust validator library compatible with AdGuard's compilers" — [`HostlistCompiler`](https://github.com/AdguardTeam/HostlistCompiler) and [`FiltersCompiler`](https://github.com/AdguardTeam/FiltersCompiler). Neither tool nor "compatible" was defined further, so the first job here was research, not code.

### What each tool actually is

- **`FiltersCompiler`** transforms AdGuard-syntax filter lists into 16+ platform-specific outputs (browser extensions, mobile apps, CLI) using the `@adguard/tsurlfilter` engine, and validates against browser/extension-oriented rule syntax (cosmetic rules, scriptlets, HTML filtering, uBlock Origin conversion). This is squarely the **browser-extension filter ecosystem**, which this repo does not target — `CLAUDE.md` describes this toolkit's actual output as `output/adguard_dns_filter.txt`, i.e. **AdGuard DNS / AdGuard Home filtering**, not browser extensions. Wiring compatibility with `FiltersCompiler` has no identified consumer in this repo, the same reasoning #365 used to descope PyPI/PowerShell Gallery publishing for wrappers with no consumer. **Explicitly out of scope for #380.**
- **`HostlistCompiler`** compiles host blocklists into AdGuard Home/AdGuard DNS-compatible rules — exactly this repo's domain. More importantly: **this repo's own compilers already shell out to `HostlistCompiler`** for the real compile step (see `rules-compiler-rust`'s README: "Filter-list URL fetching itself is delegated to the external `hostlist-compiler` process"; `Dockerfile.warp` installs it via Deno; the compiler-config schema's transformation names — `RemoveComments`, `Compress`, `Validate`, `ValidateAllowIp`, `Deduplicate`, etc. — are `HostlistCompiler`'s own 13 transformation names verbatim).

So "compatible with AdGuard's compilers" resolves to a specific, well-scoped question: **`HostlistCompiler` compatibility**, and specifically the `Validate`/`ValidateAllowIp`/`ValidateAllowPublicSuffix`/`ValidateAllowIpAndPublicSuffix` transformations — the ones `bloqr-validator-core`'s own `syntax` module independently reimplements (naively, before this change) for pre-compile diagnostics in the Dashboard and `RulesCompilerService` pipeline (#264).

### The actual bug this surfaced

`bloqr-validator-core`'s compile step already runs the real `HostlistCompiler`. But its own **pre-validation** (`syntax::validate_syntax`, used for Dashboard diagnostics and the `--validate` CLI flag, independent of the compile step) was a hand-rolled heuristic that accepted almost anything superficially rule-shaped:

```rust
// before this change
!line.is_empty()
    && (line.starts_with("||")
        || line.starts_with("@@")
        || line.contains("##")
        || line.contains("$")
        || line.starts_with('/')
        || Regex::new(r"^[a-zA-Z0-9\-\.]+\^?$").unwrap().is_match(line))
```

This is a **validation/compile disagreement bug**: a file our validator calls "syntax OK" could still get filtered out by the real `HostlistCompiler` when actually compiled — the exact failure mode a pre-compile validator exists to prevent. Concretely, it accepted `##.ad-banner`-style cosmetic rules (element-hiding, meaningless at the DNS level — `HostlistCompiler` rejects them), `$third-party`/`$document`/`$popup`-style browser-only modifiers (DNS can't act on them), and malformed IP patterns `HostlistCompiler` specifically guards against (`1.1^`, `192.168.1` bare, etc.).

## Decision

**Port `HostlistCompiler`'s `validate.js`/`utils.js`/`rule.js` rejection logic into `bloqr-validator-core::syntax`, faithfully, read directly from source** (not inferred from documentation — every rule below was read from the actual `.js` files at `github.com/AdguardTeam/HostlistCompiler/blob/master/src/`).

### What's ported (Phase 1 — this change)

- **Modifier allowlist** (`SUPPORTED_MODIFIERS`): `important`, `~important`, `dnstype`, `dnsrewrite`, `ctag`, `denyallow`, `badfilter`, `client`. Anything else (`third-party`, `document`, `popup`, `domain`, …) is rejected — these are exactly the modifiers that only mean something at the browser/HTTP layer, not DNS resolution.
- **Pattern length floor** (`MAX_PATTERN_LENGTH = 5`), with the exact-domain-pattern exemption for short TLD-only rules (`.uk^`).
- **Character-class check** (`^[a-zA-Z0-9\-.*|^]+$`, after stripping a `://` prefix), with regex-rule (`/.../`) rules exempted entirely.
- **IP pattern classification**, ported field-for-field from `parseIpPattern`/`classifyIpPattern`: IP-suffix patterns (`1.1^`, `1.1.1.1^`) always rejected; unsafe/ambiguous 1-3-octet patterns without a clear terminator always rejected; IP-subnet patterns rejected unless `ValidateAllowIp` and shaped as a `||a.b.c.`/`||a.b.c.*` 3-octet subnet; full 4-octet IPs gated on `allow_ip`.
- **`denyallow` + IP pattern** rejection (the modifier isn't meaningful for IP-shaped patterns).
- **Wildcard-after-separator** rejection (`||example.org^test*`).
- **Exact-domain-pattern extraction and dispatch** (`||example.org^`, `*.org^`, `.org^`, `||org^`), including the wildcard-TLD special case (`||*.org^`).
- **`/etc/hosts`-format rules**: now validates *every* hostname on a line (upstream drops the leading IP token and validates the rest), not just a single-hostname regex match — a strict improvement over the old single-hostname-only check.
- **Four validation modes** as `HostlistValidationMode` (`Validate`/`ValidateAllowIp`/`ValidateAllowPublicSuffix`/`ValidateAllowIpAndPublicSuffix`), named to match the transformation names the compiler-config schema already uses, exposed via new `validate_syntax_with_mode`/`validate_syntax_content_with_mode` entry points. The existing `validate_syntax`/`validate_syntax_content` keep their exact signatures and now default to `Validate` — `HostlistCompiler`'s own default and the strictest mode — so this is a **behavioral fix, not a breaking API change**.

### Phase 2 — public-suffix-list-aware hostname rejection (#385)

`validHostname()` in the real `validate.js` rejects a rule when its hostname **exactly equals a public suffix** (`co.uk`, `github.io`, …) unless `allowPublicSuffix` is set, using `tldts` — a full Public Suffix List parser. Phase 1 deliberately deferred this (a permissive-only gap: never caused acceptance of a rule `HostlistCompiler` would itself reject at compile time), pending a real dependency decision.

**Crate decision: [`psl`](https://crates.io/crates/psl), not [`publicsuffix`](https://crates.io/crates/publicsuffix).** Both are real crates from the same crate lineage (`psl-types` underlies both); the difference is where the actual list data lives:

| | `psl` | `publicsuffix` |
|---|---|---|
| PSL data | Compiled into the binary from the official list, republished on every upstream PSL change (crates.io shows 100+ versions, essentially continuous) | Not bundled by default — either fetched at runtime from `publicsuffix.org`, or supplied by the caller as a file/string to parse |
| Runtime network | None | Required unless the caller self-maintains a list file |
| Dependencies (checked via crates.io API, 2026-08-16) | `psl-types ^2.0.11` only (+ dev-only `rspec`) | `psl-types`, plus optional `hashbrown`/`idna`/`unicase` depending on features |
| License | MIT/Apache-2.0 | MIT/Apache-2.0 |

`publicsuffix`'s runtime-fetch model directly conflicts with this crate's actual design: `bloqr-validator-core` already treats "no unexpected network access" as a security property (`url_security.rs`'s whole SSRF-guard exists to control *when and how* this crate talks to the network, for the URLs it's explicitly asked to validate — not to grow a second, implicit network dependency just to know what a public suffix is). It would also make syntax validation non-deterministic in offline/sandboxed environments — the exact kind of environment this research was done in, where `cargo deny check`'s advisory-DB fetch already hit real network restrictions. `psl`'s compiled-in-data model has none of that: deterministic, offline-safe, and its one real dependency (`psl-types`) is the same small trait crate `publicsuffix` also depends on. The tradeoff is a binary-size increase (the PSL is compiled in) and a maintenance obligation to keep bumping the `psl` version to track upstream list changes — the latter is exactly what Dependabot, already active on this repo, is for.

**Implementation**: `valid_hostname()` now calls `psl::suffix(hostname.as_bytes())`. If the returned suffix's bytes case-insensitively equal the whole hostname (i.e. the hostname *is* a suffix, not a domain registered under one) and there's no limit modifier (`denyallow`/`badfilter`/`client`), the rule is rejected unless `allow_public_suffix` is set — and even then, only if `Suffix::is_known()` is true (the suffix is a real, recognized ICANN or private-domain entry), mirroring `validHostname()`'s `!result.isIcann && !result.isPrivate` guard against single-label garbage (`"a"`, `"aa"`) trivially "being" its own unrecognized suffix. This also surfaced a real bug in Phase 1's `/etc/hosts`-rule path: `valid_etc_hosts_rule()` was hardcoding `allow_public_suffix = false` regardless of the caller's actual mode, unlike the adblock-rule path — fixed as part of wiring this in, so both rule shapes now honor the mode consistently. Threading this through also means a bare single-label hostname like `"localhost"` is now correctly rejected under the default `Validate` mode (it has no registrable-domain part, so it trivially "is" an unrecognized suffix) — a real, if initially surprising, faithful behavior match, not a bug in the port.

Also not ported (still out of scope): `HostlistCompiler`'s 9 non-`Validate` transformations (`RemoveComments`, `Compress`, `RemoveModifiers`, `InvertAllow`, `Deduplicate`, `RemoveEmptyLines`, `TrimLines`, `InsertFinalNewLine`, `ConvertToAscii`, `ip-normalize`) — those are rewriting/normalizing transformations, not validation, and `HostlistCompiler` itself already performs them at actual compile time. Porting them here would duplicate, not predict, the compiler's behavior.

## Consequences

- `bloqr-validator-core`'s pre-compile syntax validation now actually predicts what `HostlistCompiler` will do, closing the validation/compile disagreement bug above — including, as of Phase 2, the public-suffix case.
- **Behavioral change (Phase 1)**: cosmetic rules (`##...`) and browser-only-modifier rules that previously passed syntax validation now correctly fail it. One existing test (`test_syntax_validation_adblock_format`) asserted a cosmetic rule was valid; fixed as part of that change.
- **Behavioral change (Phase 2)**: whole-public-suffix rules (`||co.uk^`) and bare single-label hostnames (`localhost` in an `/etc/hosts`-format line) are now correctly rejected under the default `Validate` mode where they previously passed. Two existing tests encoded the old, overly-permissive assumption (`test_syntax_validation_hosts_format`'s `127.0.0.1 localhost` line, and `syntax.rs`'s own hosts-multi-hostname test) and were updated accordingly, with a new dedicated test documenting the single-label-hostname edge case explicitly.
- `bloqr-validator-core` gains its first non-`std`/non-workspace dependency dedicated purely to this feature: `psl = "2"`. Binary-size impact measured directly: the `bloqr-validate` CLI's release binary is 5.2MB total (includes the full validation library, PSL data, and all its other dependencies) — not a concern for this toolkit's existing distribution model.
- `FiltersCompiler` compatibility remains unscoped and unimplemented — no consumer identified in this repo. If browser-extension-format output is ever added to this toolkit's scope, this ADR's reasoning should be revisited, not silently extended.

**Update (2026-08-25, #434)**: browser-extension-format output *was* added to this toolkit's scope, via epic #432's dual-engine (DNS + browser) compilation. This ADR's `syntax` module and its DNS-only grammar are **unchanged and remain in force for `ValidationEngine::Dns`** — #434 added a separate, additive `ValidationEngine::Browser` grammar alongside it (see `docs/adr/0005-browser-syntax-validation-engine.md`), rather than revising the reasoning here. The two ADRs are complementary: this one still governs "what a DNS-level blocker can act on"; 0005 governs "what a browser engine can act on."

---
_Generated by [Claude Code](https://claude.ai/code/session_011Ur9k8ZU52SUTRQeeHa4qN)_
