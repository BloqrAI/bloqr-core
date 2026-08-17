#!/bin/bash
# Ad-Blocking Repository Launcher
# Feature-rich interactive menu system for all tools and tasks

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
NC='\033[0m' # No Color

# Check for dialog/whiptail
DIALOG_CMD=""
if command -v whiptail &> /dev/null; then
    DIALOG_CMD="whiptail"
elif command -v dialog &> /dev/null; then
    DIALOG_CMD="dialog"
fi

# Function to show banner
show_banner() {
    clear
    echo -e "${CYAN}╔════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${CYAN}║                                                                ║${NC}"
    echo -e "${CYAN}║${NC}           ${MAGENTA}Ad-Blocking Repository Launcher${NC}${CYAN}                  ║${NC}"
    echo -e "${CYAN}║                                                                ║${NC}"
    echo -e "${CYAN}║${NC}     ${GREEN}Multi-Language Toolkit for Ad-Blocking & DNS Management${NC}${CYAN}   ║${NC}"
    echo -e "${CYAN}║                                                                ║${NC}"
    echo -e "${CYAN}╚════════════════════════════════════════════════════════════════╝${NC}"
    echo ""
}

# Function to pause and wait for user
pause() {
    echo ""
    read -p "Press Enter to continue..."
}

# Function to show menu with whiptail/dialog
show_menu_dialog() {
    local title="$1"
    shift
    local menu_items=("$@")
    
    local options=()
    local i=1
    for item in "${menu_items[@]}"; do
        options+=("$i" "$item")
        ((i++))
    done
    
    local choice
    choice=$($DIALOG_CMD --title "$title" --menu "Use arrow keys to navigate, Enter to select:" 20 70 12 "${options[@]}" 3>&1 1>&2 2>&3)
    echo "$choice"
}

# Function to show menu without dialog
show_menu_simple() {
    local title="$1"
    shift
    local menu_items=("$@")
    
    echo -e "${BLUE}═══ $title ═══${NC}"
    echo ""
    local i=1
    for item in "${menu_items[@]}"; do
        echo -e "  ${GREEN}$i.${NC} $item"
        ((i++))
    done
    echo ""
    read -p "Enter your choice [1-$((i-1))]: " choice
    echo "$choice"
}

# Function to show menu (auto-detect best method)
show_menu() {
    if [ -n "$DIALOG_CMD" ]; then
        show_menu_dialog "$@"
    else
        show_menu_simple "$@"
    fi
}

# Function to check tool availability
check_tool() {
    local tool="$1"
    if command -v "$tool" &> /dev/null; then
        echo -e "${GREEN}✓${NC}"
    else
        echo -e "${RED}✗${NC}"
    fi
}

# Dependency preflight: detects a required tool and, if missing, informs the
# user what would be installed and how, then asks before running the official
# installer - never installs silently. Returns 0 if the tool is present (or
# was just installed successfully), 1 if the user aborted or install failed,
# so callers can skip the action that needed it (#277).
#   require_tool <command-name> <human-label> <install-description> <install-command>
require_tool() {
    local tool="$1" label="$2" description="$3" install_cmd="$4"

    if command -v "$tool" &> /dev/null; then
        return 0
    fi

    echo -e "${RED}✗ $label is not installed.${NC}"
    echo -e "${YELLOW}  $description${NC}"
    echo -e "${CYAN}  Install command: ${NC}$install_cmd"
    echo ""
    read -p "Run this install command now? [y/N] " -n 1 -r reply
    echo ""

    if [[ ! "$reply" =~ ^[Yy]$ ]]; then
        echo -e "${YELLOW}Aborted - $label was not installed.${NC}"
        return 1
    fi

    echo -e "${CYAN}Installing $label...${NC}"
    if ! eval "$install_cmd"; then
        echo -e "${RED}Install command failed - aborting.${NC}"
        return 1
    fi

    if command -v "$tool" &> /dev/null; then
        echo -e "${GREEN}✓ $label installed successfully.${NC}"
        return 0
    fi

    echo -e "${RED}$label still not found on PATH after install - you may need to restart your shell. Aborting.${NC}"
    return 1
}

# Main Menu
main_menu() {
    while true; do
        show_banner
        
        local choice
        choice=$(show_menu "Main Menu" \
            "🔨 Build Tools" \
            "⚙️  Compile Filter Rules" \
            "🖥️  Bloqr Dashboard" \
            "🔍 Validation & Testing" \
            "📦 Project Management" \
            "ℹ️  System Information" \
            "🚪 Exit")

        case $choice in
            1) build_menu ;;
            2) rules_menu ;;
            3) run_dashboard ;;
            4) validation_menu ;;
            5) project_menu ;;
            6) system_info ;;
            7|"") exit 0 ;;
            *) echo "Invalid choice" ;;
        esac
    done
}

