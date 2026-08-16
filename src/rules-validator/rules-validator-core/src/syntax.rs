//! Syntax validation for filter rules.
//!
//! The acceptance/rejection rules here are a deliberate port of
//! [AdGuard's `HostlistCompiler`](https://github.com/AdguardTeam/HostlistCompiler)
//! `validate`/`validateAllowIp`/`validateAllowPublicSuffix`/`validateAllowIpAndPublicSuffix`
//! transformations (`src/transformations/validate.js`, `src/utils.js`, `src/rule.js` as of
//! commit history current at authoring time) — see `docs/adr/0003-adguard-hostlist-compatibility.md`
//! for why, and for the one deliberate gap (full public-suffix-list-aware hostname rejection is
//! not yet implemented; see that ADR's Phase 2).
//!
//! `HostlistCompiler` is what this toolkit's own compilers already shell out to for the real
//! compile step (see `rules-compiler-rust`'s README) — so this module's job is to predict what
//! that real compile step will accept, not to invent independent rules.

use regex::Regex;
use serde::{Deserialize, Serialize};
use std::fs;
use std::net::IpAddr;
use std::path::Path;

use crate::error::Result;

/// Filter format type.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
#[serde(rename_all = "lowercase")]
pub enum FilterFormat {
    /// AdBlock format.
    Adblock,
    /// Hosts file format.
    Hosts,
    /// Unknown format.
    Unknown,
}

/// Which of `HostlistCompiler`'s four `Validate*` transformations to emulate.
///
/// Named to match the transformation names already used in this toolkit's own
/// compiler-config schema (`schemas/compiler-config.schema.json`'s `transformations`
/// list: `Validate`, `ValidateAllowIp`) and in `HostlistCompiler` itself, rather than
/// inventing a separate vocabulary for the same four modes.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default, Serialize, Deserialize)]
pub enum HostlistValidationMode {
    /// Strictest mode: reject bare/subnet IP patterns and whole-public-suffix rules.
    #[default]
    Validate,
    /// Like [`Self::Validate`], but allows full 4-octet IP addresses and 3-octet
    /// `||a.b.c.`/`||a.b.c.*` subnets.
    ValidateAllowIp,
    /// Like [`Self::Validate`], but allows rules that block an entire public suffix
    /// (e.g. `||co.uk^`) when the suffix is recognized.
    ValidateAllowPublicSuffix,
    /// Combines [`Self::ValidateAllowIp`] and [`Self::ValidateAllowPublicSuffix`].
    ValidateAllowIpAndPublicSuffix,
}

impl HostlistValidationMode {
    const fn allow_ip(self) -> bool {
        matches!(
            self,
            Self::ValidateAllowIp | Self::ValidateAllowIpAndPublicSuffix
        )
    }

    const fn allow_public_suffix(self) -> bool {
        matches!(
            self,
            Self::ValidateAllowPublicSuffix | Self::ValidateAllowIpAndPublicSuffix
        )
    }
}

/// Syntax validation result.
#[derive(Debug, Clone, Serialize)]
pub struct SyntaxValidationResult {
    /// Whether syntax is valid.
    pub is_valid: bool,
    /// Detected format.
    pub format: FilterFormat,
    /// Number of valid rules.
    pub valid_rules: usize,
    /// Number of invalid rules.
    pub invalid_rules: usize,
    /// Errors and warnings.
    pub messages: Vec<String>,
}

/// Validate filter list syntax.
///
/// Equivalent to [`validate_syntax_with_mode`] with [`HostlistValidationMode::Validate`]
/// — `HostlistCompiler`'s default, strictest mode.
///
/// # Errors
///
/// Returns an error if file cannot be read.
pub fn validate_syntax<P: AsRef<Path>>(path: P) -> Result<SyntaxValidationResult> {
    validate_syntax_with_mode(path, HostlistValidationMode::Validate)
}

