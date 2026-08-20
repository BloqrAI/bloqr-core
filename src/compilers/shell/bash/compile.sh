#!/usr/bin/env bash
#
# compile.sh - Cross-platform shell script for compiling AdGuard filter rules
#
# This script provides a Unix/Linux/macOS interface to the hostlist-compiler,
# supporting JSON, YAML, and TOML configuration formats.
#
# Usage:
#   ./compile.sh [OPTIONS]
#
# Options:
#   -c, --config PATH    Path to configuration file (default: compiler-config.json)
#   -o, --output PATH    Path to output file (default: output/compiled-TIMESTAMP.txt)
#   -r, --copy-to-rules  Copy output to rules directory
#   -f, --format FORMAT  Force configuration format (json, yaml, toml)
#   -v, --version        Show version information
#   -h, --help           Show this help message
#   -d, --debug          Enable debug output
#
# Examples:
#   ./compile.sh
#   ./compile.sh -c config.yaml -r
#   ./compile.sh --config config.toml --output my-rules.txt
#
# Author: jaypatrick
# License: GPLv3

set -euo pipefail

# Script version
readonly VERSION="1.0.0"

# Color codes for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[0;33m'
readonly BLUE='\033[0;34m'
readonly NC='\033[0m' # No Color

# Default paths
readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../../../.." && pwd)"
readonly DEFAULT_CONFIG="${PROJECT_ROOT}/src/compilers/typescript/compiler-config.json"
readonly DEFAULT_RULES_DIR="${PROJECT_ROOT}/rules"
readonly DEFAULT_OUTPUT_FILE="adguard_user_filter.txt"

# Configuration (will be overridden by environment variables if set)
CONFIG_PATH="${ADGUARD_COMPILER_CONFIG:-}"
OUTPUT_PATH="${ADGUARD_COMPILER_OUTPUT:-}"
COPY_TO_RULES=false
FORMAT="${ADGUARD_COMPILER_FORMAT:-}"
DEBUG=false

# Check for DEBUG environment variable
if [[ "${DEBUG:-}" == "1" ]] || [[ "${DEBUG:-}" == "true" ]]; then
    DEBUG=true
fi

# Check for COPY_TO_RULES environment variable
if [[ "${ADGUARD_COMPILER_COPY_TO_RULES:-}" == "1" ]] || [[ "${ADGUARD_COMPILER_COPY_TO_RULES:-}" == "true" ]]; then
    COPY_TO_RULES=true
fi

# Check for verbose environment variable
if [[ "${ADGUARD_COMPILER_VERBOSE:-}" == "1" ]] || [[ "${ADGUARD_COMPILER_VERBOSE:-}" == "true" ]]; then
    set -x
    DEBUG=true
fi

# Logging functions
log_info() {
    echo -e "${GREEN}[INFO]${NC} $(date -u '+%Y-%m-%dT%H:%M:%SZ') - $*"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $(date -u '+%Y-%m-%dT%H:%M:%SZ') - $*" >&2
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $(date -u '+%Y-%m-%dT%H:%M:%SZ') - $*" >&2
}

log_debug() {
    if [[ "${DEBUG}" == "true" ]]; then
        echo -e "${BLUE}[DEBUG]${NC} $(date -u '+%Y-%m-%dT%H:%M:%SZ') - $*"
    fi
}

# Show help message
show_help() {
    cat << EOF
AdGuard Filter Rules Compiler (Shell API)

Usage: $(basename "$0") [OPTIONS]

Options:
  -c, --config PATH    Path to configuration file (default: compiler-config.json)
  -o, --output PATH    Path to output file (default: output/compiled-TIMESTAMP.txt)
  -r, --copy-to-rules  Copy output to rules directory
  -f, --format FORMAT  Force configuration format (json, yaml, toml)
  -v, --version        Show version information
  -h, --help           Show this help message
  -d, --debug          Enable debug output

Supported Configuration Formats:
  - JSON (.json)
  - YAML (.yaml, .yml)
  - TOML (.toml)

Examples:
  $(basename "$0")                           # Use default config
  $(basename "$0") -c config.yaml -r         # Use YAML config, copy to rules
  $(basename "$0") --config config.toml      # Use TOML config
  $(basename "$0") -v                        # Show version info

EOF
}

