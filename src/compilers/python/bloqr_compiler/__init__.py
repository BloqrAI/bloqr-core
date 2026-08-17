"""
Rules Compiler - Python API for AdGuard filter rule compilation.

This package provides a Python interface for compiling AdGuard filter rules
using the @adguard/hostlist-compiler CLI tool.

Supports both synchronous and asynchronous APIs for flexible integration.

Synchronous Example:
    >>> from bloqr_compiler import BloqrCompiler
    >>> compiler = BloqrCompiler()
    >>> result = compiler.compile("compiler-config.yaml", copy_to_rules=True)
    >>> print(f"Compiled {result.rule_count} rules in {result.elapsed_formatted()}")

Asynchronous Example:
    >>> import asyncio
    >>> from bloqr_compiler import BloqrCompiler
    >>> async def main():
    ...     compiler = BloqrCompiler()
    ...     result = await compiler.compile_async("compiler-config.yaml")
    ...     print(f"Compiled {result.rule_count} rules")
    >>> asyncio.run(main())

Features:
    - Multi-format configuration support (JSON, YAML, TOML)
    - Synchronous and asynchronous APIs
    - Configuration validation
    - Custom error types for better error handling
    - Transformation presets (recommended, minimal, hosts)
    - Helper methods for result formatting
"""

# Define __version__ early to avoid circular imports (cli.py imports __version__)
__version__ = "2.0.0"

from bloqr_compiler.config import (
    ArchivingSettings,
    ConfigurationFormat,
    CompilerConfiguration,
    FilterSource,
    HashVerificationSettings,
    OutputSettings,
    SourceType,
    Transformation,
    read_configuration,
    detect_format,
    to_json,
    to_yaml,
    to_toml,
)
from bloqr_compiler.hash_database import HashDatabaseEntry, load_hash_database, record_hash
from bloqr_compiler.output_publisher import OutputPublishResult, publish_output
from bloqr_compiler.compiler import (
    CompilerResult,
    VersionInfo,
    PlatformInfo,
    BloqrCompiler,
    compile_rules,
    compile_rules_async,
    validate_configuration,
    get_version_info,
    get_platform_info,
    count_rules,
    count_rules_async,
    compute_hash,
    compute_hash_async,
    hash_short,
    format_elapsed,
    find_command,
)
from bloqr_compiler.errors import (
    CompilerError,
    ConfigNotFoundError,
    UnknownExtensionError,
    ParseError,
    ValidationError,
    CompilerNotFoundError,
    CompilationError,
    OutputNotCreatedError,
    CopyError,
    TimeoutError,
    ValidationResult,
    ErrorCode,
)
from bloqr_compiler.chunking import (
    ChunkingOptions,
    ChunkingStrategy,
    ChunkMetadata,
    ChunkedCompilationResult,
    should_enable_chunking,
    split_into_chunks,
    merge_chunks,
    compile_chunks_async,
    estimate_speedup,
)
from bloqr_compiler.events import (
    # Enums
    ValidationSeverity,
    FileLockType,
    # Validation types
    ValidationFinding,
    # Event args
    CompilationEventArgs,
    CompilationStartedEventArgs,
    ConfigurationLoadedEventArgs,
    ValidationEventArgs,
    SourceLoadingEventArgs,
    SourceLoadedEventArgs,
    FileLockAcquiredEventArgs,
    FileLockReleasedEventArgs,
    FileLockFailedEventArgs,
    ChunkStartedEventArgs,
    ChunkCompletedEventArgs,
    ChunksMergingEventArgs,
    ChunksMergedEventArgs,
    CompilationCompletedEventArgs,
    CompilationErrorEventArgs,
    HashComputedEventArgs,
    HashVerifiedEventArgs,
    HashMismatchEventArgs,
    # Handler and dispatcher
    CompilationEventHandler,
    EventDispatcher,
    # File locking
    FileLockHandle,
    FileLockService,
)
from bloqr_compiler.cli import main

__all__ = [
    # Version
    "__version__",
    # Config types
    "ConfigurationFormat",
    "CompilerConfiguration",
    "FilterSource",
    "SourceType",
    "Transformation",
    "OutputSettings",
    "HashVerificationSettings",
    "ArchivingSettings",
    # Config functions
    "read_configuration",
    "detect_format",
    "to_json",
    "to_yaml",
    "to_toml",
    # Hash database (.hashes.json sidecar)
    "HashDatabaseEntry",
    "load_hash_database",
    "record_hash",
    # Output publishing (conflict strategy + archiving)
    "OutputPublishResult",
    "publish_output",
    # Compiler types
    "CompilerResult",
    "VersionInfo",
    "PlatformInfo",
    "BloqrCompiler",
    # Compiler functions (sync and async)
    "compile_rules",
    "compile_rules_async",
    "validate_configuration",
    "get_version_info",
    "get_platform_info",
    "count_rules",
    "count_rules_async",
    "compute_hash",
    "compute_hash_async",
    "hash_short",
    "format_elapsed",
    "find_command",
    # Chunking types
    "ChunkingOptions",
    "ChunkingStrategy",
    "ChunkMetadata",
    "ChunkedCompilationResult",
    # Chunking functions
    "should_enable_chunking",
    "split_into_chunks",
    "merge_chunks",
    "compile_chunks_async",
    "estimate_speedup",
    # Error types
    "CompilerError",
    "ConfigNotFoundError",
    "UnknownExtensionError",
    "ParseError",
    "ValidationError",
    "CompilerNotFoundError",
    "CompilationError",
    "OutputNotCreatedError",
    "CopyError",
    "TimeoutError",
    "ValidationResult",
    "ErrorCode",
    # Event enums
    "ValidationSeverity",
    "FileLockType",
    # Event types
    "ValidationFinding",
    "CompilationEventArgs",
    "CompilationStartedEventArgs",
    "ConfigurationLoadedEventArgs",
    "ValidationEventArgs",
    "SourceLoadingEventArgs",
    "SourceLoadedEventArgs",
    "FileLockAcquiredEventArgs",
    "FileLockReleasedEventArgs",
    "FileLockFailedEventArgs",
    "ChunkStartedEventArgs",
    "ChunkCompletedEventArgs",
    "ChunksMergingEventArgs",
    "ChunksMergedEventArgs",
    "CompilationCompletedEventArgs",
    "CompilationErrorEventArgs",
    "HashComputedEventArgs",
    "HashVerifiedEventArgs",
    "HashMismatchEventArgs",
    # Event handling
    "CompilationEventHandler",
    "EventDispatcher",
    # File locking
    "FileLockHandle",
    "FileLockService",
    # CLI
    "main",
]