/// Validate filter list syntax, choosing which `HostlistCompiler`-compatible mode to emulate.
///
/// # Errors
///
/// Returns an error if file cannot be read.
pub fn validate_syntax_with_mode<P: AsRef<Path>>(
    path: P,
    mode: HostlistValidationMode,
) -> Result<SyntaxValidationResult> {
    let path = path.as_ref();
    let content = fs::read_to_string(path)?;
    Ok(validate_syntax_content_with_mode(&content, mode))
}

/// Validate filter list syntax from in-memory content.
///
/// Equivalent to [`validate_syntax_content_with_mode`] with [`HostlistValidationMode::Validate`].
///
/// Pure logic split out from [`validate_syntax`] so it can be exercised
/// directly by unit tests and fuzz targets without touching the filesystem.
#[must_use]
pub fn validate_syntax_content(content: &str) -> SyntaxValidationResult {
    validate_syntax_content_with_mode(content, HostlistValidationMode::Validate)
}

/// Validate filter list syntax from in-memory content, choosing which
/// `HostlistCompiler`-compatible mode to emulate.
#[must_use]
pub fn validate_syntax_content_with_mode(
    content: &str,
    mode: HostlistValidationMode,
) -> SyntaxValidationResult {
    let mut result = SyntaxValidationResult {
        is_valid: true,
        format: detect_format(content),
        valid_rules: 0,
        invalid_rules: 0,
        messages: Vec::new(),
    };

    for (line_num, line) in content.lines().enumerate() {
        let line = line.trim();

        // Comments and empty lines are neither valid nor invalid rules - skip them,
        // matching HostlistCompiler's `valid()`, which treats them as trivially valid
        // but that this crate has never counted either way.
        if line.is_empty() || is_comment(line) {
            continue;
        }

        if is_valid_rule(line, mode) {
            result.valid_rules += 1;
        } else {
            result.invalid_rules += 1;
            result
                .messages
                .push(format!("Line {}: Invalid syntax: {}", line_num + 1, line));
        }
    }

    if result.invalid_rules > 0 {
        result.is_valid = false;
    }

    if result.valid_rules == 0 {
        result.is_valid = false;
        result.messages.push("No valid rules found".to_string());
    }

    result
}

/// Detect filter format from content.
fn detect_format(content: &str) -> FilterFormat {
    static HOSTS_PATTERN: std::sync::LazyLock<Regex> =
        std::sync::LazyLock::new(|| Regex::new(r"^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+\s+").unwrap());

    let mut adblock_score = 0;
    let mut hosts_score = 0;

    for line in content.lines().take(50) {
        let line = line.trim();
        if line.is_empty() || is_comment(line) {
            continue;
        }

        // AdBlock patterns
        if line.starts_with("||")
            || line.starts_with("@@")
            || line.contains("##")
            || line.contains('$')
        {
            adblock_score += 2;
        }

        // Hosts file patterns
        if HOSTS_PATTERN.is_match(line) {
            hosts_score += 2;
        }
    }

    match adblock_score.cmp(&hosts_score) {
        std::cmp::Ordering::Greater => FilterFormat::Adblock,
        std::cmp::Ordering::Less => FilterFormat::Hosts,
        std::cmp::Ordering::Equal => FilterFormat::Unknown,
    }
}

/// Whether `line` is a comment, per `HostlistCompiler`'s `isComment()`: `!`-prefixed,
/// `# `-prefixed (hash *and* a space), exactly `#`, or `####`-prefixed. A bare `#foo`
/// with no following space is deliberately **not** a comment upstream, and is instead
/// evaluated (and rejected) as a malformed rule.
fn is_comment(line: &str) -> bool {
    line.starts_with('!') || line.starts_with("# ") || line == "#" || line.starts_with("####")
}

