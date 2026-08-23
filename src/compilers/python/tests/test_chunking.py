"""Tests for the chunking module."""

import os
import pytest

import bloqr_compiler.compiler as compiler_module
from bloqr_compiler import chunking as chunking_module
from bloqr_compiler.chunking import (
    ChunkingOptions,
    ChunkingStrategy,
    ChunkMetadata,
    ChunkedCompilationResult,
    should_enable_chunking,
    split_into_chunks,
    merge_chunks,
    estimate_speedup,
)
from bloqr_compiler.config import CompilerConfiguration, FilterSource


class TestGetCompilerCommand:
    """Regression tests for #424: chunked and unchunked paths must resolve the same
    underlying compiler command, not two independently-implemented lookups."""

    def test_chunking_reuses_compiler_modules_command_resolver(self):
        # chunking.py must not define its own "_get_compiler_command" - it should call
        # straight through to compiler.py's, so both paths always agree on which
        # compiler binary they invoke for a given config.
        assert not hasattr(chunking_module, "_get_compiler_command")
        assert chunking_module.compiler_module._get_compiler_command is (
            compiler_module._get_compiler_command
        )


class TestChunkingOptions:
    """Tests for ChunkingOptions."""

    def test_default_has_expected_values(self):
        """Default options should have chunking disabled."""
        options = ChunkingOptions.default()

        assert options.enabled is False
        assert options.chunk_size == 100_000
        assert options.max_parallel == (os.cpu_count() or 4)
        assert options.strategy == ChunkingStrategy.SOURCE

    def test_for_large_lists_enables_chunking(self):
        """Large list options should have chunking enabled."""
        options = ChunkingOptions.for_large_lists()

        assert options.enabled is True
        assert options.max_parallel >= 2


class TestShouldEnableChunking:
    """Tests for should_enable_chunking function."""

    def test_with_no_sources_returns_false(self):
        """Should return False when no sources."""
        config = CompilerConfiguration(name="Test", sources=[])
        options = ChunkingOptions(enabled=True)

        result = should_enable_chunking(config, options)

        assert result is False

    def test_with_explicitly_disabled_returns_false(self):
        """Should return False when explicitly disabled."""
        config = CompilerConfiguration(
            name="Test",
            sources=[FilterSource(source="http://example.com/list.txt")],
        )
        options = ChunkingOptions(enabled=False)

        result = should_enable_chunking(config, options)

        assert result is False

    def test_with_explicitly_enabled_returns_true(self):
        """Should return True when explicitly enabled."""
        config = CompilerConfiguration(
            name="Test",
            sources=[FilterSource(source="http://example.com/list.txt")],
        )
        options = ChunkingOptions(enabled=True)

        result = should_enable_chunking(config, options)

        assert result is True

    def test_with_multiple_sources_and_no_options_returns_true(self):
        """Should return True with multiple sources when options is None."""
        config = CompilerConfiguration(
            name="Test",
            sources=[
                FilterSource(source="http://example.com/list1.txt"),
                FilterSource(source="http://example.com/list2.txt"),
            ],
        )

        result = should_enable_chunking(config, None)

        assert result is True

    def test_with_single_source_returns_false(self):
        """Should return False with single source."""
        config = CompilerConfiguration(
            name="Test",
            sources=[FilterSource(source="http://example.com/list.txt")],
        )
        options = ChunkingOptions(strategy=ChunkingStrategy.SOURCE)

        result = should_enable_chunking(config, options)

        assert result is False


class TestSplitIntoChunks:
    """Tests for split_into_chunks function."""

    def test_with_no_sources_returns_empty_list(self):
        """Should return empty list when no sources."""
        config = CompilerConfiguration(name="Test", sources=[])
        options = ChunkingOptions(max_parallel=4)

        chunks = split_into_chunks(config, options)

        assert len(chunks) == 0

    def test_with_four_sources_and_two_parallel_creates_two_chunks(self):
        """Should create correct number of chunks."""
        config = CompilerConfiguration(
            name="Test",
            sources=[
                FilterSource(source="http://example.com/list1.txt"),
                FilterSource(source="http://example.com/list2.txt"),
                FilterSource(source="http://example.com/list3.txt"),
                FilterSource(source="http://example.com/list4.txt"),
            ],
        )
        options = ChunkingOptions(max_parallel=2, strategy=ChunkingStrategy.SOURCE)

        chunks = split_into_chunks(config, options)

        assert len(chunks) == 2
        assert len(chunks[0][0].sources) == 2
        assert len(chunks[1][0].sources) == 2

    def test_preserves_configuration_properties(self):
        """Should preserve configuration properties in chunks."""
        config = CompilerConfiguration(
            name="Test Filter",
            description="Test description",
            homepage="https://example.com",
            license="MIT",
            version="1.0.0",
            sources=[FilterSource(source="http://example.com/list.txt")],
            transformations=["Deduplicate", "RemoveComments"],
            inclusions=["*.com"],
            exclusions=["ads.*"],
        )
        options = ChunkingOptions(max_parallel=4, strategy=ChunkingStrategy.SOURCE)

        chunks = split_into_chunks(config, options)

        chunk_config = chunks[0][0]
        assert "Test Filter" in chunk_config.name
        assert chunk_config.description == config.description
        assert chunk_config.homepage == config.homepage
        assert chunk_config.license == config.license
        assert chunk_config.version == config.version
        assert chunk_config.transformations == config.transformations
        assert chunk_config.inclusions == config.inclusions
        assert chunk_config.exclusions == config.exclusions

    def test_sets_correct_metadata(self):
        """Should set correct chunk metadata."""
        config = CompilerConfiguration(
            name="Test",
            sources=[
                FilterSource(source="http://example.com/list1.txt"),
                FilterSource(source="http://example.com/list2.txt"),
                FilterSource(source="http://example.com/list3.txt"),
            ],
        )
        options = ChunkingOptions(max_parallel=2, strategy=ChunkingStrategy.SOURCE)

        chunks = split_into_chunks(config, options)

        assert len(chunks) == 2

        assert chunks[0][1].index == 0
        assert chunks[0][1].total == 2
        assert len(chunks[0][1].sources) == 2

        assert chunks[1][1].index == 1
        assert chunks[1][1].total == 2
        assert len(chunks[1][1].sources) == 1


