"""
Event types and handlers for the compilation pipeline.

This module provides a zero-trust event system for monitoring and validating
each stage of the compilation process. Events can be used to:
- Track compilation progress
- Implement validation at each stage
- Lock local source files during compilation
- Monitor performance metrics

Example:
    >>> from bloqr_compiler.events import EventDispatcher, CompilationEventHandler
    >>>
    >>> class MyHandler(CompilationEventHandler):
    ...     async def on_source_loading(self, args):
    ...         print(f"Loading source {args.source_index + 1}/{args.total_sources}")
    ...
    ...     async def on_validation(self, args):
    ...         if args.stage_name == "configuration":
    ...             # Validate configuration
    ...             if not args.passed:
    ...                 args.abort = True
    ...                 args.abort_reason = "Configuration validation failed"
    >>>
    >>> dispatcher = EventDispatcher()
    >>> dispatcher.add_handler(MyHandler())
"""

from __future__ import annotations

import hashlib
import os
from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
from pathlib import Path
from typing import Any, Callable, Dict, List, Optional, Protocol
import asyncio
import logging
import sys

# Platform-specific file locking
if sys.platform == "win32":
    import msvcrt
    _HAS_FCNTL = False
else:
    import fcntl
    _HAS_FCNTL = True

logger = logging.getLogger(__name__)


# =============================================================================
# Enums
# =============================================================================


class ValidationSeverity(Enum):
    """Severity levels for validation findings."""

    INFO = "info"
    WARNING = "warning"
    ERROR = "error"
    CRITICAL = "critical"


class FileLockType(Enum):
    """Types of file locks."""

    READ = "read"
    WRITE = "write"


# =============================================================================
# Validation Types
# =============================================================================


@dataclass
class ValidationFinding:
    """A single validation finding."""

    severity: ValidationSeverity
    code: str
    message: str
    location: Optional[str] = None
    context: Optional[Dict[str, Any]] = None


# =============================================================================
# Event Args Base
# =============================================================================


@dataclass
class CompilationEventArgs:
    """Base class for all compilation event arguments."""

    timestamp: datetime = field(default_factory=datetime.utcnow)

    # Allow subclasses to add fields without breaking dataclass inheritance
    def __post_init__(self):
        pass


# =============================================================================
# Configuration Events
# =============================================================================


@dataclass
class CompilationStartedEventArgs(CompilationEventArgs):
    """Event arguments for when compilation starts."""

    config_path: Optional[str] = None
    cancel: bool = False
    cancel_reason: Optional[str] = None


@dataclass
class ConfigurationLoadedEventArgs(CompilationEventArgs):
    """Event arguments for when configuration is loaded."""

    config_path: str = ""
    config_name: Optional[str] = None
    source_count: int = 0


# =============================================================================
# Validation Events
# =============================================================================


@dataclass
class ValidationEventArgs(CompilationEventArgs):
    """Event arguments for validation checkpoints (zero-trust)."""

    stage_name: str = ""
    findings: List[ValidationFinding] = field(default_factory=list)
    duration_ms: float = 0.0
    items_validated: int = 0
    abort: bool = False
    abort_reason: Optional[str] = None

    @property
    def passed(self) -> bool:
        """Check if validation passed (no errors or critical findings)."""
        return not any(
            f.severity in (ValidationSeverity.ERROR, ValidationSeverity.CRITICAL)
            for f in self.findings
        )

    def add_finding(
        self,
        severity: ValidationSeverity,
        code: str,
        message: str,
        location: Optional[str] = None,
    ) -> None:
        """Add a validation finding."""
        self.findings.append(ValidationFinding(severity, code, message, location))

    def add_error(
        self, code: str, message: str, location: Optional[str] = None
    ) -> None:
        """Add an error finding."""
        self.add_finding(ValidationSeverity.ERROR, code, message, location)

    def add_warning(
        self, code: str, message: str, location: Optional[str] = None
    ) -> None:
        """Add a warning finding."""
        self.add_finding(ValidationSeverity.WARNING, code, message, location)

    def add_critical(
        self, code: str, message: str, location: Optional[str] = None
    ) -> None:
        """Add a critical finding and set abort flag."""
        self.add_finding(ValidationSeverity.CRITICAL, code, message, location)
        self.abort = True
        self.abort_reason = message


