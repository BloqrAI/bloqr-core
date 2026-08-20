//! Event types and handlers for the compilation pipeline.
//!
//! This module provides a zero-trust event system for monitoring and validating
//! each stage of the compilation process. Events can be used to:
//! - Track compilation progress
//! - Implement validation at each stage
//! - Lock local source files during compilation
//! - Monitor performance metrics
//!
//! # Example
//!
//! ```ignore
//! use bloqr_compiler::events::{EventDispatcher, CompilationEventHandler, ValidationEventArgs};
//!
//! struct MyHandler;
//!
//! #[async_trait::async_trait]
//! impl CompilationEventHandler for MyHandler {
//!     async fn on_source_loading(&self, args: &mut SourceLoadingEventArgs) {
//!         println!("Loading source {}/{}", args.source_index + 1, args.total_sources);
//!     }
//! }
//!
//! let mut dispatcher = EventDispatcher::new();
//! dispatcher.add_handler(Box::new(MyHandler));
//! ```

use sha2::{Digest, Sha256};
use std::collections::HashMap;
use std::fs::File;
use std::io::{self, Read};
use std::path::{Path, PathBuf};
use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant, SystemTime};
use uuid::Uuid;

#[cfg(unix)]
use std::os::unix::io::AsRawFd;

// =============================================================================
// Enums
// =============================================================================

/// Severity levels for validation findings.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ValidationSeverity {
    /// Informational message.
    Info,
    /// Warning that doesn't prevent compilation.
    Warning,
    /// Error that prevents compilation.
    Error,
    /// Critical security issue that must block compilation.
    Critical,
}

/// Types of file locks.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum FileLockType {
    /// Read-only lock (shared access, prevents writes).
    Read,
    /// Write lock (exclusive access).
    Write,
}

// =============================================================================
// Validation Types
// =============================================================================

/// A single validation finding.
#[derive(Debug, Clone)]
pub struct ValidationFinding {
    /// Severity of the finding.
    pub severity: ValidationSeverity,
    /// Validation code (e.g., "ZT001" for zero-trust violations).
    pub code: String,
    /// Message describing the finding.
    pub message: String,
    /// Source or location of the finding.
    pub location: Option<String>,
    /// Additional context for the finding.
    pub context: Option<HashMap<String, String>>,
}

impl ValidationFinding {
    /// Create a new validation finding.
    pub fn new(
        severity: ValidationSeverity,
        code: impl Into<String>,
        message: impl Into<String>,
    ) -> Self {
        Self {
            severity,
            code: code.into(),
            message: message.into(),
            location: None,
            context: None,
        }
    }

    /// Add a location to the finding.
    pub fn with_location(mut self, location: impl Into<String>) -> Self {
        self.location = Some(location.into());
        self
    }
}

// =============================================================================
// Event Args
// =============================================================================

/// Base event arguments with timestamp.
#[derive(Debug, Clone)]
pub struct EventTimestamp {
    /// When the event occurred.
    pub timestamp: SystemTime,
}

impl Default for EventTimestamp {
    fn default() -> Self {
        Self {
            timestamp: SystemTime::now(),
        }
    }
}

/// Event arguments for when compilation starts.
#[derive(Debug, Clone, Default)]
pub struct CompilationStartedEventArgs {
    /// Base timestamp.
    pub base: EventTimestamp,
    /// Path to the configuration file.
    pub config_path: Option<PathBuf>,
    /// Whether to cancel compilation.
    pub cancel: bool,
    /// Reason for cancellation.
    pub cancel_reason: Option<String>,
}

/// Event arguments for when configuration is loaded.
#[derive(Debug, Clone, Default)]
pub struct ConfigurationLoadedEventArgs {
    /// Base timestamp.
    pub base: EventTimestamp,
    /// Path to the configuration file.
    pub config_path: PathBuf,
    /// Name from the configuration.
    pub config_name: Option<String>,
    /// Number of sources in the configuration.
    pub source_count: usize,
}

