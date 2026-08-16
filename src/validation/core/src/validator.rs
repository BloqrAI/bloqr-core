//! Main validator combining all validation features.

use std::path::Path;

use crate::config::ValidationConfig;
use crate::error::Result;
use crate::hash::{verify_and_update, HashDatabase};
use crate::syntax::{validate_syntax, SyntaxValidationResult};
use crate::url_security::{validate_url, UrlValidationResult};

/// Main validator for filter lists.
pub struct Validator {
    config: ValidationConfig,
    hash_db: HashDatabase,
}

impl Validator {
    /// Create a new validator with the specified configuration.
    #[must_use]
    pub fn new(config: ValidationConfig) -> Self {
        let hash_db =
            HashDatabase::load(&config.hash_verification.hash_database_path).unwrap_or_default();

        Self { config, hash_db }
    }

    /// Validate a local file.
    ///
    /// Performs:
    /// - Syntax validation
    /// - Hash verification (at-rest)
    ///
    /// # Errors
    ///
    /// Returns an error if validation fails in strict mode.
    pub fn validate_local_file<P: AsRef<Path>>(
        &mut self,
        path: P,
    ) -> Result<SyntaxValidationResult> {
        let path = path.as_ref();

        // Syntax validation
        let syntax_result = validate_syntax(path)?;

        // Hash verification
        let strict = matches!(
            self.config.hash_verification.mode,
            crate::config::VerificationMode::Strict
        );

        verify_and_update(path, &mut self.hash_db, strict)?;

        // Save updated hash database
        self.hash_db
            .save(&self.config.hash_verification.hash_database_path)?;

        Ok(syntax_result)
    }

    /// Validate a remote URL.
    ///
    /// Performs:
    /// - URL security validation
    /// - HTTPS enforcement
    /// - Content validation
    /// - Hash verification (in-flight)
    ///
    /// # Errors
    ///
    /// Returns an error if validation fails.
    pub fn validate_remote_url(
        &self,
        url: &str,
        expected_hash: Option<&str>,
    ) -> Result<UrlValidationResult> {
        validate_url(url, expected_hash)
    }

    /// Get the hash database.
    #[must_use]
    pub fn hash_database(&self) -> &HashDatabase {
        &self.hash_db
    }

    /// Get the configuration.
    #[must_use]
    pub const fn config(&self) -> &ValidationConfig {
        &self.config
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;
    use tempfile::{NamedTempFile, TempDir};

    #[test]
    fn test_validator_new() {
        let config = ValidationConfig::default();
        let validator = Validator::new(config);
        assert!(validator.hash_database().is_empty());
    }

    #[test]
    fn test_validate_local_file() {
        let dir = TempDir::new().unwrap();
        let hash_db_path = dir.path().join(".hashes.json");

        let mut config = ValidationConfig::default();
        config.hash_verification.hash_database_path = hash_db_path.display().to_string();

        let mut validator = Validator::new(config);

        // Create test file
        let mut file = NamedTempFile::new().unwrap();
        writeln!(file, "||example.com^").unwrap();
        writeln!(file, "@@||allowed.com").unwrap();
        file.flush().unwrap();

        let result = validator.validate_local_file(file.path()).unwrap();
        assert!(result.is_valid);
        assert!(result.valid_rules >= 2);
    }

    #[test]
    fn test_validate_remote_url_http_rejected() {
        let config = ValidationConfig::default();
        let validator = Validator::new(config);

        let result = validator
            .validate_remote_url("http://insecure.example.com/list.txt", None)
            .unwrap();
        assert!(!result.is_valid);
    }
}