# =============================================================================
# Source Events
# =============================================================================


@dataclass
class SourceLoadingEventArgs(CompilationEventArgs):
    """Event arguments for when a source is about to be loaded."""

    source_index: int = 0
    total_sources: int = 0
    source_url: str = ""
    source_name: Optional[str] = None
    is_local_file: bool = False
    skip: bool = False
    skip_reason: Optional[str] = None


@dataclass
class SourceLoadedEventArgs(CompilationEventArgs):
    """Event arguments for when a source has been loaded."""

    source_index: int = 0
    total_sources: int = 0
    source_url: str = ""
    source_name: Optional[str] = None
    success: bool = False
    error_message: Optional[str] = None
    content_size_bytes: int = 0
    estimated_rule_count: int = 0
    load_duration_ms: float = 0.0
    content_hash: Optional[str] = None


# =============================================================================
# File Lock Events
# =============================================================================


@dataclass
class FileLockAcquiredEventArgs(CompilationEventArgs):
    """Event arguments for when a file lock is acquired."""

    file_path: str = ""
    lock_type: FileLockType = FileLockType.READ
    lock_id: str = ""
    content_hash: Optional[str] = None


@dataclass
class FileLockReleasedEventArgs(CompilationEventArgs):
    """Event arguments for when a file lock is released."""

    file_path: str = ""
    lock_id: str = ""
    lock_duration_ms: float = 0.0
    was_modified: bool = False
    hash_before: Optional[str] = None
    hash_after: Optional[str] = None


@dataclass
class FileLockFailedEventArgs(CompilationEventArgs):
    """Event arguments for when a file lock fails."""

    file_path: str = ""
    lock_type: FileLockType = FileLockType.READ
    reason: str = ""
    continue_without_lock: bool = False
    exception: Optional[Exception] = None


# =============================================================================
# Chunk Events
# =============================================================================


@dataclass
class ChunkStartedEventArgs(CompilationEventArgs):
    """Event arguments for when a chunk starts processing."""

    chunk_index: int = 0
    total_chunks: int = 0
    source_count: int = 0
    estimated_rules: int = 0
    skip: bool = False
    skip_reason: Optional[str] = None


@dataclass
class ChunkCompletedEventArgs(CompilationEventArgs):
    """Event arguments for when a chunk completes processing."""

    chunk_index: int = 0
    total_chunks: int = 0
    success: bool = False
    error_message: Optional[str] = None
    rule_count: int = 0
    duration_ms: float = 0.0


@dataclass
class ChunksMergingEventArgs(CompilationEventArgs):
    """Event arguments for when chunks are about to be merged."""

    chunk_count: int = 0
    total_rules_before_merge: int = 0


@dataclass
class ChunksMergedEventArgs(CompilationEventArgs):
    """Event arguments for when chunks have been merged."""

    chunk_count: int = 0
    total_rules_before_merge: int = 0
    final_rule_count: int = 0
    duplicates_removed: int = 0
    duration_ms: float = 0.0


# =============================================================================
# Hash Verification Events
# =============================================================================
#
# See docs/HASH_VERIFICATION.md. These use SHA-384 (96 hex chars), distinct from
# FileLockService.compute_hash's SHA-256 below - that split is intentional (see the doc's
# "Why file locking uses SHA-256 while this system uses SHA-384" section): file locking hashes
# are a short-lived same-process integrity check, while these events feed a durable,
# cross-run `.hashes.json` trust record that warrants SHA-384's larger margin.


@dataclass
class HashComputedEventArgs(CompilationEventArgs):
    """Event arguments for when a hash is computed for any file (audit trail)."""

    item_identifier: str = ""
    item_type: str = ""
    hash: str = ""
    size_bytes: int = 0
    is_verification: bool = False


@dataclass
class HashVerifiedEventArgs(CompilationEventArgs):
    """Event arguments for when a computed hash matches the recorded expected value."""

    item_identifier: str = ""
    item_type: str = ""
    expected_hash: str = ""
    actual_hash: str = ""
    size_bytes: int = 0
    computation_duration_ms: float = 0.0


