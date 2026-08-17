#!/usr/bin/env python3
"""
Run benchmarks comparing sequential vs chunked/parallel compilation.

This script runs the same compilation task with and without chunking enabled
to measure the actual speedup from parallel processing.

Usage:
    python run_benchmarks.py                    # Run all benchmarks
    python run_benchmarks.py --compiler python  # Run only Python compiler benchmarks
    python run_benchmarks.py --size medium      # Run only medium-sized tests
    python run_benchmarks.py --iterations 5     # Run 5 iterations per test
"""

import argparse
import json
import os
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path
from typing import Any


class BenchmarkResult:
    """Result of a single benchmark run."""

    def __init__(
        self,
        name: str,
        compiler: str,
        chunking_enabled: bool,
        elapsed_ms: float,
        success: bool,
        rule_count: int = 0,
        error: str = "",
    ):
        self.name = name
        self.compiler = compiler
        self.chunking_enabled = chunking_enabled
        self.elapsed_ms = elapsed_ms
        self.success = success
        self.rule_count = rule_count
        self.error = error

    def to_dict(self) -> dict:
        return {
            "name": self.name,
            "compiler": self.compiler,
            "chunking_enabled": self.chunking_enabled,
            "elapsed_ms": round(self.elapsed_ms, 2),
            "success": self.success,
            "rule_count": self.rule_count,
            "error": self.error,
        }


