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
    
    if [ ! -d "$REPO_ROOT/src/validation" ]; then
        echo -e "${RED}✗ Validation library not found${NC}"
        ERRORS=$((ERRORS + 1))
        return 1
    fi

    # core/cli are workspace members of the root Cargo.toml, not a
    # standalone crate under src/validation/ itself.
    if [ ! -f "$REPO_ROOT/src/validation/core/Cargo.toml" ] \
        || [ ! -f "$REPO_ROOT/src/validation/cli/Cargo.toml" ]; then
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
    
    local ts_dir="$REPO_ROOT/src/compilers/typescript"
    
    if [ ! -d "$ts_dir" ]; then
        echo -e "${YELLOW}⚠ TypeScript compiler not found${NC}"
        WARNINGS=$((WARNINGS + 1))
        return 0
    fi
    
    # TypeScript shells out to the bloqr-validate CLI (runRulesValidator in
    # orchestration/compiler.ts, #361) rather than binding the native lib.
    if grep -rq "runRulesValidator\|findRulesValidateBinary" "$ts_dir/src" 2>/dev/null; then
        echo -e "${GREEN}✓ TypeScript: Validation library integrated (#361)${NC}"
    else
        echo -e "${YELLOW}⚠ TypeScript: Validation library not yet integrated${NC}"
        WARNINGS=$((WARNINGS + 1))
    fi
}

# Function to check .NET compiler integration
check_dotnet_integration() {
    echo ""
    echo "→ Checking .NET compiler integration..."
    
    local dotnet_dir="$REPO_ROOT/src/compilers/dotnet"
    
    if [ ! -d "$dotnet_dir" ]; then
        echo -e "${YELLOW}⚠ .NET compiler not found${NC}"
        WARNINGS=$((WARNINGS + 1))
        return 0
    fi
    
    # Check for the P/Invoke wrapper (BloqrValidatorService, #264) and its wiring
    # into the compilation pipeline (BloqrCompilerService raising the RV001
    # validation code documented in docs/event-pipeline.md).
    if grep -rq "bloqr_validator_new\|IBloqrValidatorService" "$dotnet_dir/src" 2>/dev/null \
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
    
    local python_dir="$REPO_ROOT/src/compilers/python"
    
    if [ ! -d "$python_dir" ]; then
        echo -e "${YELLOW}⚠ Python compiler not found${NC}"
        WARNINGS=$((WARNINGS + 1))
        return 0
    fi
    
    # Python shells out to the bloqr-validate CLI (_run_rules_validator in
    # bloqr_compiler/compiler.py, #361) rather than depending on a package.
    if grep -rq "_run_rules_validator\|find_rules_validate_binary" "$python_dir/bloqr_compiler" 2>/dev/null; then
        echo -e "${GREEN}✓ Python: Validation library integrated (#361)${NC}"
    else
        echo -e "${YELLOW}⚠ Python: Validation library not yet integrated${NC}"
        WARNINGS=$((WARNINGS + 1))
    fi
}

# Function to check Rust compiler integration
check_rust_integration() {
    echo ""
    echo "→ Checking Rust compiler integration..."
    
    local rust_dir="$REPO_ROOT/src/compilers/rust"
    
    if [ ! -d "$rust_dir" ]; then
        echo -e "${YELLOW}⚠ Rust compiler not found${NC}"
        WARNINGS=$((WARNINGS + 1))
        return 0
    fi
    
    # Rust depends on bloqr-validator-core directly as a Cargo workspace path
    # dependency (#361) - same language, no FFI/shellout needed.
    if grep -q "bloqr-validator\|bloqr_validator" "$rust_dir/Cargo.toml" 2>/dev/null; then
        echo -e "${GREEN}✓ Rust: Validation library integrated (#361)${NC}"
    else
        echo -e "${YELLOW}⚠ Rust: Validation library not yet integrated${NC}"
        WARNINGS=$((WARNINGS + 1))
    fi
}

# Function to check PowerShell module integration
check_powershell_integration() {
    echo ""
    echo "→ Checking PowerShell module integration..."

    local ps_dir="$REPO_ROOT/src/compilers/powershell"

    if [ ! -d "$ps_dir" ]; then
        echo -e "${YELLOW}⚠ PowerShell toolkit not found${NC}"
        WARNINGS=$((WARNINGS + 1))
        return 0
    fi

    # PowerShell shells out to the bloqr-validate CLI (Invoke-RulesValidator /
    # Find-RulesValidateBinary in the BloqrCompiler module, #361).
    if grep -rq "Invoke-RulesValidator\|Find-RulesValidateBinary" "$ps_dir/BloqrCompiler" 2>/dev/null; then
        echo -e "${GREEN}✓ PowerShell: Validation library integrated (#361)${NC}"
    else
        echo -e "${YELLOW}⚠ PowerShell: Validation library not yet integrated${NC}"
        WARNINGS=$((WARNINGS + 1))
    fi
}

# Function to check Shell (bash/zsh) script integration
check_shell_integration() {
    echo ""
    echo "→ Checking Shell (bash/zsh) script integration..."

    local shell_dir="$REPO_ROOT/src/compilers/shell"

    if [ ! -d "$shell_dir" ]; then
        echo -e "${YELLOW}⚠ Shell scripts not found${NC}"
        WARNINGS=$((WARNINGS + 1))
        return 0
    fi

    # Both compile.sh and compile.zsh shell out to the
    # bloqr-validate CLI (run_rules_validator/find_rules_validate_binary, #361).
    if grep -rq "run_rules_validator\|find_rules_validate_binary" "$shell_dir/bash" "$shell_dir/zsh" 2>/dev/null; then
        echo -e "${GREEN}✓ Shell: Validation library integrated (#361)${NC}"
    else
        echo -e "${YELLOW}⚠ Shell: Validation library not yet integrated${NC}"
        WARNINGS=$((WARNINGS + 1))
    fi
}

# Function to check if validation library builds
check_validation_library_builds() {
    echo ""
    echo "→ Checking if validation library builds..."
    
    cd "$REPO_ROOT"

    if cargo build --release -p bloqr-validator-core -p bloqr-validator-core-cli >/dev/null 2>&1; then
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

    if cargo test -p bloqr-validator-core -p bloqr-validator-core-cli >/dev/null 2>&1; then
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
check_powershell_integration
check_shell_integration

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