@dataclass
class HashMismatchEventArgs(CompilationEventArgs):
    """
    Event arguments for when a computed hash does NOT match the recorded expected value.

    Defaults to aborting; a handler can set allow_continuation=True (and/or abort=False) to
    accept the new hash as the trusted baseline going forward instead of failing compilation.
    """

    item_identifier: str = ""
    item_type: str = ""
    expected_hash: str = ""
    actual_hash: str = ""
    size_bytes: int = 0
    abort: bool = True
    abort_reason: Optional[str] = None
    allow_continuation: bool = False


# =============================================================================
# Completion Events
# =============================================================================


@dataclass
class CompilationCompletedEventArgs(CompilationEventArgs):
    """Event arguments for when compilation completes successfully."""

    rule_count: int = 0
    output_path: Optional[str] = None
    duration_ms: float = 0.0
    content_hash: Optional[str] = None


@dataclass
class CompilationErrorEventArgs(CompilationEventArgs):
    """Event arguments for when compilation fails."""

    error_message: str = ""
    error_code: Optional[str] = None
    exception: Optional[Exception] = None
    handled: bool = False


# =============================================================================
# Event Handler Protocol
# =============================================================================


class CompilationEventHandler(ABC):
    """
    Abstract base class for compilation event handlers.

    Override only the methods you need. All methods have default no-op implementations.
    """

    async def on_compilation_starting(
        self, args: CompilationStartedEventArgs
    ) -> None:
        """Called when compilation is about to start."""
        pass

    async def on_configuration_loaded(
        self, args: ConfigurationLoadedEventArgs
    ) -> None:
        """Called when configuration has been loaded."""
        pass

    async def on_validation(self, args: ValidationEventArgs) -> None:
        """Called at validation checkpoints."""
        pass

    async def on_source_loading(self, args: SourceLoadingEventArgs) -> None:
        """Called when a source is about to be loaded."""
        pass

    async def on_source_loaded(self, args: SourceLoadedEventArgs) -> None:
        """Called when a source has been loaded."""
        pass

    async def on_file_lock_acquired(
        self, args: FileLockAcquiredEventArgs
    ) -> None:
        """Called when a file lock is acquired."""
        pass

    async def on_file_lock_released(
        self, args: FileLockReleasedEventArgs
    ) -> None:
        """Called when a file lock is released."""
        pass

    async def on_file_lock_failed(self, args: FileLockFailedEventArgs) -> None:
        """Called when a file lock fails."""
        pass

    async def on_chunk_started(self, args: ChunkStartedEventArgs) -> None:
        """Called when a chunk starts processing."""
        pass

    async def on_chunk_completed(self, args: ChunkCompletedEventArgs) -> None:
        """Called when a chunk completes processing."""
        pass

    async def on_chunks_merging(self, args: ChunksMergingEventArgs) -> None:
        """Called when chunks are about to be merged."""
        pass

    async def on_chunks_merged(self, args: ChunksMergedEventArgs) -> None:
        """Called when chunks have been merged."""
        pass

    async def on_compilation_completed(
        self, args: CompilationCompletedEventArgs
    ) -> None:
        """Called when compilation completes successfully."""
        pass

    async def on_compilation_error(
        self, args: CompilationErrorEventArgs
    ) -> None:
        """Called when compilation fails."""
        pass

    async def on_hash_computed(self, args: HashComputedEventArgs) -> None:
        """Called whenever a hash is computed for any file."""
        pass

    async def on_hash_verified(self, args: HashVerifiedEventArgs) -> None:
        """Called when a computed hash matches the recorded expected value."""
        pass

    async def on_hash_mismatch(self, args: HashMismatchEventArgs) -> None:
        """Called when a computed hash does not match the recorded expected value."""
        pass


# =============================================================================
# Event Dispatcher
# =============================================================================


