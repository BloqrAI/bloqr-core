"""
Chunking implementation for parallel compilation of large rule lists.

This module provides functionality to split large filter source configurations
into chunks for parallel compilation, which can significantly improve
compilation times for large filter lists.
"""

from __future__ import annotations

import asyncio
import logging
import os
import tempfile
import time
from dataclasses import dataclass, field
from enum import Enum

import bloqr_compiler.compiler as compiler_module
from bloqr_compiler.config import (
    CompilerConfiguration,
    FilterSource,
    to_json,
)

logger = logging.getLogger(__name__)


class ChunkingStrategy(Enum):
    """Strategy for splitting sources into chunks."""
    SOURCE = "source"
    LINE_COUNT = "line_count"  # Not yet implemented


@dataclass
class ChunkingOptions:
    """Configuration options for chunked parallel compilation."""
    enabled: bool = False
    chunk_size: int = 100_000
    max_parallel: int = field(default_factory=lambda: os.cpu_count() or 4)
    strategy: ChunkingStrategy = ChunkingStrategy.SOURCE

    @classmethod
    def default(cls) -> ChunkingOptions:
        """Get default chunking options with chunking disabled."""
        return cls(
            enabled=False,
            chunk_size=100_000,
            max_parallel=os.cpu_count() or 4,
            strategy=ChunkingStrategy.SOURCE,
        )

    @classmethod
    def for_large_lists(cls) -> ChunkingOptions:
        """Get chunking options optimized for large filter lists."""
        return cls(
            enabled=True,
            chunk_size=100_000,
            max_parallel=max(2, os.cpu_count() or 4),
            strategy=ChunkingStrategy.SOURCE,
        )


@dataclass
class ChunkMetadata:
    """Metadata about a compilation chunk."""
    index: int = 0
    total: int = 0
    estimated_rules: int = 0
    actual_rules: int | None = None
    sources: list[FilterSource] = field(default_factory=list)
    elapsed_ms: int | None = None
    success: bool = False
    error_message: str | None = None
    output_path: str | None = None


@dataclass
class ChunkedCompilationResult:
    """Result of chunked compilation."""
    success: bool = False
    total_elapsed_ms: int = 0
    chunks: list[ChunkMetadata] = field(default_factory=list)
    total_rules: int = 0
    final_rule_count: int = 0
    duplicates_removed: int = 0
    merged_rules: list[str] | None = None
    errors: list[str] = field(default_factory=list)

    @property
    def estimated_speedup(self) -> float:
        """Get the estimated speedup ratio compared to sequential compilation."""
        if not self.chunks:
            return 1.0
        total_chunk_time = sum(c.elapsed_ms or 0 for c in self.chunks)
        if self.total_elapsed_ms == 0:
            return 1.0
        return total_chunk_time / self.total_elapsed_ms


def should_enable_chunking(
    config: CompilerConfiguration,
    options: ChunkingOptions | None = None,
) -> bool:
    """
    Determine if chunking should be enabled for the given configuration.

    Args:
        config: The compiler configuration.
        options: The chunking options (optional).

    Returns:
        True if chunking should be enabled.
    """
    # If no sources, don't chunk
    if not config.sources:
        return False

    # If explicitly disabled, don't chunk
    if options is not None and not options.enabled:
        return False

    # If explicitly enabled, chunk
    if options is not None and options.enabled:
        logger.debug("Chunking explicitly enabled in options")
        return True

    # For source strategy (default), chunk if we have multiple sources
    strategy = options.strategy if options else ChunkingStrategy.SOURCE
    if strategy == ChunkingStrategy.SOURCE and len(config.sources) > 1:
        logger.debug("Chunking enabled: %d sources detected", len(config.sources))
        return True

    return False


def split_into_chunks(
    config: CompilerConfiguration,
    options: ChunkingOptions,
) -> list[tuple[CompilerConfiguration, ChunkMetadata]]:
    """
    Split a configuration into chunks for parallel compilation.

    Args:
        config: The original configuration.
        options: The chunking options.

    Returns:
        List of (chunked_config, metadata) tuples.
    """
    sources = config.sources or []
    chunks: list[tuple[CompilerConfiguration, ChunkMetadata]] = []

    if not sources:
        logger.warning("No sources to chunk")
        return chunks

    logger.info("Splitting configuration into chunks (strategy: %s)", options.strategy.value)

    if options.strategy == ChunkingStrategy.SOURCE:
        return _split_by_source(config, options)
    else:
        logger.warning("LineCount strategy not yet implemented, falling back to Source strategy")
        return _split_by_source(config, options)


