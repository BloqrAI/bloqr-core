#!/usr/bin/env zsh
#
# compile-rules.zsh - Zsh script for compiling AdGuard filter rules
#
# This script provides a Unix/Linux/macOS interface to the hostlist-compiler,
# supporting JSON, YAML, and TOML configuration formats.
#
# Usage:
#   ./compile-rules.zsh [OPTIONS]
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
#   ./compile-rules.zsh
#   ./compile-rules.zsh -c config.yaml -r
#   ./compile-rules.zsh --config config.toml --output my-rules.txt
#
# Author: jaypatrick
# License: GPLv3

# Enable extended globbing and error handling
setopt ERR_EXIT PIPE_FAIL NO_UNSET EXTENDED_GLOB

# Script version
typeset -r VERSION="1.0.0"

# Color codes for output
typeset -r RED=$'\e[0;31m'
typeset -r GREEN=$'\e[0;32m'
typeset -r YELLOW=$'\e[0;33m'
typeset -r BLUE=$'\e[0;34m'
typeset -r NC=$'\e[0m'

# Get script directory using zsh-specific syntax
typeset -r SCRIPT_DIR="${0:a:h}"
typeset -r PROJECT_ROOT="${SCRIPT_DIR:h:h:h}"
typeset -r DEFAULT_CONFIG="${PROJECT_ROOT}/src/rules-compiler-typescript/compiler-config.json"
typeset -r DEFAULT_RULES_DIR="${PROJECT_ROOT}/rules"
typeset -r DEFAULT_OUTPUT_FILE="adguard_user_filter.txt"

# Configuration variables (will be overridden by environment variables if set)
typeset CONFIG_PATH="${ADGUARD_COMPILER_CONFIG:-}"
typeset OUTPUT_PATH="${ADGUARD_COMPILER_OUTPUT:-}"
typeset -i COPY_TO_RULES=0
typeset FORMAT="${ADGUARD_COMPILER_FORMAT:-}"
typeset -i DEBUG=0

# Check for DEBUG environment variable
if [[ "${DEBUG:-}" == "1" ]] || [[ "${DEBUG:l}" == "true" ]]; then
    DEBUG=1
fi

# Check for COPY_TO_RULES environment variable
if (( ${+ADGUARD_COMPILER_COPY_TO_RULES} )); then
    if [[ "${ADGUARD_COMPILER_COPY_TO_RULES}" == "1" ]] || [[ "${ADGUARD_COMPILER_COPY_TO_RULES:l}" == "true" ]]; then
        COPY_TO_RULES=1
    fi
fi

# Check for verbose environment variable
if (( ${+ADGUARD_COMPILER_VERBOSE} )); then
    if [[ "${ADGUARD_COMPILER_VERBOSE}" == "1" ]] || [[ "${ADGUARD_COMPILER_VERBOSE:l}" == "true" ]]; then
        setopt XTRACE
        DEBUG=1
    fi
fi

# Logging functions
log_info() {
    print -P "${GREEN}[INFO]${NC} $(date -u '+%Y-%m-%dT%H:%M:%SZ') - $*"
}

log_warn() {
    print -P "${YELLOW}[WARN]${NC} $(date -u '+%Y-%m-%dT%H:%M:%SZ') - $*" >&2
}

log_error() {
    print -P "${RED}[ERROR]${NC} $(date -u '+%Y-%m-%dT%H:%M:%SZ') - $*" >&2
}

log_debug() {
    if (( DEBUG )); then
        print -P "${BLUE}[DEBUG]${NC} $(date -u '+%Y-%m-%dT%H:%M:%SZ') - $*"
    fi
}

# Show help message
show_help() {
    cat <<EOF
AdGuard Filter Rules Compiler (Zsh API)

Usage: ${0:t} [OPTIONS]

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
  ${0:t}                           # Use default config
  ${0:t} -c config.yaml -r         # Use YAML config, copy to rules
  ${0:t} --config config.toml      # Use TOML config
  ${0:t} -v                        # Show version info

EOF
}

# Show version information
show_version() {
    print "AdGuard Filter Rules Compiler (Zsh API)"
    print "Version: ${VERSION}"
    print ""
    print "Platform Information:"
    print "  OS: $(uname -s)"
    print "  Architecture: $(uname -m)"
    print "  Shell: zsh ${ZSH_VERSION}"
    print ""

    # Check Node.js
    if (( $+commands[node] )); then
        print "  Node.js: $(node --version)"
    else
        print "  Node.js: Not found"
    fi

    # Check hostlist-compiler
    if (( $+commands[hostlist-compiler] )); then
        print "  hostlist-compiler: $(hostlist-compiler --version 2>/dev/null || print 'installed')"
    elif (( $+commands[npx] )); then
        print "  hostlist-compiler: Available via npx"
    else
        print "  hostlist-compiler: Not found"
    fi
}

# Detect configuration format from file extension
detect_format() {
    local file_path="$1"
    local extension="${file_path:e:l}"  # :e = extension, :l = lowercase

    case "${extension}" in
        json)
            print "json"
            ;;
        yaml|yml)
            print "yaml"
            ;;
        toml)
            print "toml"
            ;;
        *)
            log_error "Unknown configuration file extension: .${extension}"
            return 1
            ;;
    esac
}

# Convert YAML to JSON (requires yq or python)
yaml_to_json() {
    local yaml_file="$1"

    if (( $+commands[yq] )); then
        yq -o=json '.' "$yaml_file"
    elif (( $+commands[python3] )); then
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
        return 1
    fi
}

# Convert TOML to JSON (requires python with tomllib/toml)
toml_to_json() {
    local toml_file="$1"

    if (( $+commands[python3] )); then
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
        return 1
    fi
}