# Build Tools Menu
build_menu() {
    while true; do
        show_banner
        echo -e "${MAGENTA}Build Tools${NC}"
        echo ""
        
        local choice
        choice=$(show_menu "Build Tools" \
            "Build All Projects (Debug)" \
            "Build All Projects (Release)" \
            "Build Rust Projects" \
            "Build .NET Projects" \
            "Build TypeScript Projects" \
            "Build Python Projects" \
            "Run Build Tests" \
            "← Back to Main Menu")
        
        case $choice in
            1) ./build.sh --all --debug; pause ;;
            2) ./build.sh --all --release; pause ;;
            3) 
                local rust_choice
                rust_choice=$(show_menu "Rust Build Profile" "Debug" "Release" "← Cancel")
                case $rust_choice in
                    1) ./build.sh --rust --debug; pause ;;
                    2) ./build.sh --rust --release; pause ;;
                esac
                ;;
            4)
                local dotnet_choice
                dotnet_choice=$(show_menu ".NET Build Profile" "Debug" "Release" "← Cancel")
                case $dotnet_choice in
                    1) ./build.sh --dotnet --debug; pause ;;
                    2) ./build.sh --dotnet --release; pause ;;
                esac
                ;;
            5) ./build.sh --typescript; pause ;;
            6) ./build.sh --python; pause ;;
            7)
                echo -e "${CYAN}Running build script tests...${NC}"
                ./tools/test-build-scripts.sh
                pause
                ;;
            8|"") return ;;
        esac
    done
}

# Filter Rules Compilation Menu
rules_menu() {
    while true; do
        show_banner
        echo -e "${MAGENTA}Filter Rules Compilation${NC}"
        echo ""
        
        local choice
        choice=$(show_menu "Rules Compiler" \
            "Compile with TypeScript (Deno)" \
            "Compile with .NET" \
            "Compile with Rust" \
            "Compile with Python" \
            "Run Compiler Tests" \
            "← Back to Main Menu")
        
        case $choice in
            1)
                if require_tool deno "Deno" \
                    "Required to run @bloqr/compiler-core, the TypeScript compilation engine." \
                    'curl -fsSL https://deno.land/install.sh | sh && export PATH="$PATH:$HOME/.deno/bin"'; then
                    cd src/compilers/typescript
                    deno task compile
                    cd "$SCRIPT_DIR"
                fi
                pause
                ;;
            2)
                if require_tool dotnet ".NET SDK" \
                    "Required to build/run the .NET rules compiler (installs to ~/.dotnet; add to PATH if it isn't already)." \
                    'curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir "$HOME/.dotnet" && export PATH="$PATH:$HOME/.dotnet"'; then
                    cd src/compilers/dotnet
                    dotnet run --project src/Bloqr.Compiler.Dotnet.Console
                    cd "$SCRIPT_DIR"
                fi
                pause
                ;;
            3)
                if require_tool cargo "Rust toolchain" \
                    "Required to build/run the Rust rules compiler." \
                    'curl --proto "=https" --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y && source "$HOME/.cargo/env"'; then
                    cd src/rules-compiler-rust
                    cargo run --release
                    cd "$SCRIPT_DIR"
                fi
                pause
                ;;
            4)
                if command -v python3 &> /dev/null; then
                    cd src/compilers/python
                    python3 -m bloqr_compiler
                    cd "$SCRIPT_DIR"
                else
                    echo -e "${RED}✗ Python 3 is not installed${NC}"
                fi
                pause
                ;;
            5)
                echo -e "${CYAN}Choose compiler to test:${NC}"
                local test_choice
                test_choice=$(show_menu "Test Which Compiler?" "TypeScript" "Rust" ".NET" "Python" "← Cancel")
                case $test_choice in
                    1) cd src/compilers/typescript && deno task test && cd "$SCRIPT_DIR" ;;
                    2) cargo test -p rules-compiler ;;
                    3) cd src/compilers/dotnet && dotnet test CompilerDotnet.slnx && cd "$SCRIPT_DIR" ;;
                    4) cd src/compilers/python && python3 -m pytest && cd "$SCRIPT_DIR" ;;
                esac
                pause
                ;;
            6|"") return ;;
        esac
    done
}

# Run the Bloqr Dashboard
run_dashboard() {
    show_banner
    echo -e "${MAGENTA}Bloqr Dashboard${NC}"
    echo ""
    if require_tool dotnet ".NET SDK" \
        "Required to run the Dashboard (installs to ~/.dotnet; add to PATH if it isn't already)." \
        'curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir "$HOME/.dotnet" && export PATH="$PATH:$HOME/.dotnet"'; then
        cd src/bloqr-dashboard
        dotnet run --project src/Bloqr.Dashboard.Console
        cd "$SCRIPT_DIR"
    fi
    pause
}

# AdGuard API Clients have moved to BloqrAI/bloqr-apiclients; this launcher
# no longer manages them.

