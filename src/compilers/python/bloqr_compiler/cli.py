#!/usr/bin/env python3
"""
Command-line interface for the AdGuard Filter Rules Compiler.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from bloqr_compiler import __version__
from bloqr_compiler.compiler import (
    BloqrCompiler,
    get_version_info,
    validate_configuration,
)
from bloqr_compiler.config import (
    ConfigurationFormat,
    Transformation,
    read_configuration,
    to_json,
)


def create_parser() -> argparse.ArgumentParser:
    """Create the argument parser."""
    parser = argparse.ArgumentParser(
        prog="bloqr-compiler",
        description="AdGuard Filter Rules Compiler - Python API",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  bloqr-compiler                           # Use default config
  bloqr-compiler config.yaml               # Use positional config path
  bloqr-compiler -c config.yaml -r         # Use YAML config, copy to rules
  bloqr-compiler --config config.toml      # Use TOML config
  bloqr-compiler --show-config             # Display parsed configuration
  bloqr-compiler --validate                # Validate config without compiling
  bloqr-compiler -v                        # Show version info
  bloqr-compiler --transformations         # List available transformations
        """,
    )

    # Positional argument for config (optional)
    parser.add_argument(
        "config_path",
        nargs="?",
        metavar="CONFIG",
        help="Path to configuration file (can also use -c/--config)",
    )

    parser.add_argument(
        "-c", "--config",
        metavar="PATH",
        help="Path to configuration file (default: compiler-config.json)",
    )

    parser.add_argument(
        "-o", "--output",
        metavar="PATH",
        help="Path to output file (default: output/compiled-TIMESTAMP.txt)",
    )

    parser.add_argument(
        "-r", "--copy-to-rules",
        action="store_true",
        help="Copy output to rules directory",
    )

    parser.add_argument(
        "--rules-dir",
        metavar="PATH",
        help="Custom rules directory path (used with -r)",
    )

    parser.add_argument(
        "-f", "--format",
        choices=["json", "yaml", "toml"],
        help="Force configuration format (default: auto-detect)",
    )

    parser.add_argument(
        "-v", "--version",
        action="store_true",
        help="Show version information and exit",
    )

    parser.add_argument(
        "-V", "--version-info",
        action="store_true",
        help="Show version information and exit (alias for -v)",
    )

    parser.add_argument(
        "-d", "--debug",
        action="store_true",
        help="Enable debug output",
    )

    parser.add_argument(
        "--show-config",
        action="store_true",
        help="Display parsed configuration without compiling",
    )

    parser.add_argument(
        "--validate",
        action="store_true",
        help="Validate configuration only (no compilation)",
    )

    parser.add_argument(
        "--validate-config",
        action="store_true",
        help="Enable configuration validation before compilation (default: true)",
    )

    parser.add_argument(
        "--no-validate-config",
        action="store_true",
        help="Disable configuration validation before compilation",
    )

    parser.add_argument(
        "--fail-on-warnings",
        action="store_true",
        help="Fail compilation if configuration has validation warnings",
    )

    parser.add_argument(
        "--check-files",
        action="store_true",
        help="Check if local source files exist (use with --validate)",
    )

    parser.add_argument(
        "--transformations",
        action="store_true",
        help="List all available transformations and exit",
    )

    parser.add_argument(
        "-i", "--interactive",
        action="store_true",
        help="Run in interactive menu mode",
    )

    parser.add_argument(
        "--benchmark",
        action="store_true",
        help="Run synthetic benchmark to show expected chunking speedups",
    )

    parser.add_argument(
        "--benchmark-rules",
        type=int,
        default=200_000,
        metavar="COUNT",
        help="Number of rules to simulate in benchmark (default: 200000)",
    )

    parser.add_argument(
        "--benchmark-parallel",
        type=int,
        default=None,
        metavar="WORKERS",
        help="Max parallel workers for benchmark (default: CPU count, max 8)",
    )

    return parser


def show_version() -> None:
    """Display version information."""
    info = get_version_info()

    print("=" * 60)
    print("  AdGuard Filter Rules Compiler (Python API)")
    print("=" * 60)
    print()
    print(f"  Version:      {info.module_version}")
    print(f"  Python:       {info.python_version}")
    print()
    print("  Platform:")
    print(f"    OS:         {info.platform.os_name}")
    print(f"    Arch:       {info.platform.architecture}")
    print()
    print("  Dependencies:")
    print(f"    Node.js:    {info.node_version or 'Not found'}")
    print(f"    Compiler:   {info.hostlist_compiler_version or 'Not found'}")
    if info.hostlist_compiler_path:
        print(f"    Path:       {info.hostlist_compiler_path}")
    print()