/// Event arguments for validation checkpoints (zero-trust).
#[derive(Debug, Clone, Default)]
pub struct ValidationEventArgs {
    /// Base timestamp.
    pub base: EventTimestamp,
    /// Name of the validation stage.
    pub stage_name: String,
    /// List of validation findings.
    pub findings: Vec<ValidationFinding>,
    /// Duration of validation in milliseconds.
    pub duration_ms: f64,
    /// Number of items validated.
    pub items_validated: usize,
    /// Whether to abort compilation.
    pub abort: bool,
    /// Reason for aborting.
    pub abort_reason: Option<String>,
}

impl ValidationEventArgs {
    /// Check if validation passed (no errors or critical findings).
    pub fn passed(&self) -> bool {
        !self.findings.iter().any(|f| {
            matches!(
                f.severity,
                ValidationSeverity::Error | ValidationSeverity::Critical
            )
        })
    }

    /// Add a validation finding.
    pub fn add_finding(&mut self, finding: ValidationFinding) {
        self.findings.push(finding);
    }

    /// Add an error finding.
    pub fn add_error(&mut self, code: impl Into<String>, message: impl Into<String>) {
        self.findings.push(ValidationFinding::new(
            ValidationSeverity::Error,
            code,
            message,
        ));
    }

    /// Add a warning finding.
    pub fn add_warning(&mut self, code: impl Into<String>, message: impl Into<String>) {
        self.findings.push(ValidationFinding::new(
            ValidationSeverity::Warning,
            code,
            message,
        ));
    }

    /// Add a critical finding and set abort flag.
    pub fn add_critical(&mut self, code: impl Into<String>, message: impl Into<String>) {
        let msg = message.into();
        self.findings.push(ValidationFinding::new(
            ValidationSeverity::Critical,
            code,
            msg.clone(),
        ));
        self.abort = true;
        self.abort_reason = Some(msg);
    }
}

/// Event arguments for when a source is about to be loaded.
#[derive(Debug, Clone, Default)]
pub struct SourceLoadingEventArgs {
    /// Base timestamp.
    pub base: EventTimestamp,
    /// Index of this source in the source list.
    pub source_index: usize,
    /// Total number of sources.
    pub total_sources: usize,
    /// Source URL or path.
    pub source_url: String,
    /// Source name.
    pub source_name: Option<String>,
    /// Whether this is a local file.
    pub is_local_file: bool,
    /// Whether to skip this source.
    pub skip: bool,
    /// Reason for skipping.
    pub skip_reason: Option<String>,
}

/// Event arguments for when a source has been loaded.
#[derive(Debug, Clone, Default)]
pub struct SourceLoadedEventArgs {
    /// Base timestamp.
    pub base: EventTimestamp,
    /// Index of this source.
    pub source_index: usize,
    /// Total number of sources.
    pub total_sources: usize,
    /// Source URL or path.
    pub source_url: String,
    /// Source name.
    pub source_name: Option<String>,
    /// Whether loading was successful.
    pub success: bool,
    /// Error message if loading failed.
    pub error_message: Option<String>,
    /// Size of content in bytes.
    pub content_size_bytes: u64,
    /// Estimated number of rules.
    pub estimated_rule_count: usize,
    /// Load duration in milliseconds.
    pub load_duration_ms: f64,
    /// SHA-256 hash of the content.
    pub content_hash: Option<String>,
}

/// Event arguments for when a file lock is acquired.
#[derive(Debug, Clone)]
pub struct FileLockAcquiredEventArgs {
    /// Base timestamp.
    pub base: EventTimestamp,
    /// Path to the locked file.
    pub file_path: PathBuf,
    /// Type of lock.
    pub lock_type: FileLockType,
    /// Lock identifier.
    pub lock_id: String,
    /// Content hash for integrity verification.
    pub content_hash: Option<String>,
}

impl Default for FileLockAcquiredEventArgs {
    fn default() -> Self {
        Self {
            base: EventTimestamp::default(),
            file_path: PathBuf::new(),
            lock_type: FileLockType::Read,
            lock_id: String::new(),
            content_hash: None,
        }
    }
}