/// Whether `line` looks like an `/etc/hosts` entry: an IP-ish token (hex/dot/colon/brackets,
/// with an optional `%zone` suffix) followed by whitespace and one or more hostnames, with an
/// optional trailing `#comment`. Mirrors `HostlistCompiler`'s `etcHostsRegex`.
fn is_etc_hosts_rule(line: &str) -> bool {
    static ETC_HOSTS_PATTERN: std::sync::LazyLock<Regex> = std::sync::LazyLock::new(|| {
        Regex::new(r"^([a-fA-F0-9.:\[\]]+)(%[a-zA-Z0-9]+)?\s+([^#]+)(#.*)?$").unwrap()
    });
    ETC_HOSTS_PATTERN.is_match(line)
}

/// Dispatches a single non-comment, non-empty line to `/etc/hosts`-style or adblock-style
/// validation, matching `HostlistCompiler`'s own per-line auto-detection in `valid()`
/// (it does not trust a whole-file format guess for this decision, and neither do we).
fn is_valid_rule(line: &str, mode: HostlistValidationMode) -> bool {
    if is_etc_hosts_rule(line) {
        valid_etc_hosts_rule(line, mode.allow_ip())
    } else {
        valid_adblock_rule(line, mode.allow_ip(), mode.allow_public_suffix())
    }
}

/// Validates an `/etc/hosts` rule: every hostname on the line (there may be more than
/// one) must pass [`valid_hostname`]. Mirrors `validEtcHostsRule()`.
fn valid_etc_hosts_rule(line: &str, allow_ip: bool) -> bool {
    let without_comment = line.split('#').next().unwrap_or(line).trim();
    let mut tokens = without_comment.split_whitespace();
    // First token is the IP address itself, not a hostname to validate.
    if tokens.next().is_none() {
        return false;
    }
    let hostnames: Vec<&str> = tokens.collect();
    if hostnames.is_empty() {
        return false;
    }
    hostnames
        .iter()
        .all(|h| valid_hostname(h, allow_ip, false, false))
}

/// The list of modifiers that limit a rule to specific domains/clients (`ANY_PATTERN_MODIFIER`
/// in `validate.js`) — their presence allows an otherwise-too-broad public-suffix rule through.
const ANY_PATTERN_MODIFIER: &[&str] = &["denyallow", "badfilter", "client"];

/// The full list of modifiers hosts-level (DNS) blockers support (`SUPPORTED_MODIFIERS`).
/// Notably excludes browser-only modifiers like `third-party`/`document`/`popup` — those
/// can't mean anything at the DNS-resolution layer.
const SUPPORTED_MODIFIERS: &[&str] = &[
    "important",
    "~important",
    "dnstype",
    "dnsrewrite",
    "ctag",
    "denyallow",
    "badfilter",
    "client",
];

/// Minimum pattern length (`MAX_PATTERN_LENGTH` in `validate.js` — a "how short is too
/// short" ceiling used as a floor check, kept under its upstream name for traceability).
const MAX_PATTERN_LENGTH: usize = 5;

/// Tokenized adblock-style rule: pattern plus optional `$modifier,modifier=value` options.
struct AdblockRuleTokens<'a> {
    pattern: &'a str,
    options: Option<&'a str>,
}

/// Splits a rule into pattern and options, matching `parseRuleTokens()`: strips a leading
/// `@@`, short-circuits full-regex rules (`/.../ ` without a `replace=` option), then finds
/// the last un-escaped `$` scanning from the end.
fn parse_rule_tokens(rule_text: &str) -> Option<AdblockRuleTokens<'_>> {
    let start = if rule_text.starts_with("@@") { 2 } else { 0 };
    if rule_text.len() <= start {
        return None;
    }
    let pattern = &rule_text[start..];

    if pattern.len() > 1
        && pattern.starts_with('/')
        && pattern.ends_with('/')
        && !pattern.contains("replace=")
    {
        return Some(AdblockRuleTokens {
            pattern,
            options: None,
        });
    }

    let bytes = rule_text.as_bytes();
    let mut split_at = None;
    let mut i = rule_text.len();
    while i > start {
        i -= 1;
        if bytes[i] == b'$' && !(i > start && bytes[i - 1] == b'\\') {
            split_at = Some(i);
            break;
        }
    }

    Some(match split_at {
        Some(idx) => AdblockRuleTokens {
            pattern: &rule_text[start..idx],
            options: Some(&rule_text[idx + 1..]),
        },
        None => AdblockRuleTokens {
            pattern,
            options: None,
        },
    })
}