def run_benchmark(rule_count: int, max_parallel: int | None = None) -> int:
    """Run a synthetic benchmark to demonstrate chunking speedup."""
    import os
    import random
    import string
    import time
    from concurrent.futures import ThreadPoolExecutor, as_completed

    if max_parallel is None:
        max_parallel = min(os.cpu_count() or 4, 8)

    print()
    print("=" * 70)
    print("CHUNKING PERFORMANCE BENCHMARK")
    print("=" * 70)
    print(f"CPU cores available: {os.cpu_count()}")
    print(f"Max parallel workers: {max_parallel}")
    print(f"Simulating {rule_count:,} rules")
    print()

    # Generate synthetic rules
    print("Generating synthetic rules...", end=" ", flush=True)
    rules = []
    tlds = ["com", "net", "org", "io", "co"]
    words = ["ad", "ads", "track", "pixel", "banner", "promo", "click"]
    for _ in range(rule_count):
        word = random.choice(words)
        suffix = ''.join(random.choices(string.ascii_lowercase, k=4))
        tld = random.choice(tlds)
        rules.append(f"||{word}{suffix}.{tld}^")
    print("done")

    # Simulate sequential processing
    def simulate_processing(rule_list: list, chunk_id: int = 0) -> tuple[int, float]:
        """Simulate processing rules with realistic timing."""
        fixed_overhead_ms = 50
        per_rule_ms = 0.01  # 10ms per 1000 rules

        processing_time = fixed_overhead_ms + (len(rule_list) * per_rule_ms)
        variation = random.uniform(0.95, 1.15)
        processing_time *= variation

        # Scale down for demo (1% of actual time)
        time.sleep(processing_time / 1000 * 0.01)

        return len(rule_list), processing_time

    print("Running sequential benchmark...", end=" ", flush=True)
    start = time.perf_counter()
    _, sequential_time = simulate_processing(rules)
    print(f"done ({sequential_time:.0f}ms simulated)")

    # Simulate parallel processing
    print(f"Running parallel benchmark ({max_parallel} workers)...", end=" ", flush=True)
    chunk_size = (len(rules) + max_parallel - 1) // max_parallel
    chunks = [rules[i:i + chunk_size] for i in range(0, len(rules), chunk_size)]

    start = time.perf_counter()
    chunk_times = []

    with ThreadPoolExecutor(max_workers=max_parallel) as executor:
        futures = [executor.submit(simulate_processing, chunk, i) for i, chunk in enumerate(chunks)]
        for future in as_completed(futures):
            _, chunk_time = future.result()
            chunk_times.append(chunk_time)

    parallel_time = max(chunk_times)  # Parallel time is the slowest chunk
    print(f"done ({parallel_time:.0f}ms simulated)")

    # Calculate results
    speedup = sequential_time / parallel_time if parallel_time > 0 else 1.0
    efficiency = speedup / max_parallel
    time_saved = sequential_time - parallel_time

    print()
    print("-" * 70)
    print("RESULTS")
    print("-" * 70)
    print(f"Sequential time:     {sequential_time:,.0f} ms")
    print(f"Parallel time:       {parallel_time:,.0f} ms")
    print(f"Speedup:             {speedup:.2f}x")
    print(f"Efficiency:          {efficiency:.1%}")
    print(f"Time saved:          {time_saved:,.0f} ms")
    print()

    # Show scaling table
    print("Expected speedups at different scales:")
    print("-" * 50)
    print(f"{'Rules':<15} {'Sequential':<15} {'Parallel':<15} {'Speedup'}")
    print("-" * 50)

    test_sizes = [10_000, 50_000, 200_000, 500_000]
    for size in test_sizes:
        seq = 50 + (size * 0.01)  # Simulated time
        par = 50 + ((size / max_parallel) * 0.01)
        spd = seq / par
        print(f"{size:,}".ljust(15) + f"{seq:,.0f} ms".ljust(15) + f"{par:,.0f} ms".ljust(15) + f"{spd:.2f}x")

    print("-" * 50)
    print()
    print("Note: Actual speedup depends on:")
    print("  - Number of CPU cores")
    print("  - I/O performance (especially for network sources)")
    print("  - Rule complexity and transformations applied")
    print()

    return 0