/// Event arguments for when a file lock is released.
#[derive(Debug, Clone, Default)]
pub struct FileLockReleasedEventArgs {
    /// Base timestamp.
    pub base: EventTimestamp,
    /// Path to the unlocked file.
    pub file_path: PathBuf,
    /// Lock identifier.
    pub lock_id: String,
    /// Duration the lock was held in milliseconds.
    pub lock_duration_ms: f64,
    /// Whether the file was modified.
    pub was_modified: bool,
    /// Content hash before lock.
    pub hash_before: Option<String>,
    /// Content hash after release.
    pub hash_after: Option<String>,
}

/// Event arguments for when a file lock fails.
#[derive(Debug, Clone)]
pub struct FileLockFailedEventArgs {
    /// Base timestamp.
    pub base: EventTimestamp,
    /// Path to the file.
    pub file_path: PathBuf,
    /// Type of lock attempted.
    pub lock_type: FileLockType,
    /// Reason for failure.
    pub reason: String,
    /// Whether to continue without lock.
    pub continue_without_lock: bool,
    /// Error message.
    pub error_message: Option<String>,
}

impl Default for FileLockFailedEventArgs {
    fn default() -> Self {
        Self {
            base: EventTimestamp::default(),
            file_path: PathBuf::new(),
            lock_type: FileLockType::Read,
            reason: String::new(),
            continue_without_lock: false,
            error_message: None,
        }
    }
}

/// Event arguments for when a chunk starts processing.
#[derive(Debug, Clone, Default)]
pub struct ChunkStartedEventArgs {
    /// Base timestamp.
    pub base: EventTimestamp,
    /// Chunk index.
    pub chunk_index: usize,
    /// Total number of chunks.
    pub total_chunks: usize,
    /// Number of sources in this chunk.
    pub source_count: usize,
    /// Estimated number of rules.
    pub estimated_rules: usize,
    /// Whether to skip this chunk.
    pub skip: bool,
    /// Reason for skipping.
    pub skip_reason: Option<String>,
}

/// Event arguments for when a chunk completes processing.
#[derive(Debug, Clone, Default)]
pub struct ChunkCompletedEventArgs {
    /// Base timestamp.
    pub base: EventTimestamp,
    /// Chunk index.
    pub chunk_index: usize,
    /// Total number of chunks.
    pub total_chunks: usize,
    /// Whether processing was successful.
    pub success: bool,
    /// Error message if failed.
    pub error_message: Option<String>,
    /// Number of rules in the result.
    pub rule_count: usize,
    /// Processing duration in milliseconds.
    pub duration_ms: f64,
}

/// Event arguments for when chunks are about to be merged.
#[derive(Debug, Clone, Default)]
pub struct ChunksMergingEventArgs {
    /// Base timestamp.
    pub base: EventTimestamp,
    /// Number of chunks to merge.
    pub chunk_count: usize,
    /// Total rules before merge.
    pub total_rules_before_merge: usize,
}

/// Event arguments for when chunks have been merged.
#[derive(Debug, Clone, Default)]
pub struct ChunksMergedEventArgs {
    /// Base timestamp.
    pub base: EventTimestamp,
    /// Number of chunks merged.
    pub chunk_count: usize,
    /// Total rules before merge.
    pub total_rules_before_merge: usize,
    /// Final rule count after deduplication.
    pub final_rule_count: usize,
    /// Number of duplicates removed.
    pub duplicates_removed: usize,
    /// Merge duration in milliseconds.
    pub duration_ms: f64,
}

/// Event arguments for when compilation completes successfully.
#[derive(Debug, Clone, Default)]
pub struct CompilationCompletedEventArgs {
    /// Base timestamp.
    pub base: EventTimestamp,
    /// Number of rules compiled.
    pub rule_count: usize,
    /// Output file path.
    pub output_path: Option<PathBuf>,
    /// Compilation duration in milliseconds.
    pub duration_ms: f64,
    /// Content hash.
    pub content_hash: Option<String>,
}

