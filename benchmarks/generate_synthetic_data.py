#!/usr/bin/env python3
"""
Generate synthetic test data for benchmarking the chunking feature.

This script creates filter list files of varying sizes to test the performance
of the chunking/parallel compilation feature across all compiler implementations.

Usage:
    python generate_synthetic_data.py [--sizes small,medium,large,xlarge]
    python generate_synthetic_data.py --all
    python generate_synthetic_data.py --custom 500000

Sizes:
    small   =  10,000 rules  (baseline, no chunking benefit)
    medium  =  50,000 rules  (moderate chunking benefit)
    large   = 200,000 rules  (significant chunking benefit)
    xlarge  = 500,000 rules  (maximum chunking benefit)
"""

import argparse
import hashlib
import json
import os
import random
import string
import sys
from datetime import datetime
from pathlib import Path

# Size presets
SIZES = {
    "small": 10_000,
    "medium": 50_000,
    "large": 200_000,
    "xlarge": 500_000,
}

# TLDs for synthetic domains
TLDS = ["com", "net", "org", "io", "co", "app", "dev", "xyz", "info", "biz"]

# Ad-related words for realistic domain generation
AD_WORDS = [
    "ad", "ads", "adserver", "advert", "banner", "click", "track", "tracker",
    "pixel", "beacon", "analytics", "metrics", "stat", "stats", "count",
    "counter", "impression", "conversion", "retarget", "syndicate", "widget",
    "pop", "popup", "overlay", "interstitial", "sponsor", "promo", "affiliate",
    "partner", "campaign", "cpc", "cpm", "cpa", "bid", "auction", "rtb",
]

# Common second-level domain patterns
DOMAIN_PATTERNS = [
    "{word}s",
    "{word}-{word2}",
    "get{word}",
    "my{word}",
    "{word}hub",
    "{word}ly",
    "{word}io",
    "{word}network",
    "{word}media",
    "{word}zone",
]


def generate_random_string(length: int) -> str:
    """Generate a random alphanumeric string."""
    return "".join(random.choices(string.ascii_lowercase + string.digits, k=length))


def generate_domain() -> str:
    """Generate a realistic-looking ad domain."""
    pattern = random.choice(DOMAIN_PATTERNS)
    word = random.choice(AD_WORDS)
    word2 = random.choice(AD_WORDS)
    tld = random.choice(TLDS)

    domain = pattern.format(word=word, word2=word2)
    # Sometimes add a random subdomain
    if random.random() < 0.3:
        subdomain = generate_random_string(random.randint(3, 8))
        domain = f"{subdomain}.{domain}"

    return f"{domain}.{tld}"


def generate_adblock_rule() -> str:
    """Generate a synthetic AdBlock-style rule."""
    rule_type = random.random()

    if rule_type < 0.5:
        # Basic domain block: ||domain.com^
        return f"||{generate_domain()}^"
    elif rule_type < 0.7:
        # URL pattern block: ||domain.com/path
        path = generate_random_string(random.randint(5, 15))
        return f"||{generate_domain()}/{path}"
    elif rule_type < 0.85:
        # With modifiers: ||domain.com^$third-party
        modifiers = random.choice(["$third-party", "$script", "$image", "$popup", "$third-party,script"])
        return f"||{generate_domain()}^{modifiers}"
    else:
        # Exception rule: @@||domain.com^
        return f"@@||{generate_domain()}^"


def generate_comment() -> str:
    """Generate a realistic comment."""
    comment_types = [
        "! Title: Synthetic Test Filter",
        "! Homepage: https://example.com",
        "! License: MIT",
        f"! Last modified: {datetime.now().strftime('%Y-%m-%d')}",
        "! Description: Synthetic test data for benchmarking",
        f"! Section: {random.choice(['Ads', 'Trackers', 'Analytics', 'Social', 'Annoyances'])}",
    ]
    return random.choice(comment_types)


