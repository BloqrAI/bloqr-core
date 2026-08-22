//! Error types for the rules compiler.
//!
//! This module provides a comprehensive error type hierarchy for all operations
//! in the rules compiler, with detailed context and helpful error messages.

use std::path::PathBuf;
use thiserror::Error;

/// Errors that can occur during compilation operations.
#[derive(Error, Debug)]
#[non_exhaustive]
pub enum CompilerError {
    /// Configuration file was not found at the specified path.
    #[error("configuration file not found: {path}")]
    ConfigNotFound {
        /// The path that was searched.
        path: PathBuf,
    },

    /// The configuration file extension is not recognized.
    #[error(
        "unknown configuration file extension '.{extension}' (expected: .json, .yaml, .yml, .toml)"
    )]
    UnknownExtension {
        /// The unrecognized extension.
        extension: String,
    },

    /// Failed to parse JSON configuration.
    #[error("failed to parse JSON configuration: {source}")]
    JsonParse {
        #[source]
        source: serde_json::Error,
    },

    /// Failed to parse YAML configuration.
    #[error("failed to parse YAML configuration: {source}")]
    YamlParse {
        #[source]
        source: serde_yaml::Error,
    },

    /// Failed to parse TOML configuration.
    #[error("failed to parse TOML configuration: {source}")]
    TomlParse {
        #[source]
        source: toml::de::Error,
    },

    /// Configuration validation failed.
    #[error("configuration validation failed: {message}")]
    ValidationFailed {
        /// Description of the validation error.
        message: String,
    },

    /// File system operation failed.
    #[error("file system error: {context}")]
    FileSystem {
        /// Context describing the operation that failed.
        context: String,
        #[source]
        source: std::io::Error,
    },

    /// The hostlist-compiler tool was not found.
    #[error(
        "hostlist-compiler not found (install with: npm install -g @adguard/hostlist-compiler)"
    )]
    CompilerNotFound,

    /// Compilation process failed.
    #[error("compilation failed: {message}")]
    CompilationFailed {
        /// Description of the failure.
        message: String,
        /// Exit code from the compiler process.
        exit_code: Option<i32>,
        /// Standard error output from the compiler.
        stderr: Option<String>,
    },

    /// Output file was not created after compilation.
    #[error("compilation completed but output file was not created at: {path}")]
    OutputNotCreated {
        /// The expected output path.
        path: PathBuf,
    },

    /// Failed to copy output to rules directory.
    #[error("failed to copy output to rules directory: {context}")]
    CopyFailed {
        /// Context describing the copy operation.
        context: String,
        #[source]
        source: std::io::Error,
    },

    /// Process execution failed.
    #[error("failed to execute process '{command}': {source}")]
    ProcessExecution {
        /// The command that failed.
        command: String,
        #[source]
        source: std::io::Error,
    },

    /// Hash verification failed.
    #[error("hash mismatch for {path}: expected {expected}, got {actual}")]
    HashMismatch {
        /// The path to the file with mismatched hash.
        path: String,
        /// The expected hash value.
        expected: String,
        /// The actual hash value.
        actual: String,
    },

    /// Generic I/O error.
    #[error("I/O error: {0}")]
    Io(#[from] std::io::Error),
}

impl CompilerError {
    /// Create a new `ConfigNotFound` error.
    #[must_use]
    pub fn config_not_found(path: impl Into<PathBuf>) -> Self {
        Self::ConfigNotFound { path: path.into() }
    }

    /// Create a new `UnknownExtension` error.
    #[must_use]
    pub fn unknown_extension(extension: impl Into<String>) -> Self {
        Self::UnknownExtension {
            extension: extension.into(),
        }
    }

    /// Create a new `ValidationFailed` error.
    #[must_use]
    pub fn validation_failed(message: impl Into<String>) -> Self {
        Self::ValidationFailed {
            message: message.into(),
        }
    }

    /// Create a new `FileSystem` error.
    #[must_use]
    pub fn file_system(context: impl Into<String>, source: std::io::Error) -> Self {
        Self::FileSystem {
            context: context.into(),
            source,
        }
    }

    /// Create a new `CompilationFailed` error.
    #[must_use]
    pub fn compilation_failed(
        message: impl Into<String>,
        exit_code: Option<i32>,
        stderr: Option<String>,
    ) -> Self {
        Self::CompilationFailed {
            message: message.into(),
            exit_code,
            stderr,
        }
    }

    /// Create a new `OutputNotCreated` error.
    #[must_use]
    pub fn output_not_created(path: impl Into<PathBuf>) -> Self {
        Self::OutputNotCreated { path: path.into() }
    }

    /// Create a new `CopyFailed` error.
    #[must_use]
    pub fn copy_failed(context: impl Into<String>, source: std::io::Error) -> Self {
        Self::CopyFailed {
            context: context.into(),
            source,
        }
    }

    /// Create a new `ProcessExecution` error.
    #[must_use]
    pub fn process_execution(command: impl Into<String>, source: std::io::Error) -> Self {
        Self::ProcessExecution {
            command: command.into(),
            source,
        }
    }

    /// Create a new `HashMismatch` error.
    #[must_use]
    pub fn hash_mismatch(
        path: impl Into<String>,
        expected: impl Into<String>,
        actual: impl Into<String>,
    ) -> Self {
        Self::HashMismatch {
            path: path.into(),
            expected: expected.into(),
            actual: actual.into(),
        }
    }

    /// Check if this error is recoverable.
    #[must_use]
    pub const fn is_recoverable(&self) -> bool {
        matches!(
            self,
            Self::ConfigNotFound { .. }
                | Self::UnknownExtension { .. }
                | Self::ValidationFailed { .. }
        )
    }
}

impl From<serde_json::Error> for CompilerError {
    fn from(source: serde_json::Error) -> Self {
        Self::JsonParse { source }
    }
}

impl From<serde_yaml::Error> for CompilerError {
    fn from(source: serde_yaml::Error) -> Self {
        Self::YamlParse { source }
    }
}

impl From<toml::de::Error> for CompilerError {
    fn from(source: toml::de::Error) -> Self {
        Self::TomlParse { source }
    }
}

/// Result type alias for compiler operations.
pub type Result<T> = std::result::Result<T, CompilerError>;

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_config_not_found_display() {
        let err = CompilerError::config_not_found("/path/to/config.json");
        assert!(err.to_string().contains("/path/to/config.json"));
    }

    #[test]
    fn test_unknown_extension_display() {
        let err = CompilerError::unknown_extension("xyz");
        assert!(err.to_string().contains("xyz"));
        assert!(err.to_string().contains(".json"));
    }

    #[test]
    fn test_is_recoverable() {
        assert!(CompilerError::config_not_found("/path").is_recoverable());
        assert!(CompilerError::unknown_extension("xyz").is_recoverable());
        assert!(!CompilerError::CompilerNotFound.is_recoverable());
    }

    #[test]
    fn test_validation_failed() {
        let err = CompilerError::validation_failed("missing required field 'name'");
        assert!(err.to_string().contains("missing required field"));
    }
}
