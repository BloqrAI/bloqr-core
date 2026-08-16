//! URL security validation module.

use regex::Regex;
use reqwest::blocking::Client;
use reqwest::redirect;
use serde::Serialize;
use std::io::Read;
use std::net::{IpAddr, Ipv4Addr, ToSocketAddrs};
use std::time::Duration;
use url::Url;

use crate::error::{Result, ValidationError};
use crate::hash::compute_hash;

/// Maximum response body size accepted from a filter-list source (bytes).
const MAX_CONTENT_BYTES: u64 = 50 * 1024 * 1024;

/// Maximum number of HTTPS redirects followed before giving up.
const MAX_REDIRECTS: usize = 5;

/// Returns `true` if `ip` must be refused as a fetch target: loopback,
/// private, link-local (this covers the `169.254.169.254` cloud metadata
/// endpoint), unspecified, multicast, broadcast, or documentation/testing
/// ranges. Used to block SSRF via a filter-list source URL - or an HTTPS
/// redirect hop - pointing at internal infrastructure.
fn is_disallowed_ip(ip: IpAddr) -> bool {
    match ip {
        IpAddr::V4(v4) => is_disallowed_ipv4(v4),
        IpAddr::V6(v6) => {
            v6.is_loopback()
                || v6.is_unspecified()
                || v6.is_multicast()
                // Unique local (fc00::/7) - Ipv6Addr::is_unique_local() is
                // still unstable, so check the prefix directly.
                || (v6.segments()[0] & 0xfe00) == 0xfc00
                || v6
                    .to_ipv4_mapped()
                    .is_some_and(is_disallowed_ipv4)
        }
    }
}

fn is_disallowed_ipv4(v4: Ipv4Addr) -> bool {
    v4.is_loopback()
        || v4.is_private()
        || v4.is_link_local()
        || v4.is_unspecified()
        || v4.is_broadcast()
        || v4.is_documentation()
        || v4.is_multicast()
}

/// Validates that `url` is safe to fetch: HTTPS only, and its host must not
/// resolve to a loopback/private/link-local/metadata address. Applied to
/// both the initial URL and every HTTPS redirect hop.
///
/// The hostname (if not an IP literal) is resolved once, synchronously,
/// right here - the actual connection is made separately by reqwest and
/// re-resolves DNS itself, so this does not fully close a DNS-rebinding
/// TOCTOU window (a malicious authoritative DNS server could serve a public
/// address here and a private one moments later, at connect time). That's
/// an accepted tradeoff for a filter-list URL validator, not a
/// general-purpose network proxy - closing it fully would require a custom
/// `reqwest` DNS resolver pinning the address used for both checks.
fn validate_fetch_target(url: &Url) -> std::result::Result<(), String> {
    if url.scheme() != "https" {
        return Err(format!(
            "Insecure protocol '{}' - only HTTPS is allowed",
            url.scheme()
        ));
    }

    let host = url
        .host_str()
        .ok_or_else(|| "Missing or invalid host".to_string())?;

    if let Ok(ip) = host.parse::<IpAddr>() {
        return if is_disallowed_ip(ip) {
            Err(format!("Refusing to connect to disallowed address '{ip}'"))
        } else {
            Ok(())
        };
    }

    let port = url.port_or_known_default().unwrap_or(443);
    let addrs = (host, port)
        .to_socket_addrs()
        .map_err(|e| format!("DNS resolution failed for '{host}': {e}"))?;

    let mut resolved_any = false;
    for addr in addrs {
        resolved_any = true;
        if is_disallowed_ip(addr.ip()) {
            return Err(format!(
                "Refusing to connect to '{host}' - resolves to disallowed address '{}'",
                addr.ip()
            ));
        }
    }

    if resolved_any {
        Ok(())
    } else {
        Err(format!("DNS resolution for '{host}' returned no addresses"))
    }
}

/// Builds a redirect policy that re-validates every hop with
/// [`validate_fetch_target`] and caps the chain at [`MAX_REDIRECTS`].
/// Without this, `reqwest`'s default policy (follow up to 10 redirects)
/// would let an initially-valid HTTPS URL redirect to an internal or
/// loopback address and be followed anyway.
fn hardened_redirect_policy() -> redirect::Policy {
    redirect::Policy::custom(|attempt| {
        if attempt.previous().len() >= MAX_REDIRECTS {
            return attempt.error("too many redirects");
        }
        match validate_fetch_target(attempt.url()) {
            Ok(()) => attempt.follow(),
            Err(msg) => attempt.error(msg),
        }
    })
}

/// URL validation result.
#[derive(Debug, Clone, Serialize)]
pub struct UrlValidationResult {
    /// Whether the URL passed validation.
    pub is_valid: bool,
    /// Validation errors/warnings.
    pub messages: Vec<String>,
    /// Content SHA-384 hash (if downloaded).
    pub content_hash: Option<String>,
    /// Content size in bytes.
    pub content_size: Option<u64>,
}