def generate_filter_list(num_rules: int) -> list[str]:
    """Generate a synthetic filter list with the specified number of rules."""
    lines = []

    # Add header comments
    lines.append("! Title: Synthetic Benchmark Filter")
    lines.append(f"! Rules: {num_rules}")
    lines.append(f"! Generated: {datetime.now().isoformat()}")
    lines.append("! Homepage: https://github.com/BloqrAI/bloqr-lists")
    lines.append("!")
    lines.append("")

    # Generate rules with occasional comments and empty lines
    rules_generated = 0
    section_size = num_rules // 10  # 10 sections

    while rules_generated < num_rules:
        # Start a new section occasionally
        if rules_generated % section_size == 0 and rules_generated > 0:
            lines.append("")
            lines.append(f"! === Section {rules_generated // section_size} ===")
            lines.append("")

        # Add a rule
        lines.append(generate_adblock_rule())
        rules_generated += 1

        # Occasionally add an empty line (5% chance)
        if random.random() < 0.05:
            lines.append("")

    return lines


def write_filter_file(output_dir: Path, name: str, num_rules: int) -> dict:
    """Generate and write a filter list file, return metadata."""
    print(f"Generating {name} ({num_rules:,} rules)...", end=" ", flush=True)

    lines = generate_filter_list(num_rules)
    content = "\n".join(lines)

    # Write the filter file
    filter_path = output_dir / f"{name}.txt"
    filter_path.write_text(content, encoding="utf-8")

    # Calculate stats
    file_size = filter_path.stat().st_size
    content_hash = hashlib.sha256(content.encode()).hexdigest()[:16]

    print(f"done ({file_size / 1024:.1f} KB)")

    return {
        "name": name,
        "path": str(filter_path),
        "rules": num_rules,
        "lines": len(lines),
        "size_bytes": file_size,
        "size_kb": round(file_size / 1024, 1),
        "hash": content_hash,
    }


def create_config_files(output_dir: Path, filter_files: list[dict]) -> None:
    """Create configuration files for each filter list in multiple formats."""
    print("\nCreating configuration files...")

    for filter_info in filter_files:
        name = filter_info["name"]
        source_path = filter_info["path"]

        # Base configuration
        config = {
            "name": f"Synthetic Benchmark - {name.title()}",
            "description": f"Synthetic {filter_info['rules']:,} rule filter list for benchmarking",
            "version": "1.0.0",
            "homepage": "https://github.com/BloqrAI/bloqr-lists",
            "license": "MIT",
            "sources": [
                {
                    "name": f"{name}-source",
                    "source": source_path.replace("\\", "/"),
                    "type": "adblock"
                }
            ],
            "transformations": ["Deduplicate", "RemoveEmptyLines", "TrimLines"]
        }

        # Write JSON config
        json_path = output_dir / f"config-{name}.json"
        json_path.write_text(json.dumps(config, indent=2), encoding="utf-8")

        print(f"  Created: {json_path.name}")


def create_multi_source_configs(output_dir: Path, filter_files: list[dict]) -> None:
    """Create multi-source configurations for testing parallel chunking."""
    print("\nCreating multi-source configurations...")

    # Create configs with 2, 4, and 8 sources
    for num_sources in [2, 4, 8]:
        # Use the medium filter files repeated
        if len(filter_files) == 0:
            continue

        # Pick a filter to duplicate as multiple sources
        base_filter = filter_files[1] if len(filter_files) > 1 else filter_files[0]

        sources = []
        for i in range(num_sources):
            sources.append({
                "name": f"source-{i+1}",
                "source": base_filter["path"].replace("\\", "/"),
                "type": "adblock"
            })

        config = {
            "name": f"Multi-Source Benchmark ({num_sources} sources)",
            "description": f"Benchmark with {num_sources} parallel sources",
            "version": "1.0.0",
            "sources": sources,
            "transformations": ["Deduplicate", "RemoveEmptyLines"]
        }

        json_path = output_dir / f"config-multi-{num_sources}sources.json"
        json_path.write_text(json.dumps(config, indent=2), encoding="utf-8")
        print(f"  Created: {json_path.name}")