/// Event arguments for when compilation fails.
#[derive(Debug, Clone, Default)]
pub struct CompilationErrorEventArgs {
    /// Base timestamp.
    pub base: EventTimestamp,
    /// Error message.
    pub error_message: String,
    /// Error code.
    pub error_code: Option<String>,
    /// Whether the error was handled.
    pub handled: bool,
}

// =============================================================================
// Hash Verification Events
// =============================================================================

/// Event arguments for when a hash is computed.
#[derive(Debug, Clone, Default)]
pub struct HashComputedEventArgs {
    /// Base timestamp.
    pub base: EventTimestamp,
    /// Path or identifier for the item being hashed.
    pub item_identifier: String,
    /// Type of item (e.g., "input_file", "output_file", "downloaded_source").
    pub item_type: String,
    /// Computed SHA-384 hash (96 hex characters).
    pub hash: String,
    /// Size of the item in bytes.
    pub size_bytes: u64,
    /// Whether this is for verification purposes.
    pub is_verification: bool,
}

/// Event arguments for when a hash is verified successfully.
#[derive(Debug, Clone, Default)]
pub struct HashVerifiedEventArgs {
    /// Base timestamp.
    pub base: EventTimestamp,
    /// Path or identifier for the item.
    pub item_identifier: String,
    /// Type of item (e.g., "input_file", "output_file", "downloaded_source").
    pub item_type: String,
    /// Expected hash.
    pub expected_hash: String,
    /// Actual hash.
    pub actual_hash: String,
    /// Size of the item in bytes.
    pub size_bytes: u64,
    /// Duration of hash computation in milliseconds.
    pub computation_duration_ms: f64,
}

/// Event arguments for when a hash verification fails.
#[derive(Debug, Clone, Default)]
pub struct HashMismatchEventArgs {
    /// Base timestamp.
    pub base: EventTimestamp,
    /// Path or identifier for the item.
    pub item_identifier: String,
    /// Type of item (e.g., "input_file", "output_file", "downloaded_source").
    pub item_type: String,
    /// Expected hash.
    pub expected_hash: String,
    /// Actual hash.
    pub actual_hash: String,
    /// Size of the item in bytes.
    pub size_bytes: u64,
    /// Whether to abort compilation.
    pub abort: bool,
    /// Reason for aborting (if abort is true).
    pub abort_reason: Option<String>,
    /// Whether the handler allowed continuation despite mismatch.
    pub allow_continuation: bool,
}

// =============================================================================
// Event Handler Trait
// =============================================================================

/// Trait for handling compilation events.
///
/// Implement this trait to receive notifications during compilation.
/// All methods have default no-op implementations.
#[allow(unused_variables)]
pub trait CompilationEventHandler: Send + Sync {
    /// Called when compilation is about to start.
    fn on_compilation_starting(&self, args: &mut CompilationStartedEventArgs) {}

    /// Called when configuration has been loaded.
    fn on_configuration_loaded(&self, args: &ConfigurationLoadedEventArgs) {}

    /// Called at validation checkpoints.
    fn on_validation(&self, args: &mut ValidationEventArgs) {}

    /// Called when a source is about to be loaded.
    fn on_source_loading(&self, args: &mut SourceLoadingEventArgs) {}

    /// Called when a source has been loaded.
    fn on_source_loaded(&self, args: &SourceLoadedEventArgs) {}

    /// Called when a file lock is acquired.
    fn on_file_lock_acquired(&self, args: &FileLockAcquiredEventArgs) {}

    /// Called when a file lock is released.
    fn on_file_lock_released(&self, args: &FileLockReleasedEventArgs) {}

    /// Called when a file lock fails.
    fn on_file_lock_failed(&self, args: &mut FileLockFailedEventArgs) {}

    /// Called when a chunk starts processing.
    fn on_chunk_started(&self, args: &mut ChunkStartedEventArgs) {}