class EventDispatcher:
    """
    Dispatches compilation events to registered handlers.

    Supports zero-trust validation with cancellation at each stage.
    """

    def __init__(self) -> None:
        self._handlers: List[CompilationEventHandler] = []

    def add_handler(self, handler: CompilationEventHandler) -> None:
        """Add an event handler."""
        self._handlers.append(handler)

    def remove_handler(self, handler: CompilationEventHandler) -> None:
        """Remove an event handler."""
        self._handlers.remove(handler)

    @property
    def handler_count(self) -> int:
        """Get the number of registered handlers."""
        return len(self._handlers)

    async def raise_compilation_starting(
        self, args: CompilationStartedEventArgs
    ) -> None:
        """Raise the compilation starting event."""
        logger.debug(f"Raising CompilationStarting event to {len(self._handlers)} handlers")
        for handler in self._handlers:
            try:
                await handler.on_compilation_starting(args)
                if args.cancel:
                    logger.info(
                        f"Compilation cancelled by handler {handler.__class__.__name__}: "
                        f"{args.cancel_reason}"
                    )
                    break
            except Exception as e:
                logger.error(
                    f"Error in handler {handler.__class__.__name__} during "
                    f"CompilationStarting: {e}"
                )
                raise

    async def raise_configuration_loaded(
        self, args: ConfigurationLoadedEventArgs
    ) -> None:
        """Raise the configuration loaded event."""
        logger.debug(f"Raising ConfigurationLoaded event to {len(self._handlers)} handlers")
        for handler in self._handlers:
            try:
                await handler.on_configuration_loaded(args)
            except Exception as e:
                logger.error(
                    f"Error in handler {handler.__class__.__name__} during "
                    f"ConfigurationLoaded: {e}"
                )
                raise

    async def raise_validation(self, args: ValidationEventArgs) -> None:
        """Raise a validation checkpoint event."""
        logger.debug(
            f"Raising Validation event ({args.stage_name}) to {len(self._handlers)} handlers"
        )
        for handler in self._handlers:
            try:
                await handler.on_validation(args)
                if args.abort:
                    logger.warning(
                        f"Validation aborted by handler {handler.__class__.__name__} "
                        f"at stage {args.stage_name}: {args.abort_reason}"
                    )
                    break
            except Exception as e:
                logger.error(
                    f"Error in handler {handler.__class__.__name__} during "
                    f"Validation ({args.stage_name}): {e}"
                )
                raise

    async def raise_source_loading(self, args: SourceLoadingEventArgs) -> None:
        """Raise the source loading event."""
        logger.debug(
            f"Raising SourceLoading event ({args.source_index + 1}/{args.total_sources}) "
            f"to {len(self._handlers)} handlers"
        )
        for handler in self._handlers:
            try:
                await handler.on_source_loading(args)
                if args.skip:
                    logger.info(
                        f"Source {args.source_index} skipped by handler "
                        f"{handler.__class__.__name__}: {args.skip_reason}"
                    )
                    break
            except Exception as e:
                logger.error(
                    f"Error in handler {handler.__class__.__name__} during "
                    f"SourceLoading: {e}"
                )
                raise

    async def raise_source_loaded(self, args: SourceLoadedEventArgs) -> None:
        """Raise the source loaded event."""
        logger.debug(
            f"Raising SourceLoaded event ({args.source_index + 1}/{args.total_sources}, "
            f"Success: {args.success}) to {len(self._handlers)} handlers"
        )
        for handler in self._handlers:
            try:
                await handler.on_source_loaded(args)
            except Exception as e:
                logger.error(
                    f"Error in handler {handler.__class__.__name__} during "
                    f"SourceLoaded: {e}"
                )
                # Don't rethrow - source is already loaded

    async def raise_file_lock_acquired(
        self, args: FileLockAcquiredEventArgs
    ) -> None:
        """Raise the file lock acquired event."""
        logger.debug(
            f"Raising FileLockAcquired event ({args.file_path}, {args.lock_type.value}) "
            f"to {len(self._handlers)} handlers"
        )
        for handler in self._handlers:
            try:
                await handler.on_file_lock_acquired(args)
            except Exception as e:
                logger.error(
                    f"Error in handler {handler.__class__.__name__} during "
                    f"FileLockAcquired: {e}"
                )
                # Don't rethrow - lock is already acquired

    async def raise_file_lock_released(
        self, args: FileLockReleasedEventArgs
    ) -> None:
        """Raise the file lock released event."""
        logger.debug(
            f"Raising FileLockReleased event ({args.file_path}) "
            f"to {len(self._handlers)} handlers"
        )
        for handler in self._handlers:
            try:
                await handler.on_file_lock_released(args)
            except Exception as e:
                logger.error(
                    f"Error in handler {handler.__class__.__name__} during "
                    f"FileLockReleased: {e}"
                )
                # Don't rethrow - lock is already released

    async def raise_file_lock_failed(
        self, args: FileLockFailedEventArgs
    ) -> None:
        """Raise the file lock failed event."""
        logger.debug(
            f"Raising FileLockFailed event ({args.file_path}) "
            f"to {len(self._handlers)} handlers"
        )
        for handler in self._handlers:
            try:
                await handler.on_file_lock_failed(args)
            except Exception as e:
                logger.error(
                    f"Error in handler {handler.__class__.__name__} during "
                    f"FileLockFailed: {e}"
                )
                # Don't rethrow - lock already failed

    async def raise_chunk_started(self, args: ChunkStartedEventArgs) -> None:
        """Raise the chunk started event."""
        logger.debug(
            f"Raising ChunkStarted event ({args.chunk_index + 1}/{args.total_chunks}) "
            f"to {len(self._handlers)} handlers"
        )
        for handler in self._handlers:
            try:
                await handler.on_chunk_started(args)
                if args.skip:
                    logger.info(
                        f"Chunk {args.chunk_index} skipped by handler "
                        f"{handler.__class__.__name__}: {args.skip_reason}"
                    )
                    break
            except Exception as e:
                logger.error(
                    f"Error in handler {handler.__class__.__name__} during "
                    f"ChunkStarted: {e}"
                )
                raise

    async def raise_chunk_completed(
        self, args: ChunkCompletedEventArgs
    ) -> None:
        """Raise the chunk completed event."""
        logger.debug(
            f"Raising ChunkCompleted event ({args.chunk_index + 1}/{args.total_chunks}, "
            f"Success: {args.success}) to {len(self._handlers)} handlers"
        )
        for handler in self._handlers:
            try:
                await handler.on_chunk_completed(args)
            except Exception as e:
                logger.error(
                    f"Error in handler {handler.__class__.__name__} during "
                    f"ChunkCompleted: {e}"
                )
                # Don't rethrow - chunk is already completed

    async def raise_chunks_merging(self, args: ChunksMergingEventArgs) -> None:
        """Raise the chunks merging event."""
        logger.debug(
            f"Raising ChunksMerging event ({args.chunk_count} chunks, "
            f"{args.total_rules_before_merge} rules) to {len(self._handlers)} handlers"
        )
        for handler in self._handlers:
            try:
                await handler.on_chunks_merging(args)
            except Exception as e:
                logger.error(
                    f"Error in handler {handler.__class__.__name__} during "
                    f"ChunksMerging: {e}"
                )
                raise

    async def raise_chunks_merged(self, args: ChunksMergedEventArgs) -> None:
        """Raise the chunks merged event."""
        logger.debug(
            f"Raising ChunksMerged event ({args.final_rule_count} rules, "
            f"{args.duplicates_removed} duplicates removed) to {len(self._handlers)} handlers"
        )
        for handler in self._handlers:
            try:
                await handler.on_chunks_merged(args)
            except Exception as e:
                logger.error(
                    f"Error in handler {handler.__class__.__name__} during "
                    f"ChunksMerged: {e}"
                )
                # Don't rethrow - merge is already completed

    async def raise_compilation_completed(
        self, args: CompilationCompletedEventArgs
    ) -> None:
        """Raise the compilation completed event."""
        logger.debug(
            f"Raising CompilationCompleted event to {len(self._handlers)} handlers"
        )
        for handler in self._handlers:
            try:
                await handler.on_compilation_completed(args)
            except Exception as e:
                logger.error(
                    f"Error in handler {handler.__class__.__name__} during "
                    f"CompilationCompleted: {e}"
                )
                # Don't rethrow - compilation already succeeded

    async def raise_compilation_error(
        self, args: CompilationErrorEventArgs
    ) -> None:
        """Raise the compilation error event."""
        logger.debug(
            f"Raising CompilationError event to {len(self._handlers)} handlers"
        )
        for handler in self._handlers:
            try:
                await handler.on_compilation_error(args)
            except Exception as e:
                logger.error(
                    f"Error in handler {handler.__class__.__name__} during "
                    f"CompilationError: {e}"
                )
                # Don't rethrow - error already occurred

    async def raise_hash_computed(self, args: HashComputedEventArgs) -> None:
        """Raise the hash computed event."""
        logger.debug(
            f"Raising HashComputed event ({args.item_type}: {args.item_identifier}) "
            f"to {len(self._handlers)} handlers"
        )
        for handler in self._handlers:
            try:
                await handler.on_hash_computed(args)
            except Exception as e:
                logger.error(
                    f"Error in handler {handler.__class__.__name__} during "
                    f"HashComputed: {e}"
                )
                # Don't rethrow - hash is already computed

    async def raise_hash_verified(self, args: HashVerifiedEventArgs) -> None:
        """Raise the hash verified event."""
        logger.debug(
            f"Raising HashVerified event ({args.item_type}: {args.item_identifier}) "
            f"to {len(self._handlers)} handlers"
        )
        for handler in self._handlers:
            try:
                await handler.on_hash_verified(args)
            except Exception as e:
                logger.error(
                    f"Error in handler {handler.__class__.__name__} during "
                    f"HashVerified: {e}"
                )
                # Don't rethrow - hash is already verified

    async def raise_hash_mismatch(self, args: HashMismatchEventArgs) -> None:
        """Raise the hash mismatch event."""
        logger.debug(
            f"Raising HashMismatch event ({args.item_type}: {args.item_identifier}) "
            f"to {len(self._handlers)} handlers"
        )
        for handler in self._handlers:
            try:
                await handler.on_hash_mismatch(args)
            except Exception as e:
                logger.error(
                    f"Error in handler {handler.__class__.__name__} during "
                    f"HashMismatch: {e}"
                )
                raise


