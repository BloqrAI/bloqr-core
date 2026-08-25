//! # Bloqr Validator Core
//!
//! Part of the [Bloqr](https://github.com/BloqrAI) open-source ad-blocking toolkit
//! ([`bloqr-core`](https://github.com/BloqrAI/bloqr-core)). Centralized validation library for
//! adblock- and hosts-format filter list compilation, with comprehensive security features
//! including hash verification, URL security validation, and syntax checking.
//!
//! This library is designed to be used across multiple language wrappers in `bloqr-core`
//! (TypeScript/[`@bloqr/compiler-core`](https://jsr.io/@bloqr/compiler-core), .NET, Python, Rust)
//! through native bindings, FFI, or WebAssembly. See the [`ffi`] module for the C-compatible
//! FFI surface (`extern "C"` functions over an opaque-handle + JSON-string boundary), suitable
//! for .NET P/Invoke or any other FFI consumer.
//!
//! ## Features
//!
//! - **At-Rest Hash Verification**: SHA-384 hashing for local files with database management
//! - **In-Flight Hash Verification**: SHA-384 verification for downloaded files (prevents MITM)
//! - **URL Security Validation**: HTTPS enforcement, domain validation, content verification
//! - **Syntax Validation**: Automatic linting for adblock and hosts file formats
//! - **File Conflict Handling**: Automatic renaming, overwrite, or error strategies
//! - **Archiving**: Timestamped archiving with manifest tracking and retention policies
//!
//! ## Quick Start
//!
//! ```no_run
//! use bloqr_validator::{Validator, ValidationConfig, VerificationMode};
//!
//! # fn main() -> Result<(), Box<dyn std::error::Error>> {
//! let config = ValidationConfig::default()
//!     .with_verification_mode(VerificationMode::Strict);
//!
//! let mut validator = Validator::new(config);
//!
//! // Validate a local file
//! let _result = validator.validate_local_file("data/input/custom-rules.txt")?;
//!
//! // Validate a remote URL
//! let _result = validator.validate_remote_url("https://example.com/list.txt", None)?;
//! # Ok(())
//! # }
//! ```

pub mod archive;
pub mod config;
pub mod error;
pub mod ffi;
pub mod file_conflict;
pub mod hash;
pub mod runtime_enforcement;
pub mod syntax;
pub mod url_security;
pub mod validator;

// Re-export main types
pub use config::{
    ArchivingConfig, ArchivingMode, ConflictStrategy, HashVerificationConfig, OutputConfig,
    ValidationConfig, VerificationMode,
};

pub use archive::{create_archive, ArchiveManifest};
pub use error::{Result, ValidationError};
pub use file_conflict::{resolve_conflict, FileConflictResolver};
pub use hash::{compute_file_hash, verify_file_hash, HashDatabase, HashEntry};
/// Deprecated prototype API — see [`runtime_enforcement`] module docs.
#[allow(deprecated)]
pub use runtime_enforcement::{
    compile_with_validation, verify_compilation_was_validated, CompilationInput,
    CompilationOptions, EnforcedCompilationResult, ValidationMetadata,
};
pub use syntax::{
    validate_syntax, validate_syntax_content_with_engine, validate_syntax_content_with_mode,
    validate_syntax_with_engine, validate_syntax_with_mode, FilterFormat, HostlistValidationMode,
    SyntaxValidationResult, ValidationEngine,
};
pub use url_security::{validate_url, UrlValidationResult};
pub use validator::Validator;

/// Library version.
pub const VERSION: &str = env!("CARGO_PKG_VERSION");

/// Library name.
pub const NAME: &str = env!("CARGO_PKG_NAME");

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    #[allow(clippy::const_is_empty)]
    fn test_version() {
        assert!(!VERSION.is_empty());
    }

    #[test]
    fn test_name() {
        assert_eq!(NAME, "bloqr-validator-core");
    }
}