# Validation & Testing Menu
validation_menu() {
    while true; do
        show_banner
        echo -e "${MAGENTA}Validation & Testing${NC}"
        echo ""
        
        local choice
        choice=$(show_menu "Validation & Testing" \
            "Run Validation Library Tests" \
            "Run All Rust Tests" \
            "Run All .NET Tests" \
            "Run Build Script Tests" \
            "Check Validation Compliance" \
            "Run Clippy (Rust Linter)" \
            "← Back to Main Menu")
        
        case $choice in
            1)
                cargo test -p bloqr-validator-core -p bloqr-validator-core-cli
                pause
                ;;
            2)
                cargo test --workspace
                pause
                ;;
            3)
                echo "Testing .NET Compiler..."
                cd src/compilers/dotnet && dotnet test CompilerDotnet.slnx && cd "$SCRIPT_DIR"
                pause
                ;;
            4)
                ./tools/test-build-scripts.sh
                pause
                ;;
            5)
                chmod +x tools/check-validation-compliance.sh
                ./tools/check-validation-compliance.sh
                pause
                ;;
            6)
                cargo clippy --workspace --all-features -- -W clippy::all
                pause
                ;;
            7|"") return ;;
        esac
    done
}

# Project Management Menu
project_menu() {
    while true; do
        show_banner
        echo -e "${MAGENTA}Project Management${NC}"
        echo ""
        
        local choice
        choice=$(show_menu "Project Management" \
            "View Project Structure" \
            "Clean Build Artifacts" \
            "Update Dependencies (Rust)" \
            "Update Dependencies (.NET)" \
            "Run PowerShell Module Tests" \
            "View Git Status" \
            "← Back to Main Menu")
        
        case $choice in
            1)
                echo -e "${CYAN}Project Structure:${NC}"
                echo ""
                tree -L 2 src/ 2>/dev/null || find src/ -maxdepth 2 -type d
                pause
                ;;
            2)
                echo -e "${YELLOW}Cleaning build artifacts...${NC}"
                cargo clean
                find . -type d -name "bin" -o -name "obj" -o -name "target" | while read dir; do
                    echo "Removing $dir"
                done
                echo -e "${GREEN}✓ Clean complete${NC}"
                pause
                ;;
            3)
                cargo update
                pause
                ;;
            4)
                echo "Updating .NET tools..."
                dotnet tool update --global dotnet-format || true
                pause
                ;;
            5)
                pwsh -File tools/test-modules.ps1
                pause
                ;;
            6)
                git status
                echo ""
                git log --oneline -10
                pause
                ;;
            7|"") return ;;
        esac
    done
}

# System Information
system_info() {
    show_banner
    echo -e "${MAGENTA}System Information${NC}"
    echo ""
    
    echo -e "${CYAN}Available Tools:${NC}"
    echo -e "  Rust (cargo):      $(check_tool cargo)  $(cargo --version 2>/dev/null || echo 'Not installed')"
    echo -e "  .NET:              $(check_tool dotnet)  $(dotnet --version 2>/dev/null || echo 'Not installed')"
    echo -e "  Deno:              $(check_tool deno)  $(deno --version 2>/dev/null | head -1 || echo 'Not installed')"
    echo -e "  Python:            $(check_tool python3)  $(python3 --version 2>/dev/null || echo 'Not installed')"
    echo -e "  PowerShell:        $(check_tool pwsh)  $(pwsh --version 2>/dev/null || echo 'Not installed')"
    echo -e "  Git:               $(check_tool git)  $(git --version 2>/dev/null || echo 'Not installed')"
    echo ""
    
    echo -e "${CYAN}Repository Information:${NC}"
    echo -e "  Branch:            $(git branch --show-current 2>/dev/null || echo 'Unknown')"
    echo -e "  Last Commit:       $(git log -1 --pretty=format:'%h - %s' 2>/dev/null || echo 'Unknown')"
    echo -e "  Working Directory: $SCRIPT_DIR"
    echo ""
    
    echo -e "${CYAN}Projects Available:${NC}"
    echo -e "  Rust Projects:     $(find src -name "Cargo.toml" | wc -l) packages"
    echo -e "  .NET Projects:     $(find src -name "*.csproj" | wc -l) projects"
    echo -e "  TypeScript:        $(find src -name "deno.json" | wc -l) projects"
    echo -e "  Python:            $(find src -name "pyproject.toml" | wc -l) projects"
    echo ""
    
    pause
}

# Start the launcher
if [ -t 0 ]; then
    # Interactive mode
    main_menu
else
    # Non-interactive mode - show help
    show_banner
    echo "Usage: $0 [interactive]"
    echo ""
    echo "This is an interactive launcher. Run it in a terminal to access the menu system."
    echo ""
    echo "Available build commands:"
    echo "  ./build.sh --all          Build all projects"
    echo "  ./build.sh --rust         Build Rust projects"
    echo "  ./build.sh --dotnet       Build .NET projects"
    echo ""
    echo "Run this script in a terminal for the interactive menu."
fi