def show_transformations() -> None:
    """Display available transformations."""
    print("Available Transformations:")
    print("-" * 40)
    print()

    descriptions = {
        "RemoveComments": "Remove comment lines (! or #)",
        "Compress": "Convert hosts format to adblock syntax",
        "RemoveModifiers": "Remove unsupported AdGuard modifiers",
        "Validate": "Remove dangerous/incompatible rules",
        "ValidateAllowIp": "Like Validate but allows IP rules",
        "Deduplicate": "Remove duplicate rules",
        "InvertAllow": "Convert @@ exceptions to blocking",
        "RemoveEmptyLines": "Remove blank lines",
        "TrimLines": "Trim leading/trailing whitespace",
        "InsertFinalNewLine": "Ensure file ends with newline",
        "ConvertToAscii": "Convert IDN to punycode",
    }

    for t in Transformation:
        desc = descriptions.get(t.value, "")
        print(f"  {t.value:<22} {desc}")

    print()
    print("Transformation Sets:")
    print("-" * 40)
    print()
    print("  Recommended:")
    for t in Transformation.recommended():
        print(f"    - {t.value}")
    print()
    print("  Minimal:")
    for t in Transformation.minimal():
        print(f"    - {t.value}")
    print()
    print("  Hosts File:")
    for t in Transformation.hosts_file():
        print(f"    - {t.value}")
    print()


def show_config(config_path: Path, format_override: str | None = None) -> int:
    """Display parsed configuration."""
    format_map = {
        "json": ConfigurationFormat.JSON,
        "yaml": ConfigurationFormat.YAML,
        "toml": ConfigurationFormat.TOML,
    }
    config_format = format_map.get(format_override) if format_override else None

    try:
        config = read_configuration(config_path, config_format)

        print("Configuration Details:")
        print("=" * 60)
        print()
        print(f"  File:            {config_path}")
        print(f"  Format:          {config._source_format.value if config._source_format else 'unknown'}")
        print()
        print(f"  Name:            {config.name}")
        if config.version:
            print(f"  Version:         {config.version}")
        if config.license:
            print(f"  License:         {config.license}")
        if config.description:
            print(f"  Description:     {config.description}")
        if config.homepage:
            print(f"  Homepage:        {config.homepage}")
        print()

        print(f"  Sources:         {len(config.sources)} total")
        print(f"    Local:         {config.local_sources_count()}")
        print(f"    Remote:        {config.remote_sources_count()}")
        print()

        if config.transformations:
            print(f"  Transformations: {', '.join(config.transformations)}")
        else:
            print("  Transformations: (none)")
        print()

        print("  Source Details:")
        print("  " + "-" * 56)
        for i, source in enumerate(config.sources):
            name = source.name or f"[{i}]"
            print(f"    {name}:")
            print(f"      Source: {source.source}")
            print(f"      Type:   {source.type}")
            if source.transformations:
                print(f"      Transforms: {', '.join(source.transformations)}")
        print()

        print("  JSON Representation:")
        print("  " + "-" * 56)
        json_str = to_json(config, indent=2)
        for line in json_str.split("\n"):
            print(f"    {line}")
        print()

        return 0

    except Exception as e:
        print(f"[ERROR] Failed to read configuration: {e}", file=sys.stderr)
        return 1


def validate_config(
    config_path: Path,
    format_override: str | None = None,
    check_files: bool = False,
) -> int:
    """Validate configuration file."""
    format_map = {
        "json": ConfigurationFormat.JSON,
        "yaml": ConfigurationFormat.YAML,
        "toml": ConfigurationFormat.TOML,
    }
    config_format = format_map.get(format_override) if format_override else None

    try:
        print(f"[INFO] Validating configuration: {config_path}")
        print()

        is_valid, errors, warnings = validate_configuration(
            config_path,
            format=config_format,
            check_files=check_files,
        )

        if errors:
            print("Errors:")
            for error in errors:
                print(f"  [ERROR] {error}")
            print()

        if warnings:
            print("Warnings:")
            for warning in warnings:
                print(f"  [WARN] {warning}")
            print()

        if is_valid:
            print("[OK] Configuration is valid")
            if warnings:
                print(f"     ({len(warnings)} warning(s))")
            return 0
        else:
            print(f"[FAIL] Configuration has {len(errors)} error(s)")
            return 1

    except Exception as e:
        print(f"[ERROR] Validation failed: {e}", file=sys.stderr)
        return 1