# Get the compiler command
get_compiler_command() {
    if (( $+commands[hostlist-compiler] )); then
        print "hostlist-compiler"
    elif (( $+commands[npx] )); then
        print "npx @adguard/hostlist-compiler"
    else
        log_error "hostlist-compiler not found. Install with: npm install -g @adguard/hostlist-compiler"
        return 1
    fi
}

# Count non-comment, non-empty lines in a file
count_rules() {
    local file_path="$1"
    grep -cvE '^\s*(#|!|$)' "$file_path" 2>/dev/null || print "0"
}

# Compute SHA-384 hash of a file
compute_hash() {
    local file_path="$1"

    if (( $+commands[sha384sum] )); then
        sha384sum "$file_path" | awk '{print $1}'
    elif (( $+commands[shasum] )); then
        shasum -a 384 "$file_path" | awk '{print $1}'
    else
        print "hash-unavailable"
    fi
}

# Locate the bloqr-validate CLI binary (from src/validation/).
# Resolution order: RULES_VALIDATE_PATH env override, then PATH, then a
# dev-convenience fallback to the Cargo workspace's target/{release,debug}
# output. Prints nothing and returns non-zero when not found, so callers can
# degrade gracefully.
find_rules_validate_binary() {
    if [[ -n "${RULES_VALIDATE_PATH:-}" && -f "${RULES_VALIDATE_PATH}" ]]; then
        print "${RULES_VALIDATE_PATH}"
        return 0
    fi

    if (( $+commands[bloqr-validate] )); then
        print "${commands[bloqr-validate]}"
        return 0
    fi

    local candidate profile
    for profile in release debug; do
        candidate="${PROJECT_ROOT}/target/${profile}/bloqr-validate"
        if [[ -f "${candidate}" ]]; then
            print "${candidate}"
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

    hash_db="${output_path:h}/.hashes.json"
    json_output=$("${binary}" --json file "${output_path}" --hash-db "${hash_db}" 2>/dev/null) || true

    if [[ -z "${json_output:-}" ]]; then
        log_debug "bloqr-validate produced no output; skipping syntax validation"
        return 0
    fi

    if (( ! $+commands[python3] )); then
        log_debug "python3 not found; cannot parse bloqr-validate output"
        return 0
    fi

    report=$(print -r -- "${json_output}" | python3 -c '
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

    if [[ -z "${report:-}" ]]; then
        log_debug "bloqr-validate produced output that could not be parsed as JSON; skipping"
        return 0
    fi

    local line severity text
    while IFS= read -r line; do
        [[ -z "${line}" ]] && continue
        severity="${line%%|*}"
        text="${line#*|}"
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

    local start_time=$EPOCHREALTIME

    log_info "Starting filter compilation..."
    log_debug "Config: ${config_path}"
    log_debug "Output: ${output_path}"

    # Verify config exists
    if [[ ! -f "${config_path}" ]]; then
        log_error "Configuration file not found: ${config_path}"
        return 1
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
        temp_config="${TMPDIR:-/tmp}/compile-rules-$$.json"
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
    local output_dir="${output_path:h}"
    mkdir -p "${output_dir}"

    # Get compiler command
    local compiler_cmd=$(get_compiler_command)
    log_debug "Using compiler: ${compiler_cmd}"

    # Run compilation
    log_info "Running hostlist-compiler..."

    if ${=compiler_cmd} --config "${json_config}" --output "${output_path}"; then
        local end_time=$EPOCHREALTIME
        local elapsed=$(( (end_time - start_time) * 1000 ))
        elapsed=${elapsed%.*}  # Remove decimal

        # Get statistics
        local rule_count=$(count_rules "${output_path}")
        local output_hash=$(compute_hash "${output_path}")

        # Findings are informational and never fail compilation
        run_rules_validator "${output_path}"

        log_info "Compilation successful!"
        print ""
        print "Results:"
        print "  Rule Count:  ${rule_count}"
        print "  Output Path: ${output_path}"
        print "  Hash:        ${output_hash[1,32]}..."
        print "  Elapsed:     ${elapsed}ms"

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

# Parse command line arguments using zparseopts
parse_args() {
    local -a config output format version help debug copy_to_rules_flag

    zparseopts -D -E -K -- \
        c:=config -config:=config \
        o:=output -output:=output \
        f:=format -format:=format \
        r=copy_to_rules_flag -copy-to-rules=copy_to_rules_flag \
        v=version -version=version \
        h=help -help=help \
        d=debug -debug=debug

    # Process parsed options
    if (( ${#help} )); then
        show_help
        exit 0
    fi

    if (( ${#version} )); then
        show_version
        exit 0
    fi

    if (( ${#debug} )); then
        DEBUG=1
    fi

    if (( ${#copy_to_rules_flag} )); then
        COPY_TO_RULES=1
    fi

    if (( ${#config} )); then
        CONFIG_PATH="${config[-1]}"
    fi

    if (( ${#output} )); then
        OUTPUT_PATH="${output[-1]}"
    fi

    if (( ${#format} )); then
        FORMAT="${format[-1]}"
    fi

    # Check for unknown arguments
    if (( $# > 0 )); then
        log_error "Unknown option: $1"
        show_help
        exit 1
    fi
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
        OUTPUT_PATH="${PROJECT_ROOT}/src/rules-compiler-typescript/output/compiled-${timestamp}.txt"
    fi

    # Run compilation
    if compile_rules "${CONFIG_PATH}" "${OUTPUT_PATH}" "${FORMAT}"; then
        # Copy to rules if requested
        if (( COPY_TO_RULES )); then
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