impl UrlValidationResult {
    /// Create a successful validation result.
    #[must_use]
    pub fn valid() -> Self {
        Self {
            is_valid: true,
            messages: Vec::new(),
            content_hash: None,
            content_size: None,
        }
    }

    /// Create a failed validation result.
    #[must_use]
    pub fn invalid(message: impl Into<String>) -> Self {
        Self {
            is_valid: false,
            messages: vec![message.into()],
            content_hash: None,
            content_size: None,
        }
    }

    /// Add a message.
    pub fn add_message(&mut self, message: impl Into<String>) {
        self.messages.push(message.into());
    }
}

/// Validate a URL for security and proper filter list format.
///
/// Performs comprehensive security checks:
/// 1. HTTPS protocol enforcement
/// 2. Domain validation via DNS
/// 3. Content-Type verification
/// 4. Content scanning for valid filter syntax
/// 5. Optional SHA-384 hash verification
///
/// # Errors
///
/// Returns an error if validation fails in strict mode.
pub fn validate_url(url_str: &str, expected_hash: Option<&str>) -> Result<UrlValidationResult> {
    let mut result = UrlValidationResult::valid();

    // Parse URL
    let url = Url::parse(url_str)
        .map_err(|e| ValidationError::url_validation(url_str, format!("Invalid URL: {e}")))?;

    // 1+2. HTTPS enforcement, host validation, and SSRF guard (loopback/
    // private/link-local/metadata addresses refused).
    if let Err(msg) = validate_fetch_target(&url) {
        result.is_valid = false;
        result.add_message(msg);
        return Ok(result);
    }

    // 3. Download and verify content. The redirect policy re-runs the same
    // SSRF guard on every hop - see hardened_redirect_policy().
    let client = Client::builder()
        .timeout(Duration::from_secs(30))
        .user_agent("AdGuard-Validation/1.0")
        .redirect(hardened_redirect_policy())
        .build()
        .map_err(|e| ValidationError::url_validation(url_str, format!("HTTP client error: {e}")))?;

    let response = client
        .get(url_str)
        .send()
        .map_err(|e| ValidationError::url_validation(url_str, format!("Request failed: {e}")))?;

    // Check status
    if !response.status().is_success() {
        result.is_valid = false;
        result.add_message(format!(
            "HTTP {} {}",
            response.status().as_u16(),
            response.status().canonical_reason().unwrap_or("Unknown")
        ));
        return Ok(result);
    }

    // 4. Content-Type verification
    if let Some(content_type) = response.headers().get("content-type") {
        let content_type = content_type.to_str().unwrap_or("");
        if !content_type.contains("text/plain") && !content_type.contains("text/") {
            result.add_message(format!(
                "Unexpected Content-Type: {content_type} (expected text/plain)"
            ));
        }
    }

    // 5. Content-Length pre-check - advisory only, since a server can lie
    // about or omit this header. The streaming read-cap below is the real
    // limit enforcement.
    if let Some(len) = response.content_length() {
        if len > MAX_CONTENT_BYTES {
            result.is_valid = false;
            result.add_message(format!(
                "File too large: {len} bytes (max {MAX_CONTENT_BYTES} bytes)"
            ));
            return Ok(result);
        }
    }

    // Download content, capped at MAX_CONTENT_BYTES + 1 so a response with
    // no (or a lying) Content-Length can't be read unbounded into memory
    // before the size check below ever runs.
    let mut content = Vec::new();
    response
        .take(MAX_CONTENT_BYTES + 1)
        .read_to_end(&mut content)
        .map_err(|e| ValidationError::url_validation(url_str, format!("Download failed: {e}")))?;

    if content.len() as u64 > MAX_CONTENT_BYTES {
        result.is_valid = false;
        result.add_message(format!(
            "File too large: exceeds {MAX_CONTENT_BYTES} bytes (download aborted)"
        ));
        return Ok(result);
    }

    result.content_size = Some(content.len() as u64);

    // 6. Content validation (scan first 1KB for filter syntax)
    let preview = String::from_utf8_lossy(&content[..content.len().min(1024)]);
    if !is_valid_filter_content(&preview) {
        result.add_message("Content does not appear to be a valid filter list");
    }

    // 7. Hash verification
    let actual_hash = compute_hash(&content);
    result.content_hash = Some(actual_hash.clone());

    if let Some(expected) = expected_hash {
        if actual_hash != expected {
            result.is_valid = false;
            result.add_message(format!(
                "Hash mismatch: expected {expected}, got {actual_hash}"
            ));
            return Ok(result);
        }
    }

    Ok(result)
}

