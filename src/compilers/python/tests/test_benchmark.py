"""Tests for the benchmark module (#419)."""

from pathlib import Path

import pytest

from bloqr_compiler.benchmark import (
    BENCHMARK_SIZES,
    BENCHMARK_TRANSFORMATIONS,
    BenchmarkRunResult,
    build_benchmark_config,
    find_benchmark_data_dir,
    run_benchmark,
)


class TestBuildBenchmarkConfig:
    """Tests for build_benchmark_config."""

    def test_creates_expected_number_of_identical_sources(self):
        config = build_benchmark_config("small", Path("/tmp/small.txt"), 4)

        assert len(config.sources) == 4
        assert all(s.source == "/tmp/small.txt" for s in config.sources)
        assert all(s.type == "adblock" for s in config.sources)
        assert [s.name for s in config.sources] == ["source-1", "source-2", "source-3", "source-4"]

    def test_uses_shared_benchmark_transformations(self):
        config = build_benchmark_config("small", Path("/tmp/small.txt"), 1)

        assert config.transformations == list(BENCHMARK_TRANSFORMATIONS)

    def test_num_sources_is_clamped_to_at_least_one(self):
        config = build_benchmark_config("small", Path("/tmp/small.txt"), 0)

        assert len(config.sources) == 1

    def test_name_and_description_mention_the_size(self):
        config = build_benchmark_config("xlarge", Path("/tmp/xlarge.txt"), 1)

        assert "xlarge" in config.name
        assert "xlarge" in config.description


class TestFindBenchmarkDataDir:
    """Tests for find_benchmark_data_dir."""

    def test_finds_benchmarks_data_when_present(self, tmp_path, monkeypatch):
        (tmp_path / "benchmarks" / "data").mkdir(parents=True)
        nested = tmp_path / "src" / "compilers" / "python"
        nested.mkdir(parents=True)
        monkeypatch.chdir(nested)

        result = find_benchmark_data_dir()

        assert result == tmp_path / "benchmarks" / "data"

    def test_returns_none_when_not_found(self, tmp_path, monkeypatch):
        monkeypatch.chdir(tmp_path)

        assert find_benchmark_data_dir() is None


class TestRunBenchmark:
    """Tests for run_benchmark's validation and dataset-discovery behavior (no real compiler
    invocations - those are covered by manual end-to-end runs, matching the Rust/.NET/TS
    benchmark subcommands' own test scope)."""

    def test_rejects_unknown_size(self, tmp_path):
        with pytest.raises(ValueError, match="Unknown benchmark size 'bogus'"):
            run_benchmark(size="bogus", data_dir=tmp_path)

    def test_raises_when_no_data_dir_found(self, tmp_path, monkeypatch):
        monkeypatch.chdir(tmp_path)

        with pytest.raises(FileNotFoundError, match="benchmarks/data"):
            run_benchmark(size="small", data_dir=None)

    def test_missing_dataset_file_reports_as_a_result_error_not_an_exception(self, tmp_path):
        results = run_benchmark(size="small", data_dir=tmp_path, num_sources=1)

        assert len(results) == 1
        assert results[0].size == "small"
        assert results[0].unchunked_success is False
        assert results[0].chunked_success is False
        assert "dataset file not found" in results[0].error

    def test_all_expands_to_every_benchmark_size(self, tmp_path):
        results = run_benchmark(size="all", data_dir=tmp_path, num_sources=1)

        assert [r.size for r in results] == list(BENCHMARK_SIZES)


class TestBenchmarkRunResultToDict:
    """Tests for BenchmarkRunResult.to_dict's JSON-serializable shape."""

    def test_uses_camel_case_keys_matching_the_other_language_wrappers(self):
        result = BenchmarkRunResult(
            size="small",
            sources=4,
            max_parallel=4,
            unchunked_success=True,
            unchunked_ms=100,
            unchunked_rule_count=50,
            chunked_success=True,
            chunked_ms=40,
            chunked_rule_count=50,
            speedup=2.5,
        )

        assert result.to_dict() == {
            "size": "small",
            "sources": 4,
            "maxParallel": 4,
            "unchunkedSuccess": True,
            "unchunkedMs": 100,
            "unchunkedRuleCount": 50,
            "chunkedSuccess": True,
            "chunkedMs": 40,
            "chunkedRuleCount": 50,
            "speedup": 2.5,
            "error": None,
        }