class BenchmarkRunner:
    """Runs benchmarks across different compilers and configurations."""

    def __init__(self, data_dir: Path, results_dir: Path):
        self.data_dir = data_dir
        self.results_dir = results_dir
        self.results_dir.mkdir(parents=True, exist_ok=True)

    def find_compiler(self, compiler: str) -> tuple[str, list[str]] | None:
        """Find the compiler executable and base args."""
        project_root = Path(__file__).parent.parent

        if compiler == "python":
            # Check if bloqr-compiler is installed
            try:
                result = subprocess.run(
                    ["bloqr-compiler", "--version"],
                    capture_output=True,
                    timeout=5
                )
                if result.returncode == 0:
                    return "bloqr-compiler", []
            except Exception:
                pass

            # Try running via python -m
            return sys.executable, ["-m", "bloqr_compiler"]

        elif compiler == "dotnet":
            console_project = project_root / "src" / "compilers" / "dotnet" / "src" / "Bloqr.Compiler.Dotnet.Console"
            if console_project.exists():
                return "dotnet", ["run", "--project", str(console_project), "--"]
            return None

        elif compiler == "rust":
            rust_binary = project_root / "src" / "rules-compiler-rust" / "target" / "release" / "rules-compiler"
            if rust_binary.exists():
                return str(rust_binary), []
            # Try cargo run
            rust_project = project_root / "src" / "rules-compiler-rust"
            if rust_project.exists():
                return "cargo", ["run", "--release", "--manifest-path", str(rust_project / "Cargo.toml"), "--"]
            return None

        elif compiler == "typescript":
            ts_project = project_root / "src" / "compilers" / "typescript"
            if ts_project.exists():
                return "deno", ["task", "--cwd", str(ts_project), "compile"]
            return None

        return None

    def run_single_benchmark(
        self,
        config_path: Path,
        compiler: str,
        chunking_enabled: bool,
        output_path: Path,
    ) -> BenchmarkResult:
        """Run a single benchmark and return the result."""
        name = config_path.stem

        # Find compiler
        cmd_info = self.find_compiler(compiler)
        if cmd_info is None:
            return BenchmarkResult(
                name=name,
                compiler=compiler,
                chunking_enabled=chunking_enabled,
                elapsed_ms=0,
                success=False,
                error=f"Compiler '{compiler}' not found",
            )

        cmd, base_args = cmd_info

        # Build command
        args = base_args + [
            "-c", str(config_path),
            "-o", str(output_path),
        ]

        # Add chunking flag based on compiler
        if chunking_enabled:
            if compiler in ["python", "dotnet", "rust"]:
                args.extend(["--chunking", "--max-parallel", str(os.cpu_count() or 4)])
            elif compiler == "typescript":
                args.extend(["--parallel"])

        # Run the command
        start_time = time.perf_counter()
        try:
            result = subprocess.run(
                [cmd] + args,
                capture_output=True,
                timeout=300,  # 5 minute timeout
                text=True,
            )
            elapsed_ms = (time.perf_counter() - start_time) * 1000

            if result.returncode != 0:
                return BenchmarkResult(
                    name=name,
                    compiler=compiler,
                    chunking_enabled=chunking_enabled,
                    elapsed_ms=elapsed_ms,
                    success=False,
                    error=result.stderr[:500] if result.stderr else "Unknown error",
                )

            # Count rules in output
            rule_count = 0
            if output_path.exists():
                content = output_path.read_text(encoding="utf-8")
                rule_count = len([
                    line for line in content.split("\n")
                    if line.strip() and not line.startswith("!")
                ])

            return BenchmarkResult(
                name=name,
                compiler=compiler,
                chunking_enabled=chunking_enabled,
                elapsed_ms=elapsed_ms,
                success=True,
                rule_count=rule_count,
            )

        except subprocess.TimeoutExpired:
            return BenchmarkResult(
                name=name,
                compiler=compiler,
                chunking_enabled=chunking_enabled,
                elapsed_ms=300000,
                success=False,
                error="Timeout after 5 minutes",
            )
        except Exception as e:
            return BenchmarkResult(
                name=name,
                compiler=compiler,
                chunking_enabled=chunking_enabled,
                elapsed_ms=0,
                success=False,
                error=str(e),
            )

    def run_benchmark_set(
        self,
        config_path: Path,
        compiler: str,
        iterations: int = 3,
    ) -> dict[str, Any]:
        """Run a benchmark set (sequential and chunked) with multiple iterations."""
        results = {
            "config": config_path.name,
            "compiler": compiler,
            "iterations": iterations,
            "sequential": [],
            "chunked": [],
        }

        output_dir = self.results_dir / "output"
        output_dir.mkdir(exist_ok=True)

        # Run sequential (no chunking)
        print(f"  Running sequential...", end=" ", flush=True)
        for i in range(iterations):
            output_path = output_dir / f"{config_path.stem}_{compiler}_seq_{i}.txt"
            result = self.run_single_benchmark(
                config_path, compiler, chunking_enabled=False, output_path=output_path
            )
            results["sequential"].append(result.to_dict())
            print(".", end="", flush=True)
        print()

        # Run with chunking
        print(f"  Running chunked...", end=" ", flush=True)
        for i in range(iterations):
            output_path = output_dir / f"{config_path.stem}_{compiler}_chunk_{i}.txt"
            result = self.run_single_benchmark(
                config_path, compiler, chunking_enabled=True, output_path=output_path
            )
            results["chunked"].append(result.to_dict())
            print(".", end="", flush=True)
        print()

        # Calculate statistics
        seq_times = [r["elapsed_ms"] for r in results["sequential"] if r["success"]]
        chunk_times = [r["elapsed_ms"] for r in results["chunked"] if r["success"]]

        if seq_times and chunk_times:
            seq_avg = sum(seq_times) / len(seq_times)
            chunk_avg = sum(chunk_times) / len(chunk_times)
            speedup = seq_avg / chunk_avg if chunk_avg > 0 else 1.0

            results["stats"] = {
                "sequential_avg_ms": round(seq_avg, 2),
                "chunked_avg_ms": round(chunk_avg, 2),
                "speedup": round(speedup, 2),
                "time_saved_ms": round(seq_avg - chunk_avg, 2),
            }

            print(f"  Result: {speedup:.2f}x speedup ({seq_avg:.0f}ms -> {chunk_avg:.0f}ms)")
        else:
            results["stats"] = {"error": "Not enough successful runs"}

        return results

    def run_all_benchmarks(
        self,
        compilers: list[str],
        sizes: list[str] | None = None,
        iterations: int = 3,
    ) -> dict:
        """Run all benchmarks and return results."""
        all_results = {
            "timestamp": datetime.now().isoformat(),
            "system_info": {
                "cpu_count": os.cpu_count(),
                "platform": sys.platform,
            },
            "iterations": iterations,
            "benchmarks": [],
        }

        # Find config files
        config_files = sorted(self.data_dir.glob("config-*.json"))
        if sizes:
            config_files = [
                f for f in config_files
                if any(s in f.stem for s in sizes)
            ]

        if not config_files:
            print("No config files found in", self.data_dir)
            return all_results

        print(f"\nFound {len(config_files)} config files")
        print(f"Testing compilers: {', '.join(compilers)}")
        print(f"Iterations per test: {iterations}")
        print("=" * 60)

        for config_path in config_files:
            print(f"\n{config_path.name}:")

            for compiler in compilers:
                print(f"  Compiler: {compiler}")
                results = self.run_benchmark_set(config_path, compiler, iterations)
                all_results["benchmarks"].append(results)

        return all_results


