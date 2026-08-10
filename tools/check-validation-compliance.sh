#!/bin/bash
# Validation Library Compliance Checker
# Verifies that all rules compilers are properly integrated with the validation library

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

ERRORS=0
WARNINGS=0

echo "╔═══════════════════════════════════════════════════════════╗"
echo "║   Validation Library Compliance Check                    ║"
echo "╚═══════════════════════════════════════════════════════════╝"
echo ""

# Function to check if validation library exists
check_validation_library() {
    echo "→ Checking validation library..."
    
    if [ ! -d "$REPO_ROOT/src/rules-validator" ]; then
        echo -e "${RED}✗ Validation library not found${NC}"
        ERRORS=$((ERRORS + 1))
        return 1
    fi

    # rules-validator-core/rules-validator-cli are workspace members of the root
    # Cargo.toml, not a standalone crate under src/rules-validator/ itself.
    if [ ! -f "$REPO_ROOT/src/rules-validator/rules-validator-core/Cargo.toml" ] \
        || [ ! -f "$REPO_ROOT/src/rules-validator/rules-validator-cli/Cargo.toml" ]; then
        echo -e "${RED}✗ Validation library Cargo.toml missing${NC}"
        ERRORS=$((ERRORS + 1))
        return 1
    fi
    
    echo -e "${GREEN}✓ Validation library exists${NC}"
    return 0
}

# Function to check TypeScript compiler integration
check_typescript_integration() {
    echo ""
    echo "→ Checking TypeScript compiler integration..."
    
    local ts_dir="$REPO_ROOT/src/adblock-compiler-core"
    
    if [ ! -d "$ts_dir" ]; then
        echo -e "${YELLOW}⚠ TypeScript compiler not found${NC}"
        WARNINGS=$((WARNINGS + 1))
        return 0
    fi
    
    # Check for validation library import in package.json (when integrated)
    # Currently this is aspirational - not yet implemented
    if grep -q "adguard.*validation" "$ts_dir/package.json" 2>/dev/null; then
        echo -e "${GREEN}✓ TypeScript: Validation library dependency found${NC}"
    else
        echo -e "${YELLOW}⚠ TypeScript: Validation library not yet integrated (pending Phase 2)${NC}"
        WARNINGS=$((WARNINGS + 1))
    fi
    
    # Check for validation calls in source code
    if grep -rq "validate_local_file\|validate_remote_url\|Validator" "$ts_dir/src" 2>/dev/null; then
        echo -e "${GREEN}✓ TypeScript: Validation calls found in source${NC}"
    else
        echo -e "${YELLOW}⚠ TypeScript: No validation calls found (pending Phase 2)${NC}"
        WARNINGS=$((WARNINGS + 1))
    fi
}

# Function to check .NET compiler integration
check_dotnet_integration() {
    echo ""
    echo "→ Checking .NET compiler integration..."
    
    local dotnet_dir="$REPO_ROOT/src/rules-compiler-dotnet"
    
    if [ ! -d "$dotnet_dir" ]; then
        echo -e "${YELLOW}⚠ .NET compiler not found${NC}"
        WARNINGS=$((WARNINGS + 1))
        return 0
    fi
    
    # Check for the P/Invoke wrapper (RulesValidatorService, #264) and its wiring
    # into the compilation pipeline (RulesCompilerService raising the RV001
    # validation code documented in docs/event-pipeline.md).
    if grep -rq "rules_validator_new\|IRulesValidatorService" "$dotnet_dir/src" 2>/dev/null \
        && grep -rq "RV001" "$dotnet_dir/src" 2>/dev/null; then
        echo -e "${GREEN}✓ .NET: Validation library integrated (#264)${NC}"
    else
        echo -e "${YELLOW}⚠ .NET: Validation library not yet integrated (pending Phase 3)${NC}"
        WARNINGS=$((WARNINGS + 1))
    fi
}

# Function to check Python compiler integration
check_python_integration() {
    echo ""
    echo "→ Checking Python compiler integration..."
    
    local python_dir="$REPO_ROOT/src/rules-compiler-python"
    
    if [ ! -d "$python_dir" ]; then
        echo -e "${YELLOW}⚠ Python compiler not found${NC}"
        WARNINGS=$((WARNINGS + 1))
        return 0
    fi
    
    # Check for validation library in requirements (when integrated)
    if [ -f "$python_dir/requirements.txt" ] && grep -q "rules-validator" "$python_dir/requirements.txt" 2>/dev/null; then
        echo -e "${GREEN}✓ Python: Validation library dependency found${NC}"
    else
        echo -e "${YELLOW}⚠ Python: Validation library not yet integrated (pending Phase 3)${NC}"
        WARNINGS=$((WARNINGS + 1))
    fi
}

# Function to check Rust compiler integration
check_rust_integration() {
    echo ""
    echo "→ Checking Rust compiler integration..."
    
    local rust_dir="$REPO_ROOT/src/rules-compiler-rust"
    
    if [ ! -d "$rust_dir" ]; then
        echo -e "${YELLOW}⚠ Rust compiler not found${NC}"
        WARNINGS=$((WARNINGS + 1))
        return 0
    fi
    
    # Check for validation library dependency
    if grep -q "rules-validator\|rules_validator" "$rust_dir/Cargo.toml" 2>/dev/null; then
        echo -e "${GREEN}✓ Rust: Validation library dependency found${NC}"
    else
        echo -e "${YELLOW}⚠ Rust: Validation library not yet integrated (pending Phase 3)${NC}"
        WARNINGS=$((WARNINGS + 1))
    fi
}

# Function to check if validation library builds
check_validation_library_builds() {
    echo ""
    echo "→ Checking if validation library builds..."
    
    cd "$REPO_ROOT"

    if cargo build --release -p rules-validator-core -p rules-validator-cli >/dev/null 2>&1; then
        echo -e "${GREEN}✓ Validation library builds successfully${NC}"
    else
        echo -e "${RED}✗ Validation library build failed${NC}"
        ERRORS=$((ERRORS + 1))
    fi
}

# Function to check if validation library tests pass
check_validation_library_tests() {
    echo ""
    echo "→ Checking if validation library tests pass..."
    
    cd "$REPO_ROOT"

    if cargo test -p rules-validator-core -p rules-validator-cli >/dev/null 2>&1; then
        echo -e "${GREEN}✓ Validation library tests pass (29 tests)${NC}"
    else
        echo -e "${RED}✗ Validation library tests failed${NC}"
        ERRORS=$((ERRORS + 1))
    fi
}

# Run all checks
check_validation_library
check_validation_library_builds
check_validation_library_tests
check_typescript_integration
check_dotnet_integration
check_python_integration
check_rust_integration

# Summary
echo ""
echo "╔═══════════════════════════════════════════════════════════╗"
echo "║   Compliance Check Summary                                ║"
echo "╚═══════════════════════════════════════════════════════════╝"
echo ""

if [ $ERRORS -eq 0 ] && [ $WARNINGS -eq 0 ]; then
    echo -e "${GREEN}✓ All checks passed!${NC}"
    exit 0
elif [ $ERRORS -eq 0 ]; then
    echo -e "${YELLOW}⚠ Passed with $WARNINGS warning(s)${NC}"
    echo -e "${YELLOW}  Note: Warnings indicate pending integration (migration in progress)${NC}"
    exit 0
else
    echo -e "${RED}✗ Failed with $ERRORS error(s) and $WARNINGS warning(s)${NC}"
    exit 1
fi
