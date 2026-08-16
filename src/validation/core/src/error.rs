//! Error types for validation operations.

use std::io;
use thiserror::Error;

/// Result type for validation operations.
pub type Result<T> = std::result::Result<T, ValidationError>;

/// Validation error types.
#[derive(Error, Debug)]
pub enum ValidationError {
    /// Hash mismatch detected.
    #[error("Hash mismatch for {file}: expected {expected}, got {actual}")]
    HashMismatch {
        file: String,
        expected: String,
        actual: String,
    },

    /// URL validation failed.
    #[error("URL validation failed for {url}: {reason}")]
    UrlValidationFailed { url: String, reason: String },

    /// Syntax validation failed.
    #[error("Syntax validation failed for {file}: {reason}")]
    SyntaxValidationFailed { file: String, reason: String },

    /// File system error.
    #[error("File system error: {0}")]
    FileSystem(#[from] io::Error),

    /// HTTP error.
    #[error("HTTP error: {0}")]
    Http(#[from] reqwest::Error),

    /// JSON error.
    #[error("JSON error: {0}")]
    Json(#[from] serde_json::Error),

    /// URL parse error.
    #[error("URL parse error: {0}")]
    UrlParse(#[from] url::ParseError),

    /// Configuration error.
    #[error("Configuration error: {0}")]
    Config(String),

    /// Archive error.
    #[error("Archive error: {0}")]
    Archive(String),

    /// File conflict error.
    #[error("File conflict: {0}")]
    FileConflict(String),

    /// Generic validation error.
    #[error("{0}")]
    Other(String),
}

impl ValidationError {
    /// Create a hash mismatch error.
    pub fn hash_mismatch(
        file: impl Into<String>,
        expected: impl Into<String>,
        actual: impl Into<String>,
    ) -> Self {
        Self::HashMismatch {
            file: file.into(),
            expected: expected.into(),
            actual: actual.into(),
        }
    }

    /// Create a URL validation error.
    pub fn url_validation(url: impl Into<String>, reason: impl Into<String>) -> Self {
        Self::UrlValidationFailed {
            url: url.into(),
            reason: reason.into(),
        }
    }

    /// Create a syntax validation error.
    pub fn syntax_validation(file: impl Into<String>, reason: impl Into<String>) -> Self {
        Self::SyntaxValidationFailed {
            file: file.into(),
            reason: reason.into(),
        }
    }

    /// Create a config error.
    pub fn config(msg: impl Into<String>) -> Self {
        Self::Config(msg.into())
    }

    /// Create an archive error.
    pub fn archive(msg: impl Into<String>) -> Self {
        Self::Archive(msg.into())
    }

    /// Create a file conflict error.
    pub fn file_conflict(msg: impl Into<String>) -> Self {
        Self::FileConflict(msg.into())
    }
}