    /// Called when a chunk completes processing.
    fn on_chunk_completed(&self, args: &ChunkCompletedEventArgs) {}

    /// Called when chunks are about to be merged.
    fn on_chunks_merging(&self, args: &ChunksMergingEventArgs) {}

    /// Called when chunks have been merged.
    fn on_chunks_merged(&self, args: &ChunksMergedEventArgs) {}

    /// Called when compilation completes successfully.
    fn on_compilation_completed(&self, args: &CompilationCompletedEventArgs) {}

    /// Called when compilation fails.
    fn on_compilation_error(&self, args: &mut CompilationErrorEventArgs) {}

    /// Called when a hash is computed for any item.
    fn on_hash_computed(&self, args: &HashComputedEventArgs) {}

    /// Called when a hash is verified successfully.
    fn on_hash_verified(&self, args: &HashVerifiedEventArgs) {}

    /// Called when a hash verification fails.
    fn on_hash_mismatch(&self, args: &mut HashMismatchEventArgs) {}
}

// =============================================================================
// Event Dispatcher
// =============================================================================

/// Dispatches compilation events to registered handlers.
pub struct EventDispatcher {
    handlers: Vec<Box<dyn CompilationEventHandler>>,
}

impl Default for EventDispatcher {
    fn default() -> Self {
        Self::new()
    }
}

impl EventDispatcher {
    /// Create a new event dispatcher.
    pub fn new() -> Self {
        Self {
            handlers: Vec::new(),
        }
    }

    /// Add an event handler.
    pub fn add_handler(&mut self, handler: Box<dyn CompilationEventHandler>) {
        self.handlers.push(handler);
    }

    /// Get the number of registered handlers.
    pub fn handler_count(&self) -> usize {
        self.handlers.len()
    }

    /// Raise the compilation starting event.
    pub fn raise_compilation_starting(&self, args: &mut CompilationStartedEventArgs) {
        tracing::debug!(
            "Raising CompilationStarting event to {} handlers",
            self.handlers.len()
        );
        for handler in &self.handlers {
            handler.on_compilation_starting(args);
            if args.cancel {
                tracing::info!("Compilation cancelled: {:?}", args.cancel_reason);
                break;
            }
        }
    }

    /// Raise the configuration loaded event.
    pub fn raise_configuration_loaded(&self, args: &ConfigurationLoadedEventArgs) {
        tracing::debug!(
            "Raising ConfigurationLoaded event to {} handlers",
            self.handlers.len()
        );
        for handler in &self.handlers {
            handler.on_configuration_loaded(args);
        }
    }

    /// Raise a validation checkpoint event.
    pub fn raise_validation(&self, args: &mut ValidationEventArgs) {
        tracing::debug!(
            "Raising Validation event ({}) to {} handlers",
            args.stage_name,
            self.handlers.len()
        );
        for handler in &self.handlers {
            handler.on_validation(args);
            if args.abort {
                tracing::warn!(
                    "Validation aborted at stage {}: {:?}",
                    args.stage_name,
                    args.abort_reason
                );
                break;
            }
        }
    }

    /// Raise the source loading event.
    pub fn raise_source_loading(&self, args: &mut SourceLoadingEventArgs) {
        tracing::debug!(
            "Raising SourceLoading event ({}/{}) to {} handlers",
            args.source_index + 1,
            args.total_sources,
            self.handlers.len()
        );
        for handler in &self.handlers {
            handler.on_source_loading(args);
            if args.skip {
                tracing::info!(
                    "Source {} skipped: {:?}",
                    args.source_index,
                    args.skip_reason
                );
                break;
            }
        }
    }

    /// Raise the source loaded event.
    pub fn raise_source_loaded(&self, args: &SourceLoadedEventArgs) {
        tracing::debug!(
            "Raising SourceLoaded event ({}/{}, success: {}) to {} handlers",
            args.source_index + 1,
            args.total_sources,
            args.success,
            self.handlers.len()
        );
        for handler in &self.handlers {
            handler.on_source_loaded(args);
        }
    }

