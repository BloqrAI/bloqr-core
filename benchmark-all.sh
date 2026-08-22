#!/bin/bash
# Root-level benchmark comparison script for ad-blocking repository
#
# Runs every available language's native `benchmark` command (Rust/.NET/TypeScript/
# Python/PowerShell), collects their JSON output, prints a comparison table, and writes
# a combined JSON summary. Skips any language whose toolchain isn't installed, matching
# launcher.sh's tool-detection convention. See benchmarks/README.md for the shared data/
# JSON-output contract these commands follow, and issue #421.

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

SIZE="all"
SOURCES=4
MAX_PARALLEL=""
OUTPUT=""
LANGUAGES="rust,dotnet,typescript,python,powershell"

usage() {
    cat << EOF
Usage: $0 [OPTIONS]

Run every available language's native benchmark command, compare chunked vs unchunked
real compilation performance, and write a combined JSON summary.

OPTIONS:
    --size SIZE          Dataset size: small, medium, large, xlarge, or all (default: all)
    --sources N           Identical duplicated sources for the chunked run (default: 4)
    --max-parallel N      Max parallel workers for the chunked run (default: each language's own default)
    --languages LIST      Comma-separated subset to run (default: rust,dotnet,typescript,python,powershell)
    --output PATH         Path for the combined JSON summary (default: benchmarks/results/benchmark-all-<timestamp>.json)
    -h, --help            Show this help message

EXAMPLES:
    $0                                    # Run all five, all dataset sizes
    $0 --size small                       # Just the small dataset, all languages
    $0 --languages rust,python            # Only Rust and Python
    $0 --sources 8 --max-parallel 8       # Wider chunked run

EOF
    exit "${1:-0}"
}

while [[ $# -gt 0 ]]; do
    case $1 in
        --size) SIZE="$2"; shift 2 ;;
        --sources) SOURCES="$2"; shift 2 ;;
        --max-parallel) MAX_PARALLEL="$2"; shift 2 ;;
        --languages) LANGUAGES="$2"; shift 2 ;;
        --output) OUTPUT="$2"; shift 2 ;;
        -h|--help) usage 0 ;;
        *) echo -e "${RED}Unknown option: $1${NC}"; usage 1 ;;
    esac
done

is_selected() {
    [[ ",$LANGUAGES," == *",$1,"* ]]
}

RESULTS_DIR="$SCRIPT_DIR/benchmarks/results"
mkdir -p "$RESULTS_DIR"
TIMESTAMP="$(date -u +%Y%m%d-%H%M%S)"
COMBINED_FILE="${OUTPUT:-$RESULTS_DIR/benchmark-all-$TIMESTAMP.json}"

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

# Word-split intentionally below (SC2086): each of these expands to either nothing, or a
# "--flag value" pair that needs to land as two separate positional arguments.
MAX_PARALLEL_ARGS_DOTNET=""
MAX_PARALLEL_ARGS_TS=""
MAX_PARALLEL_ARGS_PY=""
MAX_PARALLEL_ARGS_RUST=""
if [[ -n "$MAX_PARALLEL" ]]; then
    MAX_PARALLEL_ARGS_DOTNET="--benchmark-max-parallel $MAX_PARALLEL"
    MAX_PARALLEL_ARGS_TS="--benchmark-max-parallel $MAX_PARALLEL"
    MAX_PARALLEL_ARGS_PY="--benchmark-max-parallel $MAX_PARALLEL"
    MAX_PARALLEL_ARGS_RUST="--max-parallel $MAX_PARALLEL"
fi

echo -e "${CYAN}======================================================================${NC}"
echo -e "${CYAN}BENCHMARK COMPARISON (real per-language compiler pipelines)${NC}"
echo -e "${CYAN}======================================================================${NC}"
echo -e "Size: ${SIZE}   Sources: ${SOURCES}   Languages: ${LANGUAGES}"
echo ""

# Rust
if is_selected rust; then
    if command -v cargo &> /dev/null; then
        echo -e "${BLUE}--- Rust ---${NC}"
        if cargo build --release -p bloqr-compiler -q 2>&1 | tail -20; then
            # shellcheck disable=SC2086
            if ./target/release/bloqr-compiler benchmark --size "$SIZE" --sources "$SOURCES" $MAX_PARALLEL_ARGS_RUST --json > "$TMP_DIR/rust.json" 2>"$TMP_DIR/rust.err"; then
                echo -e "${GREEN}✓ Rust benchmark complete${NC}"
            else
                echo -e "${YELLOW}⚠ Rust benchmark failed:${NC}"
                cat "$TMP_DIR/rust.err"
            fi
        else
            echo -e "${YELLOW}⚠ Rust build failed, skipping${NC}"
        fi
    else
        echo -e "${YELLOW}⚠ cargo not found, skipping Rust${NC}"
    fi
    echo ""
fi

# .NET
if is_selected dotnet; then
    if command -v dotnet &> /dev/null; then
        echo -e "${BLUE}--- .NET ---${NC}"
        # shellcheck disable=SC2086
        if (cd src/compilers/dotnet && dotnet run -c Release --project src/Bloqr.Compiler.Dotnet.Console -- \
            --benchmark --benchmark-size "$SIZE" --benchmark-sources "$SOURCES" $MAX_PARALLEL_ARGS_DOTNET --benchmark-json \
            > "$TMP_DIR/dotnet.json" 2>"$TMP_DIR/dotnet.err"); then
            echo -e "${GREEN}✓ .NET benchmark complete${NC}"
        else
            echo -e "${YELLOW}⚠ .NET benchmark failed:${NC}"
            cat "$TMP_DIR/dotnet.err"
        fi
    else
        echo -e "${YELLOW}⚠ dotnet not found, skipping .NET${NC}"
    fi
    echo ""