# Show version information
show_version() {
    echo "AdGuard Filter Rules Compiler (Shell API)"
    echo "Version: ${VERSION}"
    echo ""
    echo "Platform Information:"
    echo "  OS: $(uname -s)"
    echo "  Architecture: $(uname -m)"
    echo "  Shell: ${SHELL:-unknown}"
    echo ""

    # Check Node.js
    if command -v node &> /dev/null; then
        echo "  Node.js: $(node --version)"
    else
        echo "  Node.js: Not found"
    fi

    # Check hostlist-compiler
    if command -v hostlist-compiler &> /dev/null; then
        echo "  hostlist-compiler: $(hostlist-compiler --version 2>/dev/null || echo 'installed')"
    elif command -v npx &> /dev/null; then
        echo "  hostlist-compiler: Available via npx"
    else
        echo "  hostlist-compiler: Not found"
    fi
}

# Detect configuration format from file extension
detect_format() {
    local file_path="$1"
    local extension="${file_path##*.}"

    case "${extension,,}" in
        json)
            echo "json"
            ;;
        yaml|yml)
            echo "yaml"
            ;;
        toml)
            echo "toml"
            ;;
        *)
            log_error "Unknown configuration file extension: .${extension}"
            exit 1
            ;;
    esac
}

# Convert YAML to JSON (requires yq or python)
yaml_to_json() {
    local yaml_file="$1"

    if command -v yq &> /dev/null; then
        yq -o=json '.' "$yaml_file"
    elif command -v python3 &> /dev/null; then
        python3 -c "
import sys, json
try:
    import yaml
    with open('$yaml_file', 'r') as f:
        data = yaml.safe_load(f)
    print(json.dumps(data, indent=2))
except ImportError:
    print('Error: PyYAML not installed. Run: pip install pyyaml', file=sys.stderr)
    sys.exit(1)
"
    else
        log_error "No YAML parser found. Install yq or python3 with PyYAML."
        exit 1
    fi
}

# Convert TOML to JSON (requires python with tomllib/toml)
toml_to_json() {
    local toml_file="$1"

    if command -v python3 &> /dev/null; then
        python3 -c "
import sys, json
try:
    import tomllib
    with open('$toml_file', 'rb') as f:
        data = tomllib.load(f)
    print(json.dumps(data, indent=2))
except ImportError:
    try:
        import toml
        with open('$toml_file', 'r') as f:
            data = toml.load(f)
        print(json.dumps(data, indent=2))
    except ImportError:
        print('Error: No TOML parser found. Requires Python 3.11+ or toml package.', file=sys.stderr)
        sys.exit(1)
"
    else
        log_error "Python3 required for TOML parsing."
        exit 1
    fi
}

# Get the compiler command
get_compiler_command() {
    if command -v hostlist-compiler &> /dev/null; then
        echo "hostlist-compiler"
    elif command -v npx &> /dev/null; then
        echo "npx @adguard/hostlist-compiler"
    else
        log_error "hostlist-compiler not found. Install with: npm install -g @adguard/hostlist-compiler"
        exit 1
    fi
}

# Count non-comment, non-empty lines in a file
count_rules() {
    local file_path="$1"
    grep -cvE '^\s*(#|!|$)' "$file_path" 2>/dev/null || echo "0"
}

# Compute SHA-384 hash of a file
compute_hash() {
    local file_path="$1"

    if command -v sha384sum &> /dev/null; then
        sha384sum "$file_path" | awk '{print $1}'
    elif command -v shasum &> /dev/null; then
        shasum -a 384 "$file_path" | awk '{print $1}'
    else
        echo "hash-unavailable"
    fi
}

