"""
Interactive menu mode for the AdGuard Filter Rules Compiler.
"""

from __future__ import annotations

import sys
from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from bloqr_compiler.compiler import VersionInfo

try:
    import questionary
    from questionary import Choice
    HAS_QUESTIONARY = True
except ImportError:
    HAS_QUESTIONARY = False


def check_interactive_available() -> bool:
    """Check if interactive mode dependencies are available."""
    return HAS_QUESTIONARY


def require_interactive() -> None:
    """Raise an error if interactive mode is not available."""
    if not HAS_QUESTIONARY:
        print("[ERROR] Interactive mode requires the 'questionary' package.", file=sys.stderr)
        print("", file=sys.stderr)
        print("Install with:", file=sys.stderr)
        print("  pip install rules-compiler[interactive]", file=sys.stderr)
        print("", file=sys.stderr)
        print("Or install questionary directly:", file=sys.stderr)
        print("  pip install questionary", file=sys.stderr)
        sys.exit(1)


def run_interactive_menu(initial_config: Path | None = None) -> int:
    """
    Run the interactive menu mode.
    
    Args:
        initial_config: Optional initial configuration file path.
        
    Returns:
        Exit code (0 for success, 1 for failure).
    """
    require_interactive()
    
    from bloqr_compiler.compiler import (
        BloqrCompiler,
        get_version_info,
        validate_configuration,
    )
    from bloqr_compiler.config import ConfigurationFormat, read_configuration, to_json
    
    # Try to find a default config if none provided
    if initial_config is None:
        search_paths = [
            Path.cwd() / "compiler-config.json",
            Path.cwd() / "compiler-config.yaml",
            Path.cwd() / "compiler-config.yml",
            Path.cwd() / "compiler-config.toml",
            Path.cwd() / "src" / "rules-compiler-typescript" / "compiler-config.json",
        ]
        
        for path in search_paths:
            if path.exists():
                initial_config = path
                break
    
    config_path: Path | None = initial_config
    
    print()
    print("╔════════════════════════════════════════════════════════════╗")
    print("║     AdGuard Filter Rules Compiler - Interactive Mode       ║")
    print("╚════════════════════════════════════════════════════════════╝")
    print()
    
    while True:
        current_config = str(config_path) if config_path else "Not set"
        print(f"  Current config: {current_config}")
        print()
        
        choices = [
            Choice("Compile Rules", value="compile"),
            Choice("View Configuration", value="view"),
            Choice("Validate Configuration", value="validate"),
            Choice("Change Configuration File", value="change"),
            Choice("Version Information", value="version"),
            Choice("Exit", value="exit"),
        ]
        
        try:
            action = questionary.select(
                "Select an action:",
                choices=choices,
                qmark="",
            ).ask()
        except (KeyboardInterrupt, EOFError):
            print()
            print("  Exiting...")
            return 0
        
        if action is None or action == "exit":
            print()
            print("  Exiting...")
            return 0
        
        print()
        
        if action == "compile":
            if config_path is None:
                print("  No configuration file selected.")
                print("  Use 'Change Configuration File' to select one.")
                print()
                continue
            
            # Ask for compilation options
            copy_to_rules = questionary.confirm(
                "Copy output to rules directory?",
                default=False,
            ).ask()
            
            validate = questionary.confirm(
                "Validate configuration before compiling?",
                default=True,
            ).ask()
            
            fail_on_warnings = False
            if validate:
                fail_on_warnings = questionary.confirm(
                    "Fail compilation on validation warnings?",
                    default=False,
                ).ask()
            
            # Compile
            print()
            print("╔════════════════════════════════════════════════════════════╗")
            print("║                  Compiling Filter Rules                    ║")
            print("╚════════════════════════════════════════════════════════════╝")
            print()
            print(f"  Config: {config_path}")
            print()
            
            compiler = BloqrCompiler(debug=False)
            result = compiler.compile(
                config_path=config_path,
                copy_to_rules=copy_to_rules,
                validate=validate,
                fail_on_warnings=fail_on_warnings,
            )
            
            if result.success:
                print("  ✓ Compilation successful!")
                print()
                print("  Results:")
                print(f"    Filter:     {result.config_name} v{result.config_version}")
                print(f"    Rules:      {result.rule_count:,}")
                print(f"    Output:     {result.output_path}")
                print(f"    Hash:       {result.hash_short()}...")
                print(f"    Elapsed:    {result.elapsed_formatted()}")
                
                if result.copied_to_rules:
                    print()
                    print(f"  ✓ Copied to:  {result.rules_destination}")
                
                print()
            else:
                print(f"  ✗ Compilation failed: {result.error_message}")
                if result.stderr:
                    print()
                    print("  Stderr:")
                    for line in result.stderr.splitlines():
                        print(f"    {line}")
                print()
        
        elif action == "view":
            if config_path is None:
                print("  No configuration file selected.")
                print()
                continue
            
            try:
                config = read_configuration(config_path)
                
                print("╔════════════════════════════════════════════════════════════╗")
                print("║                    Configuration Details                   ║")
                print("╚════════════════════════════════════════════════════════════╝")
                print()
                print(f"  File:         {config_path}")
                format_name = config._source_format.value if config._source_format else "unknown"
                print(f"  Format:       {format_name}")
                print()
                print(f"  Name:         {config.name}")
                if config.version:
                    print(f"  Version:      {config.version}")
                if config.license:
                    print(f"  License:      {config.license}")
                if config.description:
                    print(f"  Description:  {config.description}")
                print()
                print(f"  Sources:      {len(config.sources)} total")
                print(f"    Local:      {config.local_sources_count()}")
                print(f"    Remote:     {config.remote_sources_count()}")
                print()
                
                if config.transformations:
                    print("  Transformations:")
                    for t in config.transformations:
                        print(f"    - {t}")
                    print()
                
                print("  Source Details:")
                for i, source in enumerate(config.sources):
                    name = source.name or f"[{i}]"
                    print(f"    {name}:")
                    print(f"      Source: {source.source}")
                    print(f"      Type:   {source.type}")
                print()
                
            except Exception as e:
                print(f"  ✗ Failed to read configuration: {e}")
                print()
        
        elif action == "validate":
            if config_path is None:
                print("  No configuration file selected.")
                print()
                continue
            
            check_files = questionary.confirm(
                "Check if local source files exist?",
                default=False,
            ).ask()
            
            print()
            print(f"  Validating configuration: {config_path}")
            print()
            
            try:
                is_valid, errors, warnings = validate_configuration(
                    config_path,
                    check_files=check_files,
                )
                
                if errors:
                    print("  Errors:")
                    for error in errors:
                        print(f"    [ERROR] {error}")
                    print()
                
                if warnings:
                    print("  Warnings:")
                    for warning in warnings:
                        print(f"    [WARN] {warning}")
                    print()
                
                if is_valid:
                    print("  ✓ Configuration is valid")
                    if warnings:
                        print(f"     ({len(warnings)} warning(s))")
                else:
                    print(f"  ✗ Configuration has {len(errors)} error(s)")
                print()
                
            except Exception as e:
                print(f"  ✗ Validation failed: {e}")
                print()
        
        elif action == "change":
            initial_text = str(config_path) if config_path else ""
            
            try:
                new_path = questionary.path(
                    "Enter configuration file path:",
                    default=initial_text,
                    only_files=True,
                ).ask()
            except (KeyboardInterrupt, EOFError):
                print()
                continue
            
            if new_path:
                new_path_obj = Path(new_path.strip())
                if new_path_obj.exists():
                    config_path = new_path_obj
                    print(f"  ✓ Configuration file updated to: {config_path}")
                else:
                    print(f"  ✗ File not found: {new_path_obj}")
            print()
        
        elif action == "version":
            info = get_version_info()
            
            print("╔════════════════════════════════════════════════════════════╗")
            print("║     AdGuard Filter Rules Compiler (Python API)             ║")
            print("╚════════════════════════════════════════════════════════════╝")
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
