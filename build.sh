#!/bin/bash
# Root-level build script for ad-blocking repository
# Builds all projects or specific ones with debug/release profiles

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default values
BUILD_PROFILE="debug"
BUILD_ALL=false
BUILD_RUST=false
BUILD_DOTNET=false
BUILD_TYPESCRIPT=false
BUILD_PYTHON=false

# Function to print usage
usage() {
    cat << EOF
Usage: $0 [OPTIONS]

Build all projects or specific ones with debug/release profiles.

OPTIONS:
    --all               Build all projects (default if no specific project selected)
    --rust              Build Rust projects
    --dotnet            Build .NET projects
    --typescript        Build TypeScript/Deno projects
    --python            Build Python projects
    --debug             Use debug profile (default)
    --release           Use release profile
    -h, --help          Show this help message

EXAMPLES:
    $0                  # Build all projects in debug mode
    $0 --rust           # Build only Rust projects in debug mode
    $0 --dotnet --release   # Build only .NET projects in release mode
    $0 --all --release  # Build all projects in release mode

EOF
    exit "${1:-0}"
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --all)
            BUILD_ALL=true
            shift
            ;;
        --rust)
            BUILD_RUST=true
            shift
            ;;
        --dotnet)
            BUILD_DOTNET=true
            shift
            ;;
        --typescript)
            BUILD_TYPESCRIPT=true
            shift
            ;;
        --python)
            BUILD_PYTHON=true
            shift
            ;;
        --debug)
            BUILD_PROFILE="debug"
            shift
            ;;
        --release)
            BUILD_PROFILE="release"
            shift
            ;;
        -h|--help)
            usage 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            usage 1
            ;;
    esac
done

# If no specific project selected, build all
if [[ "$BUILD_ALL" == false ]] && [[ "$BUILD_RUST" == false ]] && [[ "$BUILD_DOTNET" == false ]] && [[ "$BUILD_TYPESCRIPT" == false ]] && [[ "$BUILD_PYTHON" == false ]]; then
    BUILD_ALL=true
fi

# If --all is specified, enable all projects
if [[ "$BUILD_ALL" == true ]]; then
    BUILD_RUST=true
    BUILD_DOTNET=true
    BUILD_TYPESCRIPT=true
    BUILD_PYTHON=true
fi

echo "╔═══════════════════════════════════════════════════════════╗"
echo "║   Ad-Blocking Repository Build Script                    ║"
echo "╚═══════════════════════════════════════════════════════════╝"
echo ""
echo -e "${BLUE}Build Profile: ${BUILD_PROFILE}${NC}"
echo ""

BUILD_FAILED=false

# Function to build Rust projects
build_rust() {
    echo -e "${BLUE}Building Rust projects...${NC}"
    
    local cargo_flags=""
    if [[ "$BUILD_PROFILE" == "release" ]]; then
        cargo_flags="--release"
    fi
    
    # Build the entire workspace
    echo "→ Building Rust workspace..."
    if [[ "$BUILD_PROFILE" == "release" ]]; then
        cargo build --release --workspace
    else
        cargo build --workspace
    fi
    
    if [[ $? -eq 0 ]]; then
        echo -e "${GREEN}✓ Rust workspace built successfully${NC}"
    else
        echo -e "${RED}✗ Rust workspace build failed${NC}"
        BUILD_FAILED=true
        return 1
    fi
    
    echo ""
}

# Function to build .NET projects
build_dotnet() {
    echo -e "${BLUE}Building .NET projects...${NC}"
    
    local configuration="Debug"
    if [[ "$BUILD_PROFILE" == "release" ]]; then
        configuration="Release"
    fi

    # Build Compiler Common (.NET) - shared library, standalone solution
    echo "→ Building Compiler Common (.NET)..."
    if (cd src/common/dotnet && dotnet restore CompilerCommon.slnx && dotnet build CompilerCommon.slnx --no-restore --configuration $configuration); then
        echo -e "${GREEN}✓ Compiler Common (.NET) built successfully${NC}"
    else
        echo -e "${RED}✗ Compiler Common (.NET) build failed${NC}"
        BUILD_FAILED=true
    fi

    # Build Compiler Dotnet
    echo "→ Building Compiler Dotnet..."
    if (cd src/compilers/dotnet && dotnet restore CompilerDotnet.slnx && dotnet build CompilerDotnet.slnx --no-restore --configuration $configuration); then
        echo -e "${GREEN}✓ Compiler Dotnet built successfully${NC}"
    else
        echo -e "${RED}✗ Compiler Dotnet build failed${NC}"
        BUILD_FAILED=true
    fi

    # Build Dashboard (.NET)
    echo "→ Building Dashboard (.NET)..."
    if (cd src/apps/dashboard && dotnet restore BloqrDashboard.slnx && dotnet build BloqrDashboard.slnx --no-restore --configuration $configuration); then
        echo -e "${GREEN}✓ Dashboard (.NET) built successfully${NC}"
    else
        echo -e "${RED}✗ Dashboard (.NET) build failed${NC}"
        BUILD_FAILED=true
    fi

    echo ""
}

# Function to build TypeScript/Deno projects
build_typescript() {
    echo -e "${BLUE}Building TypeScript/Deno projects...${NC}"
    
    # Check if Deno is installed
    if ! command -v deno &> /dev/null; then
        echo -e "${RED}✗ Deno is not installed. Please install Deno to build TypeScript projects.${NC}"
        BUILD_FAILED=true
        return 1
    fi
    
    # Build Rules Compiler TypeScript
    echo "→ Building Rules Compiler (TypeScript)..."
    if (cd src/compilers/typescript && deno task generate:types && deno task check); then
        echo -e "${GREEN}✓ Rules Compiler (TypeScript) built successfully${NC}"
    else
        echo -e "${RED}✗ Rules Compiler (TypeScript) build failed${NC}"
        BUILD_FAILED=true
    fi
    
    echo ""
}

# Function to build Python projects
build_python() {
    echo -e "${BLUE}Building Python projects...${NC}"
    
    # Check if Python is installed
    if ! command -v python3 &> /dev/null; then
        echo -e "${RED}✗ Python 3 is not installed. Please install Python 3 to build Python projects.${NC}"
        BUILD_FAILED=true
        return 1
    fi
    
    # Build Bloqr Compiler Python
    echo "→ Building Compiler (Python)..."
    if (cd src/compilers/python && python3 -m pip install --quiet -e ".[dev]" && python3 -m mypy bloqr_compiler/); then
        echo -e "${GREEN}✓ Compiler (Python) built successfully${NC}"
    else
        echo -e "${RED}✗ Compiler (Python) build failed${NC}"
        BUILD_FAILED=true
    fi
    
    echo ""
}

# Build projects based on flags
if [[ "$BUILD_RUST" == true ]]; then
    build_rust
fi

if [[ "$BUILD_DOTNET" == true ]]; then
    build_dotnet
fi

if [[ "$BUILD_TYPESCRIPT" == true ]]; then
    build_typescript
fi

if [[ "$BUILD_PYTHON" == true ]]; then
    build_python
fi

# Summary
echo "╔═══════════════════════════════════════════════════════════╗"
echo "║   Build Summary                                           ║"
echo "╚═══════════════════════════════════════════════════════════╝"
echo ""

if [[ "$BUILD_FAILED" == true ]]; then
    echo -e "${RED}✗ Some builds failed. Please check the output above.${NC}"
    exit 1
else
    echo -e "${GREEN}✓ All builds completed successfully!${NC}"
    exit 0
fi