    /// Raise the file lock acquired event.
    pub fn raise_file_lock_acquired(&self, args: &FileLockAcquiredEventArgs) {
        tracing::debug!(
            "Raising FileLockAcquired event ({:?}) to {} handlers",
            args.file_path,
            self.handlers.len()
        );
        for handler in &self.handlers {
            handler.on_file_lock_acquired(args);
        }
    }

    /// Raise the file lock released event.
    pub fn raise_file_lock_released(&self, args: &FileLockReleasedEventArgs) {
        tracing::debug!(
            "Raising FileLockReleased event ({:?}) to {} handlers",
            args.file_path,
            self.handlers.len()
        );
        for handler in &self.handlers {
            handler.on_file_lock_released(args);
        }
    }

    /// Raise the file lock failed event.
    pub fn raise_file_lock_failed(&self, args: &mut FileLockFailedEventArgs) {
        tracing::debug!(
            "Raising FileLockFailed event ({:?}) to {} handlers",
            args.file_path,
            self.handlers.len()
        );
        for handler in &self.handlers {
            handler.on_file_lock_failed(args);
        }
    }

    /// Raise the chunk started event.
    pub fn raise_chunk_started(&self, args: &mut ChunkStartedEventArgs) {
        tracing::debug!(
            "Raising ChunkStarted event ({}/{}) to {} handlers",
            args.chunk_index + 1,
            args.total_chunks,
            self.handlers.len()
        );
        for handler in &self.handlers {
            handler.on_chunk_started(args);
            if args.skip {
                tracing::info!("Chunk {} skipped: {:?}", args.chunk_index, args.skip_reason);
                break;
            }
        }
    }

    /// Raise the chunk completed event.
    pub fn raise_chunk_completed(&self, args: &ChunkCompletedEventArgs) {
        tracing::debug!(
            "Raising ChunkCompleted event ({}/{}, success: {}) to {} handlers",
            args.chunk_index + 1,
            args.total_chunks,
            args.success,
            self.handlers.len()
        );
        for handler in &self.handlers {
            handler.on_chunk_completed(args);
        }
    }

    /// Raise the chunks merging event.
    pub fn raise_chunks_merging(&self, args: &ChunksMergingEventArgs) {
        tracing::debug!(
            "Raising ChunksMerging event ({} chunks, {} rules) to {} handlers",
            args.chunk_count,
            args.total_rules_before_merge,
            self.handlers.len()
        );
        for handler in &self.handlers {
            handler.on_chunks_merging(args);
        }
    }

    /// Raise the chunks merged event.
    pub fn raise_chunks_merged(&self, args: &ChunksMergedEventArgs) {
        tracing::debug!(
            "Raising ChunksMerged event ({} rules, {} duplicates removed) to {} handlers",
            args.final_rule_count,
            args.duplicates_removed,
            self.handlers.len()
        );
        for handler in &self.handlers {
            handler.on_chunks_merged(args);
        }
    }

    /// Raise the compilation completed event.
    pub fn raise_compilation_completed(&self, args: &CompilationCompletedEventArgs) {
        tracing::debug!(
            "Raising CompilationCompleted event to {} handlers",
            self.handlers.len()
        );
        for handler in &self.handlers {
            handler.on_compilation_completed(args);
        }
    }

    /// Raise the compilation error event.
    pub fn raise_compilation_error(&self, args: &mut CompilationErrorEventArgs) {
        tracing::debug!(
            "Raising CompilationError event to {} handlers",
            self.handlers.len()
        );
        for handler in &self.handlers {
            handler.on_compilation_error(args);
        }
    }

    /// Raise the hash computed event.
    pub fn raise_hash_computed(&self, args: &HashComputedEventArgs) {
        tracing::debug!(
            "Raising HashComputed event ({}, {}) to {} handlers",
            args.item_type,
            &args.hash[..16.min(args.hash.len())],
            self.handlers.len()
        );
        for handler in &self.handlers {
            handler.on_hash_computed(args);
        }
    }