def _split_by_source(
    config: CompilerConfiguration,
    options: ChunkingOptions,
) -> list[tuple[CompilerConfiguration, ChunkMetadata]]:
    """Split configuration by distributing sources evenly across chunks."""
    sources = config.sources or []
    chunks: list[tuple[CompilerConfiguration, ChunkMetadata]] = []

    # Calculate sources per chunk to keep chunks balanced
    sources_per_chunk = max(1, -(-len(sources) // options.max_parallel))  # Ceiling division
    total_chunks = -(-len(sources) // sources_per_chunk)  # Ceiling division

    logger.info(
        "Creating %d chunks with ~%d sources each",
        total_chunks,
        sources_per_chunk,
    )

    for i in range(total_chunks):
        start_idx = i * sources_per_chunk
        end_idx = min(start_idx + sources_per_chunk, len(sources))
        chunk_sources = sources[start_idx:end_idx]

        chunk_config = CompilerConfiguration(
            name=f"{config.name} (chunk {i + 1}/{total_chunks})",
            description=config.description,
            homepage=config.homepage,
            license=config.license,
            version=config.version,
            sources=chunk_sources,
            transformations=config.transformations,
            inclusions=config.inclusions,
            exclusions=config.exclusions,
            default_engine=config.default_engine,
        )

        metadata = ChunkMetadata(
            index=i,
            total=total_chunks,
            estimated_rules=0,
            sources=chunk_sources,
        )

        chunks.append((chunk_config, metadata))

    logger.debug("Created %d chunks", len(chunks))
    return chunks


def merge_chunks(chunk_results: list[list[str]]) -> tuple[list[str], int]:
    """
    Merge compiled rules from multiple chunks.

    Args:
        chunk_results: List of rule arrays from each chunk.

    Returns:
        Tuple of (merged_rules, duplicates_removed).
    """
    logger.info("Merging %d chunks...", len(chunk_results))

    # Flatten all chunks
    all_rules = [rule for chunk in chunk_results for rule in chunk]
    logger.debug("Total rules before deduplication: %d", len(all_rules))

    # Deduplicate while preserving order
    seen: set[str] = set()
    deduplicated: list[str] = []

    for rule in all_rules:
        trimmed = rule.strip()

        # Keep comments and empty lines without deduplication
        if not trimmed or trimmed.startswith("!") or trimmed.startswith("#"):
            deduplicated.append(rule)
            continue

        # Deduplicate actual rules
        if rule not in seen:
            seen.add(rule)
            deduplicated.append(rule)

    duplicates_removed = len(all_rules) - len(deduplicated)

    logger.info(
        "Merged to %d rules (removed %d duplicates)",
        len(deduplicated),
        duplicates_removed,
    )

    return deduplicated, duplicates_removed


def estimate_speedup(total_rules: int, options: ChunkingOptions) -> float:
    """
    Estimate the time savings from chunked compilation.

    Args:
        total_rules: Estimated total rule count.
        options: The chunking options.

    Returns:
        Estimated speedup ratio (1.0 = no improvement).
    """
    if not options.enabled or total_rules == 0:
        return 1.0

    # Simple linear model
    import math
    num_chunks = math.ceil(total_rules / options.chunk_size)
    batches = math.ceil(num_chunks / options.max_parallel)

    # Theoretical speedup = numChunks / batches = min(numChunks, maxParallel)
    return min(num_chunks, options.max_parallel)


async def compile_chunks_async(
    chunks: list[tuple[CompilerConfiguration, ChunkMetadata]],
    options: ChunkingOptions,
    debug: bool = False,
) -> ChunkedCompilationResult:
    """
    Compile chunks in parallel.

    Args:
        chunks: List of (config, metadata) tuples to compile.
        options: The chunking options.
        debug: Enable debug logging.

    Returns:
        The chunked compilation result.
    """
    result = ChunkedCompilationResult()
    start_time = time.time()
    chunk_results: list[list[str]] = []

    logger.info(
        "Compiling %d chunks with max %d parallel workers",
        len(chunks),
        options.max_parallel,
    )

    # Process chunks in batches to limit parallelism
    for batch_start in range(0, len(chunks), options.max_parallel):
        batch_end = min(batch_start + options.max_parallel, len(chunks))
        batch = chunks[batch_start:batch_end]

        batch_number = batch_start // options.max_parallel + 1
        total_batches = -(-len(chunks) // options.max_parallel)

        logger.info(
            "Processing batch %d/%d (chunks %d-%d)",
            batch_number,
            total_batches,
            batch_start + 1,
            batch_end,
        )

        # Compile all chunks in this batch in parallel
        tasks = [
            _compile_single_chunk_async(config, metadata, debug)
            for config, metadata in batch
        ]

        batch_results = await asyncio.gather(*tasks, return_exceptions=True)

        for i, batch_result in enumerate(batch_results):
            config, metadata = batch[i]
            if isinstance(batch_result, Exception):
                metadata.success = False
                metadata.error_message = str(batch_result)
                result.errors.append(f"Chunk {metadata.index + 1}: {batch_result}")
                logger.error(
                    "Chunk %d/%d failed: %s",
                    metadata.index + 1,
                    metadata.total,
                    batch_result,
                )
            else:
                rules, metadata = batch_result
                if metadata.success and rules:
                    chunk_results.append(rules)
                if not metadata.success and metadata.error_message:
                    result.errors.append(f"Chunk {metadata.index + 1}: {metadata.error_message}")

            result.chunks.append(metadata)

    # Calculate total time
    result.total_elapsed_ms = int((time.time() - start_time) * 1000)

    # Merge results
    if chunk_results:
        merged_rules, duplicates_removed = merge_chunks(chunk_results)
        result.merged_rules = merged_rules
        result.duplicates_removed = duplicates_removed
        result.final_rule_count = len(merged_rules)

    result.total_rules = sum(c.actual_rules or 0 for c in result.chunks)
    result.success = len(result.errors) == 0

    logger.info(
        "Chunked compilation complete: %d rules (removed %d duplicates) in %dms",
        result.final_rule_count,
        result.duplicates_removed,
        result.total_elapsed_ms,
    )

    if result.estimated_speedup > 1.0:
        logger.info("Estimated speedup: %.2fx", result.estimated_speedup)

    return result


async def _compile_single_chunk_async(
    config: CompilerConfiguration,
    metadata: ChunkMetadata,
    debug: bool = False,
) -> tuple[list[str], ChunkMetadata]:
    """
    Compile a single chunk asynchronously.

    Args:
        config: The chunk configuration.
        metadata: The chunk metadata.
        debug: Enable debug logging.

    Returns:
        Tuple of (rules, updated_metadata).
    """
    start_time = time.time()
    temp_config_path: str | None = None
    temp_output_path: str | None = None

    try:
        logger.debug(
            "Starting chunk %d/%d: %s",
            metadata.index + 1,
            metadata.total,
            config.name,
        )

        # Create temporary config and output files
        temp_config_fd, temp_config_path = tempfile.mkstemp(suffix=".json", prefix="chunk-config-")
        temp_output_fd, temp_output_path = tempfile.mkstemp(suffix=".txt", prefix="chunk-output-")

        # Close file descriptors (we'll write to the file by path)
        os.close(temp_config_fd)
        os.close(temp_output_fd)

        # Write config to temp file
        with open(temp_config_path, "w", encoding="utf-8") as f:
            f.write(to_json(config))

        # Get compiler command - shared with the unchunked path (bloqr_compiler.compiler) so
        # both invoke the same underlying compiler for the same config; see #424. Chunked
        # compilation doesn't support the multi-engine (--engine/--browser-output) path yet
        # (#438); each chunk's own sources still carry per-source engine/defaultEngine, so
        # @bloqr/compiler-core continues to auto-detect per-chunk, matching the unchunked
        # default behavior.
        cmd, _cwd = compiler_module._get_compiler_command(temp_config_path, temp_output_path)

        if debug:
            logger.debug("Running: %s", " ".join(cmd))

        # Execute compiler asynchronously
        proc = await asyncio.create_subprocess_exec(
            *cmd,
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE,
        )
        stdout_bytes, stderr_bytes = await asyncio.wait_for(
            proc.communicate(),
            timeout=300,  # 5 minute timeout
        )

        if proc.returncode != 0:
            stderr = stderr_bytes.decode("utf-8") if stderr_bytes else ""
            raise RuntimeError(f"Compilation failed with exit code {proc.returncode}: {stderr}")

        # Read output
        if not os.path.exists(temp_output_path):
            raise RuntimeError("Output file was not created")

        with open(temp_output_path, encoding="utf-8") as f:
            rules = f.read().splitlines()

        # Update metadata
        metadata.success = True
        metadata.elapsed_ms = int((time.time() - start_time) * 1000)
        metadata.actual_rules = len(rules)
        metadata.output_path = temp_output_path

        logger.info(
            "Chunk %d/%d complete: %d rules in %dms",
            metadata.index + 1,
            metadata.total,
            len(rules),
            metadata.elapsed_ms,
        )

        return rules, metadata

    except Exception as e:
        metadata.success = False
        metadata.elapsed_ms = int((time.time() - start_time) * 1000)
        metadata.error_message = str(e)

        logger.error(
            "Chunk %d/%d failed: %s",
            metadata.index + 1,
            metadata.total,
            e,
        )

        return [], metadata

    finally:
        # Clean up temp files
        for path in [temp_config_path, temp_output_path]:
            if path and os.path.exists(path):
                try:
                    os.unlink(path)
                except Exception:
                    pass