def generate_expected_speedups() -> dict:
    """Calculate expected speedups for documentation."""
    cpu_count = os.cpu_count() or 4

    return {
        "system_info": {
            "cpu_cores": cpu_count,
            "recommended_max_parallel": min(cpu_count, 8),
        },
        "expected_speedups": {
            "single_source": {
                "description": "Single source file, chunking by line count",
                "10k_rules": 1.0,
                "50k_rules": 1.0,
                "200k_rules": f"{min(2, cpu_count):.1f}x",
                "500k_rules": f"{min(5, cpu_count):.1f}x",
            },
            "multi_source": {
                "description": "Multiple source files, chunking by source",
                "2_sources": f"{min(2, cpu_count):.1f}x",
                "4_sources": f"{min(4, cpu_count):.1f}x",
                "8_sources": f"{min(8, cpu_count):.1f}x",
            },
        },
        "notes": [
            "Actual speedup depends on CPU cores, I/O speed, and source complexity",
            "Network fetching is the main bottleneck for remote sources",
            "Local file sources show the best speedup from chunking",
            f"Your system has {cpu_count} CPU cores available",
        ],
    }


def main():
    parser = argparse.ArgumentParser(
        description="Generate synthetic test data for chunking benchmarks"
    )
    parser.add_argument(
        "--sizes",
        type=str,
        default="small,medium,large",
        help="Comma-separated list of sizes to generate (small,medium,large,xlarge)"
    )
    parser.add_argument(
        "--all",
        action="store_true",
        help="Generate all size presets"
    )
    parser.add_argument(
        "--custom",
        type=int,
        help="Generate a custom-sized filter list with this many rules"
    )
    parser.add_argument(
        "--output",
        type=str,
        default=None,
        help="Output directory (default: benchmarks/data)"
    )
    parser.add_argument(
        "--seed",
        type=int,
        default=42,
        help="Random seed for reproducibility"
    )

    args = parser.parse_args()

    # Set random seed
    random.seed(args.seed)

    # Determine output directory
    script_dir = Path(__file__).parent
    output_dir = Path(args.output) if args.output else script_dir / "data"
    output_dir.mkdir(parents=True, exist_ok=True)

    print(f"Output directory: {output_dir}")
    print(f"Random seed: {args.seed}")
    print()

    # Determine which sizes to generate
    sizes_to_generate = {}

    if args.all:
        sizes_to_generate = SIZES.copy()
    elif args.custom:
        sizes_to_generate = {"custom": args.custom}
    else:
        for size_name in args.sizes.split(","):
            size_name = size_name.strip().lower()
            if size_name in SIZES:
                sizes_to_generate[size_name] = SIZES[size_name]
            else:
                print(f"Warning: Unknown size '{size_name}', skipping")

    if not sizes_to_generate:
        print("Error: No valid sizes specified")
        sys.exit(1)

    # Generate filter files
    filter_files = []
    for name, num_rules in sizes_to_generate.items():
        info = write_filter_file(output_dir, name, num_rules)
        filter_files.append(info)

    # Create configuration files
    create_config_files(output_dir, filter_files)
    create_multi_source_configs(output_dir, filter_files)

    # Generate expected speedups info
    speedups = generate_expected_speedups()
    speedups_path = output_dir / "expected_speedups.json"
    speedups_path.write_text(json.dumps(speedups, indent=2), encoding="utf-8")
    print(f"\nCreated: {speedups_path.name}")

    # Create summary
    summary = {
        "generated_at": datetime.now().isoformat(),
        "seed": args.seed,
        "filter_files": filter_files,
        "total_rules": sum(f["rules"] for f in filter_files),
        "total_size_kb": sum(f["size_kb"] for f in filter_files),
    }

    summary_path = output_dir / "benchmark_data_summary.json"
    summary_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(f"Created: {summary_path.name}")

    # Print summary
    print("\n" + "=" * 60)
    print("SUMMARY")
    print("=" * 60)
    print(f"Total filter files: {len(filter_files)}")
    print(f"Total rules:        {summary['total_rules']:,}")
    print(f"Total size:         {summary['total_size_kb']:.1f} KB")
    print()
    print("Expected speedups on your system:")
    print(f"  CPU cores: {speedups['system_info']['cpu_cores']}")
    print(f"  With 4 sources: ~{min(4, speedups['system_info']['cpu_cores'])}x faster")
    print(f"  With 8 sources: ~{min(8, speedups['system_info']['cpu_cores'])}x faster")
    print()
    print("Next steps:")
    print("  1. Run: ../benchmark-all.sh (or ..\\benchmark-all.ps1 on Windows)")
    print("  2. Compare unchunked vs chunked compilation times")
    print("  3. Review results in benchmarks/results/")


if __name__ == "__main__":
    main()