/// Splits a `$`-options string on `,`, honoring `\,` as an escaped (non-splitting) comma,
/// matching `splitByDelimiterWithEscapeCharacter(options, ',', '\\', false)`.
fn split_options(options: &str) -> Vec<String> {
    let mut parts = Vec::new();
    let mut current = String::new();
    let chars: Vec<char> = options.chars().collect();
    for (i, &c) in chars.iter().enumerate() {
        if c == ',' {
            if i == 0 {
                // leading comma: ignored, matches upstream's `i === 0` branch
            } else if chars[i - 1] == '\\' {
                current.pop();
                current.push(c);
            } else if !current.is_empty() {
                parts.push(std::mem::take(&mut current));
            }
        } else {
            current.push(c);
        }
    }
    if !current.is_empty() {
        parts.push(current);
    }
    parts
}

/// Returns the modifier names present in `options` (ignoring `=value` parts) — enough to
/// check against [`SUPPORTED_MODIFIERS`]/[`ANY_PATTERN_MODIFIER`].
fn option_names(options: &str) -> Vec<String> {
    split_options(options)
        .into_iter()
        .map(|part| part.split('=').next().unwrap_or("").to_string())
        .collect()
}

/// Structural breakdown of an IP-like adblock pattern, mirroring `parseIpPattern()`.
struct IpPatternInfo {
    prefix: &'static str,
    octet_count: usize,
    has_trailing_dot: bool,
    has_trailing_wildcard: bool,
    has_caret: bool,
}

fn is_valid_octet(s: &str) -> bool {
    !s.is_empty()
        && s.len() <= 3
        && s.chars().all(|c| c.is_ascii_digit())
        && s.parse::<u16>().is_ok_and(|n| n <= 255)
}

/// Parses `pattern` (already free of `@@`/`$options`) as an IP-shaped pattern, or returns
/// `None` if it doesn't look like one at all. Mirrors `parseIpPattern()`.
fn parse_ip_pattern(pattern: &str) -> Option<IpPatternInfo> {
    let mut remaining = pattern;
    let prefix = if let Some(rest) = remaining.strip_prefix("||") {
        remaining = rest;
        "||"
    } else if let Some(rest) = remaining.strip_prefix('|') {
        remaining = rest;
        "|"
    } else {
        ""
    };

    let has_caret = if let Some(rest) = remaining.strip_suffix("^|") {
        remaining = rest;
        true
    } else if let Some(rest) = remaining.strip_suffix('^') {
        remaining = rest;
        true
    } else {
        false
    };

    let has_trailing_wildcard = if let Some(rest) = remaining.strip_suffix(".*") {
        remaining = rest;
        true
    } else {
        false
    };

    let has_trailing_dot = if let Some(rest) = remaining.strip_suffix('.') {
        remaining = rest;
        true
    } else {
        false
    };

    let parts: Vec<&str> = remaining.split('.').collect();
    if parts.is_empty() || parts.len() > 4 || !parts.iter().all(|p| is_valid_octet(p)) {
        return None;
    }

    Some(IpPatternInfo {
        prefix,
        octet_count: parts.len(),
        has_trailing_dot,
        has_trailing_wildcard,
        has_caret,
    })
}

/// `1`/`2`-octet patterns are always too wide (`isTooWide`).
fn is_too_wide(info: &IpPatternInfo) -> bool {
    info.octet_count <= 2
}