/// Check if content appears to be a valid filter list.
fn is_valid_filter_content(content: &str) -> bool {
    // Look for common filter list patterns
    let patterns = [
        r"^!",              // Comment
        r"^#",              // Comment or cosmetic rule
        r"^\|\|",           // Domain blocking rule
        r"^@@",             // Exception rule
        r"^[0-9]+\.[0-9]+", // IP address (hosts format)
        r"##",              // Cosmetic rule
        r"\$",              // Rule options
    ];

    let mut found_patterns = 0;
    for line in content.lines().take(20) {
        let line = line.trim();
        if line.is_empty() {
            continue;
        }

        for pattern in &patterns {
            if Regex::new(pattern).is_ok_and(|re| re.is_match(line)) {
                found_patterns += 1;
                break;
            }
        }
    }

    // At least 3 lines should match filter patterns
    found_patterns >= 3
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_url_validation_result() {
        let result = UrlValidationResult::valid();
        assert!(result.is_valid);
        assert!(result.messages.is_empty());

        let mut result = UrlValidationResult::invalid("test error");
        assert!(!result.is_valid);
        assert_eq!(result.messages.len(), 1);

        result.add_message("another error");
        assert_eq!(result.messages.len(), 2);
    }

    #[test]
    fn test_is_valid_filter_content() {
        let valid_content = "! Comment\n||example.com^\n@@||allowed.com^\n";
        assert!(is_valid_filter_content(valid_content));

        let invalid_content = "random text\nmore random\nnothing here\n";
        assert!(!is_valid_filter_content(invalid_content));
    }

    #[test]
    fn test_validate_url_http_rejected() {
        let result = validate_url("http://insecure.example.com/list.txt", None).unwrap();
        assert!(!result.is_valid);
        assert!(result.messages[0].contains("HTTPS"));
    }

    #[test]
    fn test_validate_url_invalid() {
        let result = validate_url("not-a-url", None);
        assert!(result.is_err());
    }

    #[test]
    fn test_is_disallowed_ip_v4() {
        assert!(is_disallowed_ip("127.0.0.1".parse().unwrap())); // loopback
        assert!(is_disallowed_ip("169.254.169.254".parse().unwrap())); // cloud metadata (link-local)
        assert!(is_disallowed_ip("10.0.0.1".parse().unwrap())); // private
        assert!(is_disallowed_ip("172.16.0.1".parse().unwrap())); // private
        assert!(is_disallowed_ip("192.168.1.1".parse().unwrap())); // private
        assert!(is_disallowed_ip("0.0.0.0".parse().unwrap())); // unspecified
        assert!(is_disallowed_ip("255.255.255.255".parse().unwrap())); // broadcast
        assert!(!is_disallowed_ip("8.8.8.8".parse().unwrap())); // public
        assert!(!is_disallowed_ip("1.1.1.1".parse().unwrap())); // public
    }

    #[test]
    fn test_is_disallowed_ip_v6() {
        assert!(is_disallowed_ip("::1".parse().unwrap())); // loopback
        assert!(is_disallowed_ip("::".parse().unwrap())); // unspecified
        assert!(is_disallowed_ip("fc00::1".parse().unwrap())); // unique local
        assert!(is_disallowed_ip("::ffff:127.0.0.1".parse().unwrap())); // IPv4-mapped loopback
        assert!(!is_disallowed_ip("2606:4700:4700::1111".parse().unwrap())); // public
    }

    #[test]
    fn test_validate_fetch_target_rejects_http() {
        let url = Url::parse("http://example.com/list.txt").unwrap();
        let err = validate_fetch_target(&url).unwrap_err();
        assert!(err.contains("HTTPS"));
    }

    #[test]
    fn test_validate_fetch_target_rejects_loopback_literal() {
        let url = Url::parse("https://127.0.0.1/list.txt").unwrap();
        let err = validate_fetch_target(&url).unwrap_err();
        assert!(err.contains("disallowed address"));
    }

    #[test]
    fn test_validate_fetch_target_rejects_metadata_literal() {
        let url = Url::parse("https://169.254.169.254/latest/meta-data/").unwrap();
        let err = validate_fetch_target(&url).unwrap_err();
        assert!(err.contains("disallowed address"));
    }

    #[test]
    fn test_validate_fetch_target_allows_public_literal() {
        let url = Url::parse("https://1.1.1.1/list.txt").unwrap();
        assert!(validate_fetch_target(&url).is_ok());
    }

    #[test]
    fn test_validate_url_rejects_loopback_without_network() {
        // Exercises the same path validate_url() takes before ever building
        // an HTTP client - no network access required.
        let result = validate_url("https://127.0.0.1/list.txt", None).unwrap();
        assert!(!result.is_valid);
        assert!(result.messages[0].contains("disallowed address"));
    }
}