# Locate the bloqr-validate CLI binary (from src/validation/).
# Resolution order: RULES_VALIDATE_PATH env override, then PATH, then a
# dev-convenience fallback to the Cargo workspace's target/{release,debug}
# output. Prints nothing and returns non-zero when not found, so callers can
# degrade gracefully.
find_rules_validate_binary() {
    if [[ -n "${RULES_VALIDATE_PATH:-}" && -f "${RULES_VALIDATE_PATH}" ]]; then
        echo "${RULES_VALIDATE_PATH}"
        return 0
    fi

    if command -v bloqr-validate &> /dev/null; then
        command -v bloqr-validate
        return 0
    fi

    local candidate profile
    for profile in release debug; do
        candidate="${PROJECT_ROOT}/target/${profile}/bloqr-validate"
        if [[ -f "${candidate}" ]]; then
            echo "${candidate}"
            return 0
        fi
    done

    return 1
}

# Runs the bloqr-validate CLI's syntax check against a compiled output file.
# Findings are informational: this never fails compilation, it only logs
# what the validator found. A missing binary, empty output, or output that
# can't be parsed as JSON is treated as a skip, not a failure.
run_rules_validator() {
    local output_path="$1"
    local binary hash_db json_output report

    if ! binary=$(find_rules_validate_binary); then
        log_debug "bloqr-validate binary not found; skipping syntax validation"
        return 0
    fi

    hash_db="$(dirname "${output_path}")/.hashes.json"
    json_output=$("${binary}" --json file "${output_path}" --hash-db "${hash_db}" 2>/dev/null) || true

    if [[ -z "${json_output}" ]]; then
        log_debug "bloqr-validate produced no output; skipping syntax validation"
        return 0
    fi

    if ! command -v python3 &> /dev/null; then
        log_debug "python3 not found; cannot parse bloqr-validate output"
        return 0
    fi

    report=$(printf '%s' "${json_output}" | python3 -c '
import json, sys
try:
    data = json.load(sys.stdin)
except Exception:
    sys.exit(0)

is_valid = bool(data.get("is_valid", False))
valid_rules = data.get("valid_rules", 0)
invalid_rules = data.get("invalid_rules", 0)
messages = data.get("messages") or []

if not messages and not is_valid:
    messages = [f"RV001: syntax validation failed ({valid_rules} valid, {invalid_rules} invalid rules)"]

severity = "INFO" if is_valid else "WARN"
print(f"{severity}|bloqr-validator: {valid_rules} valid, {invalid_rules} invalid rule(s)")
for m in messages:
    print(f"{severity}|{m}")
') || true

    if [[ -z "${report}" ]]; then
        log_debug "bloqr-validate produced output that could not be parsed as JSON; skipping"
        return 0
    fi

    local severity text
    while IFS='|' read -r severity text; do
        [[ -z "${severity}" ]] && continue
        if [[ "${severity}" == "WARN" ]]; then
            log_warn "${text}"
        else
            log_info "${text}"
        fi
    done <<< "${report}"

    return 0
}

# Main compilation function
compile_rules() {
    local config_path="$1"
    local output_path="$2"
    local format="$3"

    local start_time=$(date +%s%3N 2>/dev/null || date +%s)

    log_info "Starting filter compilation..."
    log_debug "Config: ${config_path}"
    log_debug "Output: ${output_path}"

    # Verify config exists
    if [[ ! -f "${config_path}" ]]; then
        log_error "Configuration file not found: ${config_path}"
        exit 1
    fi

    # Detect or use specified format
    if [[ -z "${format}" ]]; then
        format=$(detect_format "${config_path}")
    fi
    log_debug "Configuration format: ${format}"

    # Convert to JSON if needed
    local json_config="${config_path}"
    local temp_config=""

    if [[ "${format}" != "json" ]]; then
        temp_config=$(mktemp --suffix=.json)
        log_debug "Converting ${format} to JSON: ${temp_config}"

        case "${format}" in
            yaml)
                yaml_to_json "${config_path}" > "${temp_config}"
                ;;
            toml)
                toml_to_json "${config_path}" > "${temp_config}"
                ;;
        esac

        json_config="${temp_config}"
    fi

    # Ensure output directory exists
    local output_dir=$(dirname "${output_path}")
    mkdir -p "${output_dir}"

    # Get compiler command
    local compiler_cmd=$(get_compiler_command)
    log_debug "Using compiler: ${compiler_cmd}"

    # Run compilation
    log_info "Running hostlist-compiler..."

    if ${compiler_cmd} --config "${json_config}" --output "${output_path}"; then
        local end_time=$(date +%s%3N 2>/dev/null || date +%s)
        local elapsed=$((end_time - start_time))

        # Get statistics
        local rule_count=$(count_rules "${output_path}")
        local output_hash=$(compute_hash "${output_path}")

        # Findings are informational and never fail compilation
        run_rules_validator "${output_path}"

        log_info "Compilation successful!"
        echo ""
        echo "Results:"
        echo "  Rule Count:  ${rule_count}"
        echo "  Output Path: ${output_path}"
        echo "  Hash:        ${output_hash:0:32}..."
        echo "  Elapsed:     ${elapsed}ms"

        # Clean up temp file
        if [[ -n "${temp_config}" && -f "${temp_config}" ]]; then
            rm -f "${temp_config}"
        fi

        return 0
    else
        log_error "Compilation failed!"

        # Clean up temp file
        if [[ -n "${temp_config}" && -f "${temp_config}" ]]; then
            rm -f "${temp_config}"
        fi

        return 1
    fi
}