/// A 3-octet pattern with no trailing dot/wildcard/caret is ambiguous (`isAmbiguous3Octet`) —
/// e.g. `192.168.1` alone, which could mean many things.
fn is_ambiguous_3_octet(info: &IpPatternInfo) -> bool {
    info.octet_count == 3
        && !info.has_trailing_dot
        && !info.has_trailing_wildcard
        && !info.has_caret
}

/// A `||`-prefixed 3-octet subnet with a trailing dot/wildcard and no caret — the *only*
/// subnet shape `ValidateAllowIp` accepts (`is3OctetSubnetWithSuffix`).
fn is_3_octet_subnet_with_suffix(pattern: &str) -> bool {
    match parse_ip_pattern(pattern) {
        Some(info) => {
            info.octet_count == 3
                && (info.has_trailing_dot || info.has_trailing_wildcard)
                && !info.has_caret
                && info.prefix == "||"
        }
        None => false,
    }
}

/// Prefixed (`|`/`||`) IP-like subnet pattern that should be rejected outright, except (in
/// `ValidateAllowIp`) a 3-octet subnet with a trailing dot/wildcard. Mirrors `isIpSubnetPattern()`.
fn is_ip_subnet_pattern(pattern: &str) -> bool {
    match parse_ip_pattern(pattern) {
        Some(info) if !info.prefix.is_empty() => {
            if info.octet_count < 4 {
                true
            } else {
                info.has_trailing_dot || info.has_trailing_wildcard
            }
        }
        _ => false,
    }
}

/// Prefix-less, caret-terminated IP-suffix pattern (`1.1^`, `1.1.1.1^`) that matches string
/// endings unpredictably and must always be rejected. Mirrors `isIpSuffixPattern()`.
fn is_ip_suffix_pattern(pattern: &str) -> bool {
    match parse_ip_pattern(pattern) {
        Some(info) => info.prefix.is_empty() && info.has_caret && info.octet_count >= 2,
        None => false,
    }
}

/// Prefix-less, caret-less IP pattern that's too wide or ambiguous (`1.2.`, `192.168`,
/// `192.168.1`) and must always be rejected. Mirrors `isUnsafeIpPattern()`.
fn is_unsafe_ip_pattern(pattern: &str) -> bool {
    if pattern.contains('^') {
        return false;
    }
    let Some(info) = parse_ip_pattern(pattern) else {
        return false;
    };
    if is_too_wide(&info) || is_ambiguous_3_octet(&info) {
        return true;
    }
    (info.octet_count == 3 || info.octet_count == 4)
        && (info.has_trailing_dot || info.has_trailing_wildcard)
        && info.prefix.is_empty()
}

/// Matches exact domain-style adblock patterns: `||example.org^`, `*.org^`, `.org^`, `||org^`
/// (optionally with a trailing `|` for exception-end anchors). Mirrors `EXACT_DOMAIN_PATTERN`.
fn exact_domain_pattern_regex() -> &'static Regex {
    static PATTERN: std::sync::LazyLock<Regex> = std::sync::LazyLock::new(|| {
        Regex::new(
            r"^(?:\|\|)?(?:\*\.|\.)?([a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?)*\.?)\^\|?$",
        )
        .unwrap()
    });
    &PATTERN
}

/// Extracts the hostname-like capture from [`exact_domain_pattern_regex`], normalizing away
/// a trailing FQDN dot. Mirrors `extractDomainPattern()`.
fn extract_domain_pattern(pattern: &str) -> Option<String> {
    let caps = exact_domain_pattern_regex().captures(pattern)?;
    let hostname = caps.get(1)?.as_str();
    Some(hostname.strip_suffix('.').unwrap_or(hostname).to_string())
}

