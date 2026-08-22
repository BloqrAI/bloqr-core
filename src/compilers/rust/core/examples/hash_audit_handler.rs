//! Example implementation of hash verification event handler.
//!
//! This example demonstrates how to use the hash verification events
//! to monitor and validate integrity at each stage of compilation.

use bloqr_compiler::events::{
    CompilationEventHandler, HashComputedEventArgs, HashMismatchEventArgs, HashVerifiedEventArgs,
};

/// Example handler that logs all hash verification events.
///
/// This handler demonstrates:
/// - Logging hash computations for audit trail
/// - Tracking verified hashes
/// - Custom policy on hash mismatches (optional continuation)
pub struct HashAuditHandler {
    /// Whether to allow continuation on hash mismatch
    allow_mismatch_continuation: bool,
}

impl HashAuditHandler {
    /// Create a new handler with strict verification (no continuation on mismatch).
    pub fn new() -> Self {
        Self {
            allow_mismatch_continuation: false,
        }
    }

    /// Create a new handler that allows continuation on hash mismatch.
    ///
    /// This is useful for testing or when you want to log mismatches but not fail.
    pub fn new_permissive() -> Self {
        Self {
            allow_mismatch_continuation: true,
        }
    }
}

impl Default for HashAuditHandler {
    fn default() -> Self {
        Self::new()
    }
}

impl CompilationEventHandler for HashAuditHandler {
    fn on_hash_computed(&self, args: &HashComputedEventArgs) {
        eprintln!(
            "[AUDIT] Hash computed for {} ({}): {}... ({} bytes)",
            args.item_type,
            args.item_identifier,
            &args.hash[..16.min(args.hash.len())],
            args.size_bytes
        );
    }

    fn on_hash_verified(&self, args: &HashVerifiedEventArgs) {
        eprintln!(
            "[AUDIT] Hash verified for {} ({}): ✓ Match (computed in {:.2}ms)",
            args.item_type, args.item_identifier, args.computation_duration_ms
        );
    }

    fn on_hash_mismatch(&self, args: &mut HashMismatchEventArgs) {
        eprintln!(
            "[AUDIT] Hash mismatch for {} ({}):",
            args.item_type, args.item_identifier
        );
        eprintln!(
            "  Expected: {}...",
            &args.expected_hash[..16.min(args.expected_hash.len())]
        );
        eprintln!("  Actual:   {}...", &args.actual_hash[..16]);

        if self.allow_mismatch_continuation {
            eprintln!("  [WARNING] Allowing continuation despite mismatch");
            args.allow_continuation = true;
            args.abort = false;
        } else {
            eprintln!("  [ERROR] Aborting compilation due to hash mismatch");
        }
    }
}

fn main() {
    println!("HashAuditHandler is a CompilationEventHandler you can register with the compiler.");
    println!("See the struct and trait impl in this file for a usage example.");
    let _strict = HashAuditHandler::new();
    let _permissive = HashAuditHandler::new_permissive();
}

#[cfg(test)]
mod tests {
    use super::*;
    use bloqr_compiler::events::EventTimestamp;

    #[test]
    fn test_audit_handler_creation() {
        let handler = HashAuditHandler::new();
        assert!(!handler.allow_mismatch_continuation);

        let permissive = HashAuditHandler::new_permissive();
        assert!(permissive.allow_mismatch_continuation);
    }

    #[test]
    fn test_hash_mismatch_strict() {
        let handler = HashAuditHandler::new();
        let mut args = HashMismatchEventArgs {
            base: EventTimestamp::default(),
            item_identifier: "test.txt".to_string(),
            item_type: "test_file".to_string(),
            expected_hash: "abc123".to_string(),
            actual_hash: "def456".to_string(),
            size_bytes: 100,
            abort: true,
            abort_reason: Some("Test".to_string()),
            allow_continuation: false,
        };

        handler.on_hash_mismatch(&mut args);
        assert!(args.abort);
        assert!(!args.allow_continuation);
    }

    #[test]
    fn test_hash_mismatch_permissive() {
        let handler = HashAuditHandler::new_permissive();
        let mut args = HashMismatchEventArgs {
            base: EventTimestamp::default(),
            item_identifier: "test.txt".to_string(),
            item_type: "test_file".to_string(),
            expected_hash: "abc123".to_string(),
            actual_hash: "def456".to_string(),
            size_bytes: 100,
            abort: true,
            abort_reason: Some("Test".to_string()),
            allow_continuation: false,
        };

        handler.on_hash_mismatch(&mut args);
        assert!(!args.abort);
        assert!(args.allow_continuation);
    }
}