    /// Raise the hash verified event.
    pub fn raise_hash_verified(&self, args: &HashVerifiedEventArgs) {
        tracing::debug!(
            "Raising HashVerified event ({}, match) to {} handlers",
            args.item_type,
            self.handlers.len()
        );
        for handler in &self.handlers {
            handler.on_hash_verified(args);
        }
    }

    /// Raise the hash mismatch event.
    pub fn raise_hash_mismatch(&self, args: &mut HashMismatchEventArgs) {
        tracing::debug!(
            "Raising HashMismatch event ({}) to {} handlers",
            args.item_type,
            self.handlers.len()
        );
        for handler in &self.handlers {
            handler.on_hash_mismatch(args);
            if args.abort {
                tracing::error!(
                    "Hash mismatch caused abort for {}: {:?}",
                    args.item_identifier,
                    args.abort_reason
                );
                break;
            }
        }
    }
}

// =============================================================================
// File Lock Service
// =============================================================================

/// Represents an active file lock.
#[derive(Debug)]
pub struct FileLockHandle {
    /// Lock identifier.
    pub lock_id: String,
    /// Path to the locked file.
    pub file_path: PathBuf,
    /// Type of lock.
    pub lock_type: FileLockType,
    /// When the lock was acquired.
    pub acquired_at: Instant,
    /// Content hash for integrity verification.
    pub content_hash: Option<String>,
    /// The file handle (kept open to maintain the lock).
    file: Option<File>,
    /// Whether the lock is still active.
    is_active: bool,
}

impl FileLockHandle {
    /// Check if the lock is still active.
    pub fn is_active(&self) -> bool {
        self.is_active
    }

    /// Get the duration the lock has been held.
    pub fn duration(&self) -> Duration {
        self.acquired_at.elapsed()
    }

    /// Release the lock.
    pub fn release(&mut self) {
        if self.is_active {
            self.file = None; // Dropping the file releases the lock
            self.is_active = false;
            tracing::debug!("Lock released on {:?}", self.file_path);
        }
    }
}

impl Drop for FileLockHandle {
    fn drop(&mut self) {
        self.release();
    }
}

/// Service for managing file locks on local source files.
///
/// Implements zero-trust file integrity verification.
pub struct FileLockService {
    active_locks: Arc<Mutex<HashMap<String, PathBuf>>>,
}

impl Default for FileLockService {
    fn default() -> Self {
        Self::new()
    }
}

impl FileLockService {
    /// Create a new file lock service.
    pub fn new() -> Self {
        Self {
            active_locks: Arc::new(Mutex::new(HashMap::new())),
        }
    }

    /// Acquire a read lock on a file.
    pub fn acquire_read_lock(
        &self,
        file_path: impl AsRef<Path>,
        compute_hash: bool,
    ) -> io::Result<FileLockHandle> {
        self.acquire_lock(file_path, FileLockType::Read, compute_hash)
    }

    /// Acquire a write lock on a file.
    pub fn acquire_write_lock(
        &self,
        file_path: impl AsRef<Path>,
        compute_hash: bool,
    ) -> io::Result<FileLockHandle> {
        self.acquire_lock(file_path, FileLockType::Write, compute_hash)
    }