class TestMergeChunks:
    """Tests for merge_chunks function."""

    def test_removes_duplicate_rules(self):
        """Should remove duplicate rules."""
        chunk_results = [
            ["||example.com^", "||test.com^"],
            ["||example.com^", "||other.com^"],  # Duplicate
        ]

        rules, duplicates_removed = merge_chunks(chunk_results)

        assert len(rules) == 3
        assert duplicates_removed == 1
        assert "||example.com^" in rules
        assert "||test.com^" in rules
        assert "||other.com^" in rules

    def test_preserves_comments(self):
        """Should preserve comments (not deduplicate them)."""
        chunk_results = [
            ["! Comment 1", "||example.com^"],
            ["! Comment 1", "||other.com^"],  # Same comment in different chunk
        ]

        rules, duplicates_removed = merge_chunks(chunk_results)

        assert len(rules) == 4  # Both comments are preserved
        assert duplicates_removed == 0  # Comments don't count as duplicates

    def test_preserves_empty_lines(self):
        """Should preserve empty lines."""
        chunk_results = [
            ["||example.com^", "", "||test.com^"],
            ["||other.com^", "", ""],
        ]

        rules, duplicates_removed = merge_chunks(chunk_results)

        assert len(rules) == 6  # All lines including empty ones
        assert duplicates_removed == 0

    def test_preserves_hash_comments(self):
        """Should preserve hash comments."""
        chunk_results = [
            ["# Comment 1", "||example.com^"],
            ["# Comment 2", "||other.com^"],
        ]

        rules, duplicates_removed = merge_chunks(chunk_results)

        assert len(rules) == 4
        assert "# Comment 1" in rules
        assert "# Comment 2" in rules

    def test_preserves_order(self):
        """Should preserve order of rules."""
        chunk_results = [
            ["||first.com^", "||second.com^"],
            ["||third.com^", "||fourth.com^"],
        ]

        rules, _ = merge_chunks(chunk_results)

        assert rules[0] == "||first.com^"
        assert rules[1] == "||second.com^"
        assert rules[2] == "||third.com^"
        assert rules[3] == "||fourth.com^"


class TestEstimateSpeedup:
    """Tests for estimate_speedup function."""

    def test_when_disabled_returns_one(self):
        """Should return 1.0 when chunking is disabled."""
        options = ChunkingOptions(enabled=False)

        speedup = estimate_speedup(100_000, options)

        assert speedup == 1.0

    def test_with_zero_rules_returns_one(self):
        """Should return 1.0 when there are no rules."""
        options = ChunkingOptions(enabled=True)

        speedup = estimate_speedup(0, options)

        assert speedup == 1.0

    def test_with_many_rules_returns_expected_speedup(self):
        """Should return expected speedup for large rule sets."""
        options = ChunkingOptions(
            enabled=True,
            chunk_size=100_000,
            max_parallel=8,
        )

        # 800,000 rules = 8 chunks = ~8x speedup (limited by MaxParallel)
        speedup = estimate_speedup(800_000, options)

        assert speedup == 8.0

    def test_limited_by_max_parallel(self):
        """Speedup should be limited by max_parallel."""
        options = ChunkingOptions(
            enabled=True,
            chunk_size=100_000,
            max_parallel=4,
        )

        # 1,000,000 rules = 10 chunks, but limited to 4 parallel
        speedup = estimate_speedup(1_000_000, options)

        assert speedup == 4.0


class TestChunkedCompilationResult:
    """Tests for ChunkedCompilationResult."""

    def test_estimated_speedup_calculates_correctly(self):
        """Should calculate speedup correctly."""
        result = ChunkedCompilationResult(
            total_elapsed_ms=1000,
            chunks=[
                ChunkMetadata(index=0, total=4, elapsed_ms=800),
                ChunkMetadata(index=1, total=4, elapsed_ms=900),
                ChunkMetadata(index=2, total=4, elapsed_ms=850),
                ChunkMetadata(index=3, total=4, elapsed_ms=750),
            ],
        )

        # Sum of individual chunks: 3300ms
        # Parallel time: 1000ms
        # Speedup: 3300 / 1000 = 3.3x
        assert abs(result.estimated_speedup - 3.3) < 0.1

    def test_estimated_speedup_with_no_chunks_returns_one(self):
        """Should return 1.0 when there are no chunks."""
        result = ChunkedCompilationResult(
            total_elapsed_ms=1000,
            chunks=[],
        )

        assert result.estimated_speedup == 1.0
