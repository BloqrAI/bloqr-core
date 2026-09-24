//! JSON Schema (draft-07) validation for parsed configuration documents.
//!
//! Validates against the same canonical schema (`schemas/compiler-config.schema.json`
//! at the repo root, bundled here via `include_str!` as
//! `schemas/compiler-config.schema.json` so the published `bloqr-compiler-core` crate
//! carries its own copy) that the .NET compiler's `CompilerConfigJsonSchemaValidator`,
//! the TypeScript compiler's `assertJsonSchemaValidConfiguration`, and the Python
//! compiler's `assert_json_schema_valid_configuration` validate against - see issue
//! #518 (follow-up to #502). Kept in sync with the repo-root schema by a test that
//! diffs the two files.

use std::sync::LazyLock;

use crate::error::CompilerError;

const SCHEMA_JSON: &str = include_str!("../schemas/compiler-config.schema.json");

static VALIDATOR: LazyLock<jsonschema::Validator> = LazyLock::new(|| {
    let schema: serde_json::Value = serde_json::from_str(SCHEMA_JSON)
        .expect("bundled compiler-config.schema.json is valid JSON");
    // `format` keywords (e.g. `homepage`'s `format: "uri"`) are NOT validated by this crate
    // unless `should_validate_formats(true)` is set explicitly - without it, `validator_for`
    // silently accepts any string regardless of the schema's `format` constraint.
    jsonschema::options()
        .should_validate_formats(true)
        .build(&schema)
        .expect("bundled compiler-config.schema.json is a valid JSON Schema")
});

/// Validates a parsed configuration document against the canonical JSON Schema.
///
/// This is deliberately independent of, and runs ahead of, [`crate::config::CompilerConfig::validate`]:
/// it catches structural/type/enum errors against the schema documented in
/// `docs/configuration-reference.md` and shared with every other compiler wrapper, while
/// `CompilerConfig::validate` continues to catch anything specific to this crate - in
/// particular, this OSS engine's narrower transformation set (e.g. it rejects
/// `ConflictDetection`/`RuleOptimizer`, which the canonical schema allows because the
/// TypeScript reference implementation supports them).
///
/// # Errors
///
/// Returns [`CompilerError::ValidationFailed`] with every schema violation if `value`
/// doesn't conform.
pub fn assert_json_schema_valid(value: &serde_json::Value) -> crate::error::Result<()> {
    let errors: Vec<String> = VALIDATOR
        .iter_errors(value)
        .map(|e| format!("{}: {e}", e.instance_path()))
        .collect();

    if errors.is_empty() {
        Ok(())
    } else {
        Err(CompilerError::validation_failed(errors.join("\n")))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;
    use std::path::Path;

    #[test]
    fn bundled_schema_copy_stays_in_sync_with_canonical() {
        // A crates.io package of this crate does not contain the rest of the monorepo (in
        // particular the repo-root schemas/ directory this compares against), so `cargo test`
        // against the published/packaged source must not panic here - only the repo's own CI
        // (running from a full checkout) can meaningfully enforce this drift guard.
        let canonical_path = Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("../../../../schemas/compiler-config.schema.json");
        let Ok(canonical) = std::fs::read_to_string(&canonical_path) else {
            eprintln!(
                "skipping: {canonical_path:?} not present (not running from a full \
                 bloqr-core checkout, e.g. a packaged/published crate)"
            );
            return;
        };
        let bundled = SCHEMA_JSON;
        assert_eq!(
            bundled, canonical,
            "src/compilers/rust/core/schemas/compiler-config.schema.json has drifted from the \
             repo-root schemas/compiler-config.schema.json - copy the canonical file over the \
             bundled one (cp schemas/compiler-config.schema.json \
             src/compilers/rust/core/schemas/compiler-config.schema.json)."
        );
    }

    #[test]
    fn accepts_minimal_valid_configuration() {
        let value = json!({
            "name": "Test",
            "sources": [{"source": "https://example.com/list.txt"}],
        });
        assert!(assert_json_schema_valid(&value).is_ok());
    }

    #[test]
    fn accepts_conflict_detection_and_rule_optimizer_transformations() {
        // Regression coverage: the canonical schema's enum includes these (the TS reference
        // implementation supports them) even though this crate's own CompilerConfig::validate
        // rejects them as unsupported by this engine - both are correct at their own layer.
        let value = json!({
            "name": "Test",
            "sources": [{"source": "https://example.com/list.txt"}],
            "transformations": ["ConflictDetection", "RuleOptimizer"],
        });
        assert!(assert_json_schema_valid(&value).is_ok());
    }

    #[test]
    fn rejects_non_uri_homepage() {
        // Regression coverage: jsonschema (the Rust crate) does not validate `format`
        // keywords unless `should_validate_formats(true)` is set explicitly - without it,
        // this would silently pass.
        let value = json!({
            "name": "Test",
            "homepage": "not a uri",
            "sources": [{"source": "https://example.com/list.txt"}],
        });
        assert!(assert_json_schema_valid(&value).is_err());
    }

    #[test]
    fn accepts_valid_homepage_uri() {
        let value = json!({
            "name": "Test",
            "homepage": "https://github.com/BloqrAI/bloqr-core",
            "sources": [{"source": "https://example.com/list.txt"}],
        });
        assert!(assert_json_schema_valid(&value).is_ok());
    }

    #[test]
    fn rejects_config_missing_required_fields() {
        let value = json!({});
        assert!(assert_json_schema_valid(&value).is_err());
    }

    #[test]
    fn rejects_invalid_transformation_enum_value() {
        let value = json!({
            "name": "Test",
            "sources": [{"source": "https://example.com/list.txt"}],
            "transformations": ["NotARealTransformation"],
        });
        assert!(assert_json_schema_valid(&value).is_err());
    }

    #[test]
    fn rejects_unrecognized_top_level_property() {
        let value = json!({
            "name": "Test",
            "sources": [{"source": "https://example.com/list.txt"}],
            "bogusField": true,
        });
        assert!(assert_json_schema_valid(&value).is_err());
    }

    #[test]
    fn accepts_chunking_extensions_use_browser_and_output_file_name() {
        // Regression coverage: these TypeScript-orchestration-layer fields (added to the
        // canonical schema in #518) must not be rejected when they appear in a shared
        // cross-language config file, even though this crate doesn't model them itself.
        let value = json!({
            "name": "Test",
            "extensions": {"customKey": "customValue"},
            "chunking": {"enabled": true, "strategy": "source", "maxParallel": 4},
            "output": {"fileName": "output.txt", "conflictStrategy": "overwrite"},
            "sources": [{"source": "https://example.com/list.txt", "useBrowser": true}],
        });
        assert!(assert_json_schema_valid(&value).is_ok());
    }
}
