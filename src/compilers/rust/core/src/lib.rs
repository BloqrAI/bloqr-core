//! # Rules Compiler
//!
//! A Rust API for compiling AdGuard filter rules using `@adguard/hostlist-compiler`.
//!
//! This library provides a high-level interface for:
//! - Reading configuration files in JSON, YAML, and TOML formats
//! - Compiling filter rules from multiple sources
//! - Validating configuration
//! - Computing statistics and hashes
//!
//! ## Quick Start
//!
//! ```no_run
//! use bloqr_compiler::{BloqrCompiler, CompileOptions};
//!
//! // Create a compiler with default options
//! let compiler = BloqrCompiler::new();
//!
//! // Compile rules from a configuration file
//! let result = compiler.compile("config.yaml")?;
//!
//! if result.success {
//!     println!("Compiled {} rules to {}", result.rule_count, result.output_path_str());
//! }
//! # Ok::<(), bloqr_compiler::CompilerError>(())
//! ```
//!
//! ## With Options
//!
//! ```no_run
//! use bloqr_compiler::{BloqrCompiler, CompileOptions, ConfigFormat};
//!
//! let options = CompileOptions::new()
//!     .with_copy_to_rules(true)
//!     .with_debug(true)
//!     .with_validation(true);
//!
//! let compiler = BloqrCompiler::with_options(options);
//! let result = compiler.compile("config.yaml")?;
//! # Ok::<(), bloqr_compiler::CompilerError>(())
//! ```
//!
//! ## Reading Configuration
//!
//! ```no_run
//! use bloqr_compiler::{read_config, ConfigFormat};
//!
//! // Auto-detect format from extension
//! let config = read_config("config.yaml", None)?;
//!
//! // Force specific format
//! let config = read_config("config.txt", Some(ConfigFormat::Json))?;
//!
//! println!("Filter: {} v{}", config.name, config.version);
//! println!("Sources: {}", config.sources.len());
//! # Ok::<(), bloqr_compiler::CompilerError>(())
//! ```

pub mod chunking;
pub mod compiler;
pub mod config;
pub mod error;
pub mod events;

// Re-export main types from config module
pub use config::{
    read_config, to_json, to_toml, to_yaml, CompilerConfig, ConfigFormat, FilterSource, SourceType,
    Transformation,
};

// Re-export main types from compiler module
pub use compiler::{
    compile_rules, compile_rules_with_events, compute_hash, compute_hash_with_events, count_rules,
    verify_hash_with_events, BloqrCompiler, CompileOptions, CompilerResult, PlatformInfo,
    VersionInfo,
};

// Re-export error types
pub use error::{CompilerError, Result};

// Re-export chunking types
pub use chunking::{
    compile_chunks_async, estimate_speedup, merge_chunks, should_enable_chunking,
    split_into_chunks, ChunkMetadata, ChunkedCompilationResult, ChunkingOptions, ChunkingStrategy,
};

// Re-export event types
pub use events::{
    ChunkCompletedEventArgs,
    ChunkStartedEventArgs,
    ChunksMergedEventArgs,
    ChunksMergingEventArgs,
    CompilationCompletedEventArgs,
    CompilationErrorEventArgs,
    // Trait and dispatcher
    CompilationEventHandler,
    // Event args
    CompilationStartedEventArgs,
    ConfigurationLoadedEventArgs,
    EventDispatcher,
    EventTimestamp,
    FileLockAcquiredEventArgs,
    FileLockFailedEventArgs,
    // File locking
    FileLockHandle,
    FileLockReleasedEventArgs,
    FileLockService,
    FileLockType,
    // Hash verification events
    HashComputedEventArgs,
    HashMismatchEventArgs,
    HashVerifiedEventArgs,
    SourceLoadedEventArgs,
    SourceLoadingEventArgs,
    ValidationEventArgs,
    // Types
    ValidationFinding,
    // Enums
    ValidationSeverity,
};

/// Library version from Cargo.toml.
pub const VERSION: &str = env!("CARGO_PKG_VERSION");

/// Library name.
pub const NAME: &str = env!("CARGO_PKG_NAME");

/// Library description.
pub const DESCRIPTION: &str = env!("CARGO_PKG_DESCRIPTION");

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_version_constant() {
        // VERSION comes from CARGO_PKG_VERSION, a compile-time constant clippy can now prove
        // is always non-empty - the meaningful regression check is the format, not emptiness.
        assert!(VERSION.contains('.'));
    }

    #[test]
    fn test_name_constant() {
        assert_eq!(NAME, "bloqr-compiler-core");
    }

    #[test]
    fn test_exports() {
        // Verify all main types are exported
        let _: fn() -> BloqrCompiler = BloqrCompiler::new;
        let _: fn() -> CompileOptions = CompileOptions::new;
        let _: fn() -> VersionInfo = VersionInfo::collect;
        let _: fn() -> PlatformInfo = PlatformInfo::detect;
    }
}