def find_default_config() -> Path | None:
    """Search for default configuration file."""
    search_paths = [
        Path.cwd() / "compiler-config.json",
        Path.cwd() / "compiler-config.yaml",
        Path.cwd() / "compiler-config.yml",
        Path.cwd() / "compiler-config.toml",
        Path.cwd() / "src" / "compilers" / "typescript" / "compiler-config.json",
    ]

    for path in search_paths:
        if path.exists():
            return path

    return None


def main(args: list[str] | None = None) -> int:
    """
    Main CLI entry point.

    Args:
        args: Command line arguments (defaults to sys.argv[1:]).

    Returns:
        Exit code (0 for success, 1 for failure).
    """
    parser = create_parser()
    opts = parser.parse_args(args)

    # Handle version
    if opts.version or opts.version_info:
        show_version()
        return 0

    # Handle transformations list
    if opts.transformations:
        show_transformations()
        return 0

    # Handle benchmark mode
    if opts.benchmark:
        return run_benchmark(opts.benchmark_rules, opts.benchmark_parallel)

    # Handle interactive mode
    if opts.interactive:
        from bloqr_compiler.interactive import run_interactive_menu
        
        # Try to determine initial config
        initial_config = None
        if opts.config_path:
            initial_config = Path(opts.config_path).resolve()
        elif opts.config:
            initial_config = Path(opts.config).resolve()
        else:
            found_path = find_default_config()
            if found_path:
                initial_config = found_path
        
        return run_interactive_menu(initial_config)

    # Determine config path (positional or flag)
    if opts.config_path:
        config_path = Path(opts.config_path).resolve()
    elif opts.config:
        config_path = Path(opts.config).resolve()
    else:
        # Search for default config
        found_path = find_default_config()
        if found_path:
            config_path = found_path
        else:
            print("Error: Configuration file not found. Searched:", file=sys.stderr)
            print("  - compiler-config.json", file=sys.stderr)
            print("  - compiler-config.yaml", file=sys.stderr)
            print("  - compiler-config.yml", file=sys.stderr)
            print("  - compiler-config.toml", file=sys.stderr)
            print("  - src/compilers/typescript/compiler-config.json", file=sys.stderr)
            print("\nSpecify config path with -c/--config or as positional argument", file=sys.stderr)
            return 1

    # Handle show-config
    if opts.show_config:
        return show_config(config_path, opts.format)

    # Handle validate
    if opts.validate:
        return validate_config(config_path, opts.format, opts.check_files)

    # Parse format
    format_map = {
        "json": ConfigurationFormat.JSON,
        "yaml": ConfigurationFormat.YAML,
        "toml": ConfigurationFormat.TOML,
    }
    config_format = format_map.get(opts.format) if opts.format else None

    # Determine validation settings
    should_validate = not opts.no_validate_config  # Default is True
    fail_on_warnings = opts.fail_on_warnings

    # Create compiler and run
    compiler = BloqrCompiler(debug=opts.debug)

    try:
        print(f"[INFO] Starting compilation with config: {config_path}")

        result = compiler.compile(
            config_path=config_path,
            output_path=opts.output,
            copy_to_rules=opts.copy_to_rules,
            rules_directory=opts.rules_dir,
            format=config_format,
            validate=should_validate,
            fail_on_warnings=fail_on_warnings,
        )

        if result.success:
            print()
            print("Results:")
            print(f"  Config Name:  {result.config_name}")
            print(f"  Config Ver:   {result.config_version}")
            print(f"  Rule Count:   {result.rule_count:,}")
            print(f"  Output Path:  {result.output_path}")
            print(f"  Hash:         {result.hash_short()}...")
            print(f"  Elapsed:      {result.elapsed_formatted()}")

            if result.copied_to_rules:
                print(f"  Copied To:    {result.rules_destination}")

            print()
            print("[INFO] Done!")
            return 0
        else:
            print(f"[ERROR] Compilation failed: {result.error_message}", file=sys.stderr)
            return 1

    except Exception as e:
        print(f"[ERROR] {e}", file=sys.stderr)
        if opts.debug:
            import traceback
            traceback.print_exc()
        return 1


if __name__ == "__main__":
    sys.exit(main())
