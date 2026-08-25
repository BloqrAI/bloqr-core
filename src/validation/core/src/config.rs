//! Configuration types for validation.

use serde::{Deserialize, Serialize};

use crate::syntax::ValidationEngine;

/// Verification mode for hash checking.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Default)]
#[serde(rename_all = "lowercase")]
pub enum VerificationMode {
    /// Strict mode: all sources must have hashes, any mismatch fails.
    Strict,
    /// Warning mode: hash mismatches generate warnings but don't fail.
    #[default]
    Warning,
    /// Disabled: no hash verification.
    Disabled,
}

/// Archiving mode.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Default)]
#[serde(rename_all = "lowercase")]
pub enum ArchivingMode {
    /// Automatic archiving after compilation.
    #[default]
    Automatic,
    /// Interactive: prompt user before archiving.
    Interactive,
    /// Disabled: no archiving.
    Disabled,
}

/// File conflict resolution strategy.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Default)]
#[serde(rename_all = "lowercase")]
pub enum ConflictStrategy {
    /// Rename file with incrementing number.
    #[default]
    Rename,
    /// Overwrite existing file.
    Overwrite,
    /// Error if file exists.
    Error,
}

/// Hash verification configuration.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(default)]
pub struct HashVerificationConfig {
    /// Verification mode.
    pub mode: VerificationMode,
    /// Require hashes for all remote sources.
    pub require_hashes_for_remote: bool,
    /// Fail compilation on hash mismatch.
    pub fail_on_mismatch: bool,
    /// Path to hash database file.
    pub hash_database_path: String,
}

impl Default for HashVerificationConfig {
    fn default() -> Self {
        Self {
            mode: VerificationMode::default(),
            require_hashes_for_remote: false,
            fail_on_mismatch: false,
            hash_database_path: "data/input/.hashes.json".to_string(),
        }
    }
}

/// Archiving configuration.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(default)]
pub struct ArchivingConfig {
    /// Enable archiving.
    pub enabled: bool,
    /// Archiving mode.
    pub mode: ArchivingMode,
    /// Retention period in days.
    pub retention_days: u32,
    /// Archive directory path.
    pub archive_path: String,
}

impl Default for ArchivingConfig {
    fn default() -> Self {
        Self {
            enabled: true,
            mode: ArchivingMode::default(),
            retention_days: 90,
            archive_path: "data/archive".to_string(),
        }
    }
}

/// Output configuration.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(default)]
pub struct OutputConfig {
    /// Output file path.
    pub path: String,
    /// File conflict strategy.
    pub conflict_strategy: ConflictStrategy,
}

impl Default for OutputConfig {
    fn default() -> Self {
        Self {
            path: "data/output/adguard_user_filter.txt".to_string(),
            conflict_strategy: ConflictStrategy::default(),
        }
    }
}

/// Main validation configuration.
#[derive(Debug, Clone, Serialize, Deserialize, Default)]
#[serde(default)]
pub struct ValidationConfig {
    /// Hash verification settings.
    pub hash_verification: HashVerificationConfig,
    /// Archiving settings.
    pub archiving: ArchivingConfig,
    /// Output settings.
    pub output: OutputConfig,
    /// Which syntax grammar [`Validator::validate_local_file`](crate::validator::Validator::validate_local_file)
    /// validates against. Defaults to [`ValidationEngine::Dns`] - existing callers that
    /// don't set this see byte-identical behavior to before browser-engine support
    /// existed. Reaches the FFI surface via the JSON config passed to
    /// `bloqr_validator_new` (see `src/ffi.rs`).
    pub engine: ValidationEngine,
}

impl ValidationConfig {
    /// Create a new configuration with default values.
    #[must_use]
    pub fn new() -> Self {
        Self::default()
    }

    /// Set verification mode.
    #[must_use]
    pub fn with_verification_mode(mut self, mode: VerificationMode) -> Self {
        self.hash_verification.mode = mode;
        self
    }

    /// Set archiving enabled.
    #[must_use]
    pub fn with_archiving(mut self, enabled: bool) -> Self {
        self.archiving.enabled = enabled;
        self
    }

    /// Set output path.
    #[must_use]
    pub fn with_output_path(mut self, path: impl Into<String>) -> Self {
        self.output.path = path.into();
        self
    }

    /// Set the syntax-validation engine (DNS or browser grammar).
    #[must_use]
    pub const fn with_engine(mut self, engine: ValidationEngine) -> Self {
        self.engine = engine;
        self
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_default_verification_mode() {
        assert_eq!(VerificationMode::default(), VerificationMode::Warning);
    }

    #[test]
    fn test_config_builder() {
        let config = ValidationConfig::new()
            .with_verification_mode(VerificationMode::Strict)
            .with_archiving(false)
            .with_output_path("custom/output.txt");

        assert_eq!(config.hash_verification.mode, VerificationMode::Strict);
        assert!(!config.archiving.enabled);
        assert_eq!(config.output.path, "custom/output.txt");
    }

    #[test]
    fn test_serialization() {
        let config = ValidationConfig::default();
        let json = serde_json::to_string(&config).unwrap();
        let deserialized: ValidationConfig = serde_json::from_str(&json).unwrap();
        assert_eq!(
            config.hash_verification.mode,
            deserialized.hash_verification.mode
        );
    }
}