    /// Internal method to acquire a lock.
    fn acquire_lock(
        &self,
        file_path: impl AsRef<Path>,
        lock_type: FileLockType,
        compute_hash: bool,
    ) -> io::Result<FileLockHandle> {
        let full_path = file_path.as_ref().canonicalize()?;
        let lock_id = Uuid::new_v4().to_string();

        tracing::debug!("Acquiring {:?} lock on {:?}", lock_type, full_path);

        // Open the file
        let file = File::open(&full_path)?;

        // Platform-specific locking
        #[cfg(unix)]
        {
            use libc::{flock, LOCK_EX, LOCK_NB, LOCK_SH};
            let fd = file.as_raw_fd();
            let lock_mode = match lock_type {
                FileLockType::Read => LOCK_SH | LOCK_NB,
                FileLockType::Write => LOCK_EX | LOCK_NB,
            };
            let result = unsafe { flock(fd, lock_mode) };
            if result != 0 {
                return Err(io::Error::last_os_error());
            }
        }

        #[cfg(windows)]
        {
            // On Windows, we use a simple approach: the file is kept open
            // which provides basic protection. For true file locking,
            // windows-sys::Win32::Storage::FileSystem::LockFile could be used
            // but requires more complex OVERLAPPED structure handling.
            // The open file handle itself provides some protection.
            let _ = lock_type; // Acknowledge the variable is used
        }

        // Compute hash if requested
        let content_hash = if compute_hash {
            Some(self.compute_hash(&full_path)?)
        } else {
            None
        };

        // Track the lock
        {
            let mut locks = self.active_locks.lock().unwrap();
            locks.insert(lock_id.clone(), full_path.clone());
        }

        tracing::info!(
            "{:?} lock acquired on {:?} (LockId: {}..., Hash: {}...)",
            lock_type,
            full_path,
            &lock_id[..8],
            content_hash.as_ref().map(|h| &h[..16]).unwrap_or("N/A")
        );

        Ok(FileLockHandle {
            lock_id,
            file_path: full_path,
            lock_type,
            acquired_at: Instant::now(),
            content_hash,
            file: Some(file),
            is_active: true,
        })
    }

    /// Try to acquire a read lock without blocking.
    pub fn try_acquire_read_lock(
        &self,
        file_path: impl AsRef<Path>,
        compute_hash: bool,
    ) -> Option<FileLockHandle> {
        self.acquire_read_lock(file_path, compute_hash).ok()
    }

    /// Verify file integrity by comparing hashes.
    pub fn verify_integrity(
        &self,
        file_path: impl AsRef<Path>,
        expected_hash: &str,
    ) -> io::Result<bool> {
        let current_hash = self.compute_hash(file_path)?;
        let matches = current_hash.eq_ignore_ascii_case(expected_hash);
        if !matches {
            tracing::warn!(
                "Integrity check failed: expected {}..., got {}...",
                &expected_hash[..16.min(expected_hash.len())],
                &current_hash[..16]
            );
        }
        Ok(matches)
    }

    /// Compute SHA-256 hash of a file's contents.
    pub fn compute_hash(&self, file_path: impl AsRef<Path>) -> io::Result<String> {
        let mut file = File::open(file_path)?;
        let mut hasher = Sha256::new();
        let mut buffer = [0u8; 8192];

        loop {
            let bytes_read = file.read(&mut buffer)?;
            if bytes_read == 0 {
                break;
            }
            hasher.update(&buffer[..bytes_read]);
        }

        Ok(hex::encode(hasher.finalize()))
    }

    /// Get the number of active locks.
    pub fn active_lock_count(&self) -> usize {
        self.active_locks.lock().unwrap().len()
    }

    /// Release all active locks.
    pub fn release_all_locks(&self) {
        let mut locks = self.active_locks.lock().unwrap();
        tracing::info!("Releasing all {} active locks", locks.len());
        locks.clear();
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_validation_finding() {
        let finding = ValidationFinding::new(ValidationSeverity::Error, "E001", "Test error");
        assert_eq!(finding.severity, ValidationSeverity::Error);
        assert_eq!(finding.code, "E001");
    }

    #[test]
    fn test_validation_event_args_passed() {
        let mut args = ValidationEventArgs::default();
        assert!(args.passed());

        args.add_warning("W001", "Warning");
        assert!(args.passed());

        args.add_error("E001", "Error");
        assert!(!args.passed());
    }

    #[test]
    fn test_event_dispatcher() {
        let dispatcher = EventDispatcher::new();
        assert_eq!(dispatcher.handler_count(), 0);
    }

    #[test]
    fn test_file_lock_service() {
        let service = FileLockService::new();
        assert_eq!(service.active_lock_count(), 0);
    }
}