# Copy output to rules directory
copy_to_rules() {
    local source_path="$1"
    local dest_path="${DEFAULT_RULES_DIR}/${DEFAULT_OUTPUT_FILE}"

    log_info "Copying output to rules directory..."

    mkdir -p "${DEFAULT_RULES_DIR}"
    cp "${source_path}" "${dest_path}"

    log_info "Copied to: ${dest_path}"
}

# Parse command line arguments
parse_args() {
    while [[ $# -gt 0 ]]; do
        case "$1" in
            -c|--config)
                CONFIG_PATH="$2"
                shift 2
                ;;
            -o|--output)
                OUTPUT_PATH="$2"
                shift 2
                ;;
            -r|--copy-to-rules)
                COPY_TO_RULES=true
                shift
                ;;
            -f|--format)
                FORMAT="$2"
                shift 2
                ;;
            -v|--version)
                show_version
                exit 0
                ;;
            -h|--help)
                show_help
                exit 0
                ;;
            -d|--debug)
                DEBUG=true
                shift
                ;;
            *)
                log_error "Unknown option: $1"
                show_help
                exit 1
                ;;
        esac
    done
}

# Main entry point
main() {
    parse_args "$@"

    # Set defaults if not specified
    if [[ -z "${CONFIG_PATH}" ]]; then
        CONFIG_PATH="${DEFAULT_CONFIG}"
    fi

    if [[ -z "${OUTPUT_PATH}" ]]; then
        local timestamp=$(date -u '+%Y%m%d-%H%M%S')
        OUTPUT_PATH="${PROJECT_ROOT}/src/compilers/typescript/output/compiled-${timestamp}.txt"
    fi

    # Run compilation
    if compile_rules "${CONFIG_PATH}" "${OUTPUT_PATH}" "${FORMAT}"; then
        # Copy to rules if requested
        if [[ "${COPY_TO_RULES}" == "true" ]]; then
            copy_to_rules "${OUTPUT_PATH}"
        fi

        log_info "Done!"
        exit 0
    else
        exit 1
    fi
}

# Run main if executed directly
main "$@"
