"""Tests for the compiler module."""

from pathlib import Path

import pytest

from bloqr_compiler.compiler import (
    CompilerResult,
    PlatformInfo,
    VersionInfo,
    count_rules,
    compute_hash,
    format_elapsed,
    get_platform_info,
    get_version_info,
    hash_short,
)


class TestCountRules:
    """Tests for count_rules function."""

    def test_counts_non_comment_lines(self, tmp_path: Path) -> None:
        content = """! Comment line
# Another comment
||example.com^
||test.org^

@@||allowed.com^
! More comments
||blocked.net^
"""
        file_path = tmp_path / "rules.txt"
        file_path.write_text(content)

        count = count_rules(file_path)

        assert count == 4  # 3 blocking rules + 1 allow rule

    def test_handles_empty_file(self, tmp_path: Path) -> None:
        file_path = tmp_path / "empty.txt"
        file_path.write_text("")

        assert count_rules(file_path) == 0

    def test_handles_all_comments(self, tmp_path: Path) -> None:
        content = """! Comment 1
! Comment 2
# Comment 3

"""
        file_path = tmp_path / "comments.txt"
        file_path.write_text(content)

        assert count_rules(file_path) == 0

    def test_handles_missing_file(self, tmp_path: Path) -> None:
        file_path = tmp_path / "nonexistent.txt"

        assert count_rules(file_path) == 0


class TestComputeHash:
    """Tests for compute_hash function."""

    def test_returns_consistent_hash(self, tmp_path: Path) -> None:
        content = "Test content for hashing"
        file_path = tmp_path / "test.txt"
        file_path.write_text(content)

        hash1 = compute_hash(file_path)
        hash2 = compute_hash(file_path)

        assert hash1 == hash2
        assert len(hash1) == 96  # SHA-384 produces 96 hex characters

    def test_different_content_different_hash(self, tmp_path: Path) -> None:
        file1 = tmp_path / "file1.txt"
        file2 = tmp_path / "file2.txt"
        file1.write_text("Content A")
        file2.write_text("Content B")

        hash1 = compute_hash(file1)
        hash2 = compute_hash(file2)

        assert hash1 != hash2


class TestHashShort:
    """Tests for hash_short function."""

    def test_shortens_long_hash(self) -> None:
        long_hash = "a" * 96
        short = hash_short(long_hash, 32)
        assert len(short) == 32
        assert short == "a" * 32

    def test_preserves_short_hash(self) -> None:
        short_hash = "abc123"
        result = hash_short(short_hash, 32)
        assert result == short_hash


class TestFormatElapsed:
    """Tests for format_elapsed function."""

    def test_formats_milliseconds(self) -> None:
        assert format_elapsed(500) == "500ms"
        assert format_elapsed(100) == "100ms"
        assert format_elapsed(0) == "0ms"

    def test_formats_seconds(self) -> None:
        assert format_elapsed(1000) == "1.00s"
        assert format_elapsed(1500) == "1.50s"
        assert format_elapsed(2345) == "2.35s"


class TestGetPlatformInfo:
    """Tests for get_platform_info function."""

    def test_returns_valid_info(self) -> None:
        info = get_platform_info()

        assert isinstance(info, PlatformInfo)
        assert info.os_name != ""
        assert info.architecture != ""

        # At least one platform flag should be true
        assert info.is_windows or info.is_linux or info.is_macos


class TestGetVersionInfo:
    """Tests for get_version_info function."""

    def test_returns_version_info(self) -> None:
        info = get_version_info()

        assert isinstance(info, VersionInfo)
        assert info.module_version != ""
        assert info.python_version != ""
        assert isinstance(info.platform, PlatformInfo)


class TestVersionInfo:
    """Tests for VersionInfo class."""

    def test_has_node(self) -> None:
        info_with = VersionInfo(node_version="v20.0.0")
        info_without = VersionInfo(node_version=None)

        assert info_with.has_node() is True
        assert info_without.has_node() is False

    def test_has_compiler(self) -> None:
        info_with = VersionInfo(hostlist_compiler_version="1.0.0")
        info_without = VersionInfo(hostlist_compiler_version=None)

        assert info_with.has_compiler() is True
        assert info_without.has_compiler() is False

    def test_collect(self) -> None:
        info = VersionInfo.collect()
        assert isinstance(info, VersionInfo)
        assert info.module_version != ""


class TestCompilerResult:
    """Tests for CompilerResult class."""

    def test_elapsed_formatted(self) -> None:
        result = CompilerResult(elapsed_ms=1500)
        assert result.elapsed_formatted() == "1.50s"

        result2 = CompilerResult(elapsed_ms=500)
        assert result2.elapsed_formatted() == "500ms"

    def test_hash_short(self) -> None:
        result = CompilerResult(output_hash="a" * 96)
        assert result.hash_short() == "a" * 32
        assert result.hash_short(16) == "a" * 16

    def test_output_path_str(self) -> None:
        result = CompilerResult(output_path="/path/to/output.txt")
        assert result.output_path_str() == "/path/to/output.txt"

        empty = CompilerResult(output_path="")
        assert empty.output_path_str() == ""

    def test_rules_destination_str(self) -> None:
        result = CompilerResult(rules_destination="/path/to/rules")
        assert result.rules_destination_str() == "/path/to/rules"

        empty = CompilerResult(rules_destination=None)
        assert empty.rules_destination_str() is None