/// Checks whether `hostname` is acceptable in a blocklist. Mirrors `validHostname()` **except**
/// for its public-suffix-list lookup (`tldts.parse(...).publicSuffix`/`isIcann`/`isPrivate`):
/// without embedding a real PSL, this crate cannot yet tell "co.uk" (a public suffix, normally
/// rejected) apart from "example.com" (an ordinary registrable domain, always fine). See
/// `docs/adr/0003-adguard-hostlist-compatibility.md` Phase 2. Until that lands, whole-public-suffix
/// rules are never rejected here even when `allow_public_suffix` is `false` — a known, deliberately
/// narrower-than-upstream gap (permissive, not silently wrong: we never *reject* a rule upstream
/// would accept because of this, only fail to reject some upstream would).
fn valid_hostname(
    hostname: &str,
    allow_ip: bool,
    _has_limit_modifier: bool,
    _allow_public_suffix: bool,
) -> bool {
    if !hostname.chars().any(|c| c.is_ascii_alphanumeric()) {
        return false;
    }
    if !allow_ip && hostname.parse::<IpAddr>().is_ok() {
        return false;
    }
    true
}

/// Validates an adblock-style rule against `HostlistCompiler`'s `validAdblockRule()` semantics.
fn valid_adblock_rule(rule_text: &str, allow_ip: bool, allow_public_suffix: bool) -> bool {
    let Some(tokens) = parse_rule_tokens(rule_text) else {
        return false;
    };
    let pattern = tokens.pattern;

    let mut has_limit_modifier = false;
    if let Some(options) = tokens.options {
        for name in option_names(options) {
            if !SUPPORTED_MODIFIERS.contains(&name.as_str()) {
                return false;
            }
            if ANY_PATTERN_MODIFIER.contains(&name.as_str()) {
                has_limit_modifier = true;
            }
        }
    }

    let exact_domain = extract_domain_pattern(pattern);
    if pattern.len() < MAX_PATTERN_LENGTH && exact_domain.is_none() {
        return false;
    }

    // Regex rules (`/.../`) may contain any characters - nothing further to check.
    if pattern.len() > 1 && pattern.starts_with('/') && pattern.ends_with('/') {
        return true;
    }

    let to_test = pattern.strip_prefix("://").unwrap_or(pattern);
    static CHAR_CHECK: std::sync::LazyLock<Regex> =
        std::sync::LazyLock::new(|| Regex::new(r"^[a-zA-Z0-9\-.*|^]+$").unwrap());
    if !CHAR_CHECK.is_match(to_test) {
        return false;
    }

    let has_denyallow = tokens
        .options
        .map(|o| option_names(o).iter().any(|n| n == "denyallow"))
        .unwrap_or(false);
    if has_denyallow && parse_ip_pattern(pattern).is_some() {
        return false;
    }

    if is_ip_suffix_pattern(pattern) {
        return false;
    }
    if is_unsafe_ip_pattern(pattern) {
        return false;
    }
    if is_ip_subnet_pattern(pattern) && !(allow_ip && is_3_octet_subnet_with_suffix(pattern)) {
        return false;
    }

    let mut ip_candidate = pattern;
    if let Some(rest) = ip_candidate.strip_prefix("||") {
        ip_candidate = rest;
    } else if let Some(rest) = ip_candidate.strip_prefix('|') {
        ip_candidate = rest;
    }
    let ip_candidate = ip_candidate
        .strip_suffix("^|")
        .or_else(|| ip_candidate.strip_suffix('^'))
        .unwrap_or(ip_candidate);
    if ip_candidate.parse::<IpAddr>().is_ok() {
        return allow_ip;
    }

    let sep_idx = pattern.find('^');
    let wildcard_idx = pattern.find('*');
    if let (Some(s), Some(w)) = (sep_idx, wildcard_idx) {
        if w > s {
            return false;
        }
    }

    if let Some(domain) = &exact_domain {
        return valid_hostname(domain, allow_ip, has_limit_modifier, allow_public_suffix);
    }

    let Some(sep_idx) = sep_idx else {
        return true;
    };
    if !pattern.starts_with("||") {
        return true;
    }

    let domain_to_check = &pattern["||".len()..sep_idx];

    if let Some(wildcard_idx) = wildcard_idx {
        let starts_with_wildcard_domain = domain_to_check.starts_with("*.");
        let _ = wildcard_idx;
        if starts_with_wildcard_domain {
            let cleaned = domain_to_check.trim_start_matches("*.");
            return valid_hostname(cleaned, allow_ip, has_limit_modifier, allow_public_suffix);
        }
        return true;
    }

    if !valid_hostname(
        domain_to_check,
        allow_ip,
        has_limit_modifier,
        allow_public_suffix,
    ) {
        return false;
    }

    // Nothing may follow the `^` separator except a single trailing `|`.
    let after = &pattern[sep_idx + 1..];
    after.is_empty() || after == "|"
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;
    use tempfile::NamedTempFile;

    #[test]
    fn test_detect_format_adblock() {
        let content = "||example.com^\n@@||allowed.com^\n##.ad-banner";
        assert_eq!(detect_format(content), FilterFormat::Adblock);
    }

    #[test]
    fn test_detect_format_hosts() {
        let content = "127.0.0.1 localhost\n0.0.0.0 example.com\n0.0.0.0 ads.com";
        assert_eq!(detect_format(content), FilterFormat::Hosts);
    }

    // --- Modifier allowlist (SUPPORTED_MODIFIERS) ---

    #[test]
    fn test_supported_modifiers_accepted() {
        assert!(valid_adblock_rule("||example.com^$important", false, false));
        assert!(valid_adblock_rule("||example.com^$dnstype=A", false, false));
        assert!(valid_adblock_rule(
            "||example.com^$dnsrewrite=NXDOMAIN",
            false,
            false
        ));
        assert!(valid_adblock_rule(
            "||example.com^$ctag=device_pc",
            false,
            false
        ));
        assert!(valid_adblock_rule("||example.com^$badfilter", false, false));
        assert!(valid_adblock_rule(
            "||example.com^$denyallow=trusted.com",
            false,
            false
        ));
        assert!(valid_adblock_rule(
            "||example.com^$client=192.168.1.1",
            false,
            false
        ));
    }

    #[test]
    fn test_browser_only_modifiers_rejected() {
        // These are exactly the modifiers a DNS-level blocker can't act on - meaningless
        // (and previously silently accepted) input for this toolkit's actual target.
        assert!(!valid_adblock_rule(
            "||example.com^$third-party",
            false,
            false
        ));
        assert!(!valid_adblock_rule("||example.com^$document", false, false));
        assert!(!valid_adblock_rule("||example.com^$popup", false, false));
        assert!(!valid_adblock_rule(
            "||example.com^$domain=example.org",
            false,
            false
        ));
    }

    // --- Cosmetic rules: meaningless at the DNS level, and upstream rejects them ---

    #[test]
    fn test_cosmetic_rules_rejected() {
        assert!(!valid_adblock_rule("##.ad-banner", false, false));
        assert!(!valid_adblock_rule("example.com##.ad-banner", false, false));
    }

    // --- IP pattern classification ---

    #[test]
    fn test_ip_suffix_pattern_always_rejected() {
        assert!(!valid_adblock_rule("1.1^", false, false));
        assert!(!valid_adblock_rule("1.1.1^", false, false));
        assert!(!valid_adblock_rule("1.1.1.1^", false, false));
        assert!(!valid_adblock_rule("1.1^", true, true));
    }

    #[test]
    fn test_unsafe_ip_pattern_always_rejected() {
        assert!(!valid_adblock_rule("1.2.", false, false));
        assert!(!valid_adblock_rule("192.168", false, false));
        assert!(!valid_adblock_rule("192.168.1", false, false));
    }

    #[test]
    fn test_full_ip_requires_allow_ip() {
        assert!(!valid_adblock_rule("||1.2.3.4^", false, false));
        assert!(valid_adblock_rule("||1.2.3.4^", true, false));
    }

    #[test]
    fn test_3_octet_subnet_only_valid_with_allow_ip() {
        // Note: no trailing `^` - these are subnet-wildcard patterns (trailing `.`/`.*`),
        // not caret-terminated ones; a caret changes the shape entirely (see
        // test_ip_suffix_pattern_always_rejected for that case).
        assert!(!valid_adblock_rule("||192.168.1.", false, false));
        assert!(valid_adblock_rule("||192.168.1.", true, false));
        assert!(valid_adblock_rule("||192.168.1.*", true, false));
        // 1-2 octet subnets are rejected even with allow_ip.
        assert!(!valid_adblock_rule("||192.", true, false));
    }

    #[test]
    fn test_denyallow_with_ip_pattern_rejected() {
        assert!(!valid_adblock_rule(
            "||1.2.3.4^$denyallow=x.com",
            true,
            false
        ));
    }

    // --- Domain / hostname patterns ---

    #[test]
    fn test_exact_domain_patterns() {
        assert!(valid_adblock_rule("||example.com^", false, false));
        assert!(valid_adblock_rule("||example.com^|", false, false));
        assert!(valid_adblock_rule("*.example.com^", false, false));
    }

    #[test]
    fn test_pattern_too_short_rejected() {
        // "a.b^" is 4 chars, below MAX_PATTERN_LENGTH, and not an exact-domain match
        // for a single-letter TLD-shaped pattern the length floor still applies to.
        assert!(!valid_adblock_rule("a.b", false, false));
    }

    #[test]
    fn test_wildcard_after_separator_rejected() {
        assert!(!valid_adblock_rule("||example.org^test*", false, false));
    }

    #[test]
    fn test_char_check_rejects_non_domain_characters() {
        assert!(!valid_adblock_rule(
            "example.com/path?query=1",
            false,
            false
        ));
    }

    #[test]
    fn test_regex_rule_bypasses_char_check() {
        assert!(valid_adblock_rule("/example\\.(com|org)/", false, false));
    }

    // --- End-to-end via validate_syntax_content ---

    #[test]
    fn test_validate_syntax_content_dns_compatible_rules() {
        let result = validate_syntax_content("! Comment\n||example.com^\n@@||allowed.com^\n");
        assert!(result.is_valid);
        assert_eq!(result.invalid_rules, 0);
        assert_eq!(result.valid_rules, 2);
    }

    #[test]
    fn test_validate_syntax_content_rejects_cosmetic_and_browser_modifiers() {
        let result =
            validate_syntax_content("||example.com^\n##.ad-banner\n||x.com^$third-party\n");
        assert_eq!(result.valid_rules, 1);
        assert_eq!(result.invalid_rules, 2);
        assert!(!result.is_valid);
    }

    #[test]
    fn test_validate_syntax_content_hosts_multi_hostname_line() {
        let result =
            validate_syntax_content("0.0.0.0 example.com ads.example.com\n127.0.0.1 localhost\n");
        assert_eq!(result.invalid_rules, 0);
        assert_eq!(result.valid_rules, 2);
    }

    #[test]
    fn test_validate_syntax_content_mode_allow_ip() {
        let strict =
            validate_syntax_content_with_mode("||1.2.3.4^\n", HostlistValidationMode::Validate);
        assert_eq!(strict.invalid_rules, 1);

        let permissive = validate_syntax_content_with_mode(
            "||1.2.3.4^\n",
            HostlistValidationMode::ValidateAllowIp,
        );
        assert_eq!(permissive.invalid_rules, 0);
    }

    #[test]
    fn test_validate_syntax() {
        let mut file = NamedTempFile::new().unwrap();
        writeln!(file, "! Comment").unwrap();
        writeln!(file, "||example.com^").unwrap();
        writeln!(file, "@@||allowed.com^").unwrap();
        file.flush().unwrap();

        let result = validate_syntax(file.path()).unwrap();
        assert!(result.is_valid);
        assert_eq!(result.format, FilterFormat::Adblock);
        assert!(result.valid_rules >= 2);
    }
}