def print_summary(results: dict) -> None:
    """Print a summary of benchmark results."""
    print("\n" + "=" * 60)
    print("BENCHMARK SUMMARY")
    print("=" * 60)

    # Group by config
    by_config = {}
    for benchmark in results["benchmarks"]:
        config = benchmark["config"]
        if config not in by_config:
            by_config[config] = []
        by_config[config].append(benchmark)

    for config, benchmarks in by_config.items():
        print(f"\n{config}:")
        for b in benchmarks:
            if "stats" in b and "speedup" in b.get("stats", {}):
                stats = b["stats"]
                print(f"  {b['compiler']:12} {stats['speedup']:5.2f}x speedup "
                      f"({stats['sequential_avg_ms']:.0f}ms -> {stats['chunked_avg_ms']:.0f}ms)")
            else:
                print(f"  {b['compiler']:12} [Error or incomplete]")

    # Calculate overall averages
    speedups = [
        b["stats"]["speedup"]
        for b in results["benchmarks"]
        if "stats" in b and "speedup" in b.get("stats", {})
    ]

    if speedups:
        avg_speedup = sum(speedups) / len(speedups)
        print(f"\nOverall average speedup: {avg_speedup:.2f}x")


def main():
    parser = argparse.ArgumentParser(
        description="Run chunking benchmarks across compilers"
    )
    parser.add_argument(
        "--compiler",
        type=str,
        choices=["python", "dotnet", "rust", "typescript"],
        help="Run only this compiler (default: all available)"
    )
    parser.add_argument(
        "--size",
        type=str,
        help="Run only configs matching this size (e.g., 'small', 'medium')"
    )
    parser.add_argument(
        "--iterations",
        type=int,
        default=3,
        help="Number of iterations per test (default: 3)"
    )
    parser.add_argument(
        "--output",
        type=str,
        help="Output file for results JSON"
    )

    args = parser.parse_args()

    # Set up paths
    script_dir = Path(__file__).parent
    data_dir = script_dir / "data"
    results_dir = script_dir / "results"

    if not data_dir.exists():
        print("Error: No benchmark data found. Run 'python generate_synthetic_data.py' first.")
        sys.exit(1)

    # Determine compilers to test
    compilers = [args.compiler] if args.compiler else ["python", "dotnet", "rust", "typescript"]

    # Determine sizes
    sizes = [args.size] if args.size else None

    # Run benchmarks
    runner = BenchmarkRunner(data_dir, results_dir)
    results = runner.run_all_benchmarks(compilers, sizes, args.iterations)

    # Save results
    output_path = Path(args.output) if args.output else results_dir / f"benchmark_results_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(f"\nResults saved to: {output_path}")

    # Print summary
    print_summary(results)


if __name__ == "__main__":
    main()
