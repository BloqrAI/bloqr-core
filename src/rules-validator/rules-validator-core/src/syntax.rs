//! Syntax validation for filter rules.

use regex::Regex;
use serde::Serialize;
use std::fs;
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
/// # Errors
///
/// Returns an error if file cannot be read.
pub fn validate_syntax<P: AsRef<Path>>(path: P) -> Result<SyntaxValidationResult> {
    let path = path.as_ref();
    let content = fs::read_to_string(path)?;
    Ok(validate_syntax_content(&content))
}

/// Validate filter list syntax from in-memory content.
///
/// Pure logic split out from [`validate_syntax`] so it can be exercised
/// directly by unit tests and fuzz targets without touching the filesystem.
#[must_use]
pub fn validate_syntax_content(content: &str) -> SyntaxValidationResult {
    let mut result = SyntaxValidationResult {
        is_valid: true,
        format: detect_format(content),
        valid_rules: 0,
        invalid_rules: 0,
        messages: Vec::new(),
    };

    for (line_num, line) in content.lines().enumerate() {
        let line = line.trim();

        // Skip empty lines and comments
        if line.is_empty() || line.starts_with('!') || line.starts_with('#') {
            continue;
        }

        if is_valid_rule(line, result.format) {
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
        if line.is_empty() || line.starts_with('!') || line.starts_with('#') {
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

/// Check if a line is a valid rule.
fn is_valid_rule(line: &str, format: FilterFormat) -> bool {
    match format {
        FilterFormat::Adblock => is_valid_adblock_rule(line),
        FilterFormat::Hosts => is_valid_hosts_rule(line),
        FilterFormat::Unknown => is_valid_adblock_rule(line) || is_valid_hosts_rule(line),
    }
}

/// Validate AdBlock rule.
fn is_valid_adblock_rule(line: &str) -> bool {
    // Basic AdBlock rule validation
    !line.is_empty()
        && (line.starts_with("||")
            || line.starts_with("@@")
            || line.contains("##")
            || line.contains("$")
            || line.starts_with('/')
            || Regex::new(r"^[a-zA-Z0-9\-\.]+\^?$").unwrap().is_match(line))
}

/// Validate hosts file rule.
fn is_valid_hosts_rule(line: &str) -> bool {
    // Hosts file format: IP_ADDRESS DOMAIN
    Regex::new(r"^([0-9]+\.[0-9]+\.[0-9]+\.[0-9]+|::1?)\s+[a-zA-Z0-9\-\.]+")
        .unwrap()
        .is_match(line)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;
    use tempfile::NamedTempFile;

    #[test]
    fn test_detect_format_adblock() {
        let content = "||example.com^\n@@||allowed.com\n##.ad-banner";
        assert_eq!(detect_format(content), FilterFormat::Adblock);
    }

    #[test]
    fn test_detect_format_hosts() {
        let content = "127.0.0.1 localhost\n0.0.0.0 example.com\n0.0.0.0 ads.com";
        assert_eq!(detect_format(content), FilterFormat::Hosts);
    }

    #[test]
    fn test_is_valid_adblock_rule() {
        assert!(is_valid_adblock_rule("||example.com^"));
        assert!(is_valid_adblock_rule("@@||allowed.com"));
        assert!(is_valid_adblock_rule("##.ad-class"));
        assert!(!is_valid_adblock_rule(""));
    }

    #[test]
    fn test_is_valid_hosts_rule() {
        assert!(is_valid_hosts_rule("0.0.0.0 example.com"));
        assert!(is_valid_hosts_rule("127.0.0.1 localhost"));
        assert!(!is_valid_hosts_rule("invalid rule"));
    }

    #[test]
    fn test_validate_syntax() {
        let mut file = NamedTempFile::new().unwrap();
        writeln!(file, "! Comment").unwrap();
        writeln!(file, "||example.com^").unwrap();
        writeln!(file, "@@||allowed.com").unwrap();
        file.flush().unwrap();

        let result = validate_syntax(file.path()).unwrap();
        assert!(result.is_valid);
        assert_eq!(result.format, FilterFormat::Adblock);
        assert!(result.valid_rules >= 2);
    }
}