# =============================================================================
# File Lock Service
# =============================================================================


@dataclass
class FileLockHandle:
    """Represents an active file lock."""

    lock_id: str
    file_path: str
    lock_type: FileLockType
    acquired_at: datetime
    content_hash: Optional[str]
    _file: Any = field(default=None, repr=False)
    _is_active: bool = field(default=True, repr=False)

    @property
    def is_active(self) -> bool:
        """Check if the lock is still active."""
        return self._is_active

    def release(self) -> None:
        """Release the lock."""
        if self._file and self._is_active:
            try:
                if _HAS_FCNTL:
                    fcntl.flock(self._file.fileno(), fcntl.LOCK_UN)
                else:
                    # Windows: unlock the file
                    msvcrt.locking(self._file.fileno(), msvcrt.LK_UNLCK, 1)
                self._file.close()
            except Exception:
                pass
            self._is_active = False

    def __enter__(self) -> "FileLockHandle":
        return self

    def __exit__(self, *args) -> None:
        self.release()

    async def __aenter__(self) -> "FileLockHandle":
        return self

    async def __aexit__(self, *args) -> None:
        self.release()


class FileLockService:
    """
    Service for managing file locks on local source files.

    Implements zero-trust file integrity verification.

    Uses fcntl on Unix-like systems and msvcrt on Windows.
    """

    def __init__(self) -> None:
        self._active_locks: Dict[str, FileLockHandle] = {}

    @property
    def active_locks(self) -> List[FileLockHandle]:
        """Get the currently held locks."""
        return list(self._active_locks.values())

    async def acquire_read_lock(
        self,
        file_path: str,
        compute_hash: bool = True,
        timeout: float = 30.0,
    ) -> FileLockHandle:
        """
        Acquire a read lock on a file.

        Args:
            file_path: Path to the file to lock.
            compute_hash: Whether to compute content hash for integrity verification.
            timeout: Maximum time to wait for lock (seconds).

        Returns:
            A lock handle that releases the lock when disposed.

        Raises:
            IOError: If the file cannot be locked.
        """
        return await self._acquire_lock(
            file_path, FileLockType.READ, compute_hash, timeout
        )

    async def acquire_write_lock(
        self,
        file_path: str,
        compute_hash: bool = True,
        timeout: float = 30.0,
    ) -> FileLockHandle:
        """
        Acquire a write lock on a file.

        Args:
            file_path: Path to the file to lock.
            compute_hash: Whether to compute content hash.
            timeout: Maximum time to wait for lock (seconds).

        Returns:
            A lock handle that releases the lock when disposed.

        Raises:
            IOError: If the file cannot be locked.
        """
        return await self._acquire_lock(
            file_path, FileLockType.WRITE, compute_hash, timeout
        )

    async def _acquire_lock(
        self,
        file_path: str,
        lock_type: FileLockType,
        compute_hash: bool,
        timeout: float,
    ) -> FileLockHandle:
        """Internal method to acquire a lock."""
        import uuid
        import time

        full_path = os.path.abspath(file_path)
        lock_id = str(uuid.uuid4())

        logger.debug(f"Acquiring {lock_type.value} lock on {full_path}")

        # Determine file mode
        if lock_type == FileLockType.READ:
            file_mode = "rb"
        else:
            file_mode = "r+b"

        start_time = time.time()
        file_handle = None

        while True:
            try:
                file_handle = open(full_path, file_mode)
                if _HAS_FCNTL:
                    # Unix: use flock
                    if lock_type == FileLockType.READ:
                        lock_mode = fcntl.LOCK_SH | fcntl.LOCK_NB
                    else:
                        lock_mode = fcntl.LOCK_EX | fcntl.LOCK_NB
                    fcntl.flock(file_handle.fileno(), lock_mode)
                else:
                    # Windows: use msvcrt locking
                    if lock_type == FileLockType.READ:
                        # Shared lock (non-blocking)
                        msvcrt.locking(file_handle.fileno(), msvcrt.LK_NBLCK, 1)
                    else:
                        # Exclusive lock (non-blocking)
                        msvcrt.locking(file_handle.fileno(), msvcrt.LK_NBLCK, 1)
                break
            except (IOError, OSError) as e:
                if file_handle:
                    file_handle.close()
                if time.time() - start_time > timeout:
                    raise IOError(f"Timeout acquiring {lock_type.value} lock on {full_path}") from e
                await asyncio.sleep(0.1)

        # Compute hash if requested
        content_hash = None
        if compute_hash:
            content_hash = await self.compute_hash(full_path)

        handle = FileLockHandle(
            lock_id=lock_id,
            file_path=full_path,
            lock_type=lock_type,
            acquired_at=datetime.utcnow(),
            content_hash=content_hash,
            _file=file_handle,
        )

        self._active_locks[lock_id] = handle
        logger.info(
            f"{lock_type.value.title()} lock acquired on {full_path} "
            f"(LockId: {lock_id[:8]}..., Hash: {content_hash[:16] if content_hash else 'N/A'}...)"
        )

        return handle

    async def try_acquire_read_lock(
        self,
        file_path: str,
        timeout: float = 5.0,
        compute_hash: bool = True,
    ) -> Optional[FileLockHandle]:
        """
        Try to acquire a read lock without raising on failure.

        Returns:
            Lock handle if successful, None otherwise.
        """
        try:
            return await self.acquire_read_lock(file_path, compute_hash, timeout)
        except IOError:
            logger.warning(f"Could not acquire read lock on {file_path}")
            return None

    async def verify_integrity(
        self, file_path: str, expected_hash: str
    ) -> bool:
        """
        Verify file integrity by comparing hashes.

        Returns:
            True if the file matches the expected hash.
        """
        current_hash = await self.compute_hash(file_path)
        matches = current_hash.lower() == expected_hash.lower()
        if not matches:
            logger.warning(
                f"Integrity check failed for {file_path}: "
                f"expected {expected_hash[:16]}..., got {current_hash[:16]}..."
            )
        return matches

    async def compute_hash(self, file_path: str) -> str:
        """
        Compute SHA-256 hash of a file's contents.

        Returns:
            Hex-encoded SHA-256 hash.
        """
        hasher = hashlib.sha256()
        with open(file_path, "rb") as f:
            for chunk in iter(lambda: f.read(8192), b""):
                hasher.update(chunk)
        return hasher.hexdigest()

    def release_lock(self, lock_handle: FileLockHandle) -> None:
        """Release a lock."""
        if lock_handle.lock_id in self._active_locks:
            lock_handle.release()
            del self._active_locks[lock_handle.lock_id]
            logger.debug(f"Lock released on {lock_handle.file_path}")

    async def release_all_locks(self) -> None:
        """Release all currently held locks."""
        logger.info(f"Releasing all {len(self._active_locks)} active locks")
        for lock_id, handle in list(self._active_locks.items()):
            try:
                handle.release()
            except Exception as e:
                logger.error(f"Error releasing lock {lock_id}: {e}")
        self._active_locks.clear()