fi

# TypeScript
if is_selected typescript; then
    if command -v deno &> /dev/null; then
        echo -e "${BLUE}--- TypeScript ---${NC}"
        # shellcheck disable=SC2086
        if (cd src/compilers/typescript && deno run --allow-read --allow-write --allow-env --allow-net --allow-run src/mod.ts \
            --benchmark --benchmark-size "$SIZE" --benchmark-sources "$SOURCES" $MAX_PARALLEL_ARGS_TS --benchmark-json \
            > "$TMP_DIR/typescript.json" 2>"$TMP_DIR/typescript.err"); then
            echo -e "${GREEN}✓ TypeScript benchmark complete${NC}"
        else
            echo -e "${YELLOW}⚠ TypeScript benchmark failed:${NC}"
            cat "$TMP_DIR/typescript.err"
        fi
    else
        echo -e "${YELLOW}⚠ deno not found, skipping TypeScript${NC}"
    fi
    echo ""
fi

# Python
if is_selected python; then
    if command -v bloqr-compiler &> /dev/null; then
        echo -e "${BLUE}--- Python ---${NC}"
        # shellcheck disable=SC2086
        if bloqr-compiler --benchmark --benchmark-size "$SIZE" --benchmark-sources "$SOURCES" $MAX_PARALLEL_ARGS_PY --benchmark-json \
            > "$TMP_DIR/python.json" 2>"$TMP_DIR/python.err"; then
            echo -e "${GREEN}✓ Python benchmark complete${NC}"
        else
            echo -e "${YELLOW}⚠ Python benchmark failed:${NC}"
            cat "$TMP_DIR/python.err"
        fi
    else
        echo -e "${YELLOW}⚠ bloqr-compiler console script not found on PATH (pip install -e src/compilers/python), skipping Python${NC}"
    fi
    echo ""
fi

# PowerShell
if is_selected powershell; then
    if command -v pwsh &> /dev/null; then
        echo -e "${BLUE}--- PowerShell ---${NC}"
        PS_MAX_PARALLEL_ARG=""
        [[ -n "$MAX_PARALLEL" ]] && PS_MAX_PARALLEL_ARG="-MaxParallel $MAX_PARALLEL"
        if pwsh -NoProfile -Command "
            \$ErrorActionPreference = 'Stop'
            Import-Module '$SCRIPT_DIR/src/compilers/powershell/Common/Common.psd1' -Force
            Import-Module '$SCRIPT_DIR/src/compilers/powershell/BloqrCompiler/BloqrCompiler.psd1' -Force
            Invoke-BloqrCompilerBenchmark -Size '$SIZE' -Sources $SOURCES $PS_MAX_PARALLEL_ARG -AsJson
        " > "$TMP_DIR/powershell.json" 2>"$TMP_DIR/powershell.err"; then
            echo -e "${GREEN}✓ PowerShell benchmark complete${NC}"
        else
            echo -e "${YELLOW}⚠ PowerShell benchmark failed:${NC}"
            cat "$TMP_DIR/powershell.err"
        fi
    else
        echo -e "${YELLOW}⚠ pwsh not found, skipping PowerShell${NC}"
    fi
    echo ""
fi

# Merge whatever JSON files landed in $TMP_DIR (each an array of per-size results, tagged
# with its own language) into one combined summary, and print a comparison table.
python3 - "$TMP_DIR" "$COMBINED_FILE" << 'PYEOF'
import json
import sys
from pathlib import Path

tmp_dir, combined_file = Path(sys.argv[1]), Path(sys.argv[2])

combined = []
for name in ("rust", "dotnet", "typescript", "python", "powershell"):
    path = tmp_dir / f"{name}.json"
    if not path.exists():
        continue
    try:
        results = json.loads(path.read_text())
    except json.JSONDecodeError:
        print(f"[WARN] Could not parse {name} benchmark output as JSON, skipping")
        continue
    for r in results:
        r["language"] = name
        combined.append(r)

combined_file.parent.mkdir(parents=True, exist_ok=True)
combined_file.write_text(json.dumps(combined, indent=2))

if not combined:
    print("No benchmark results collected - nothing to compare.")
    sys.exit(0)

print("-" * 100)
print("RESULTS")
print("-" * 100)
print(f"{'Language':<12} {'Size':<8} {'Unchunked':<12} {'Chunked':<12} {'Speedup':<10} {'Rules':<10} {'Status'}")
print("-" * 100)
for r in combined:
    if r.get("error") and not r.get("unchunkedSuccess") and not r.get("chunkedSuccess"):
        print(f"{r['language']:<12} {r['size']:<8} FAILED: {r['error'][:60]}")
        continue
    speedup = f"{r['speedup']:.2f}x" if r.get("speedup") is not None else "n/a"
    status = "ok" if r.get("unchunkedSuccess") and r.get("chunkedSuccess") else "partial"
    print(
        f"{r['language']:<12} {r['size']:<8} {str(r.get('unchunkedMs', 0)) + 'ms':<12} "
        f"{str(r.get('chunkedMs', 0)) + 'ms':<12} {speedup:<10} {r.get('chunkedRuleCount', 0):<10} {status}"
    )
print("-" * 100)
print(f"\nCombined summary written to: {combined_file}")
PYEOF
