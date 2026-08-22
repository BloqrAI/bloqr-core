"""
Core compiler functionality for AdGuard filter rules.

This module provides both synchronous and asynchronous APIs for compiling
AdGuard filter rules. The async APIs offer better performance for I/O-bound
operations and can be used in async applications.
"""

from __future__ import annotations

import asyncio
import hashlib
import json
import logging
import os
import platform
import shutil
import subprocess
import tempfile
from dataclasses import dataclass, field, replace
from datetime import datetime
from pathlib import Path
from typing import Any

try:
    import aiofiles
    AIOFILES_AVAILABLE = True
except ImportError:
    AIOFILES_AVAILABLE = False

from bloqr_compiler.config import (
    CompilerConfiguration,
    ConfigurationFormat,
    HashVerificationSettings,
    read_configuration,
    to_json,
)
from bloqr_compiler.errors import (
    CompilationError,
    CompilerNotFoundError,
    CopyError,
    OutputNotCreatedError,
    TimeoutError as CompilerTimeoutError,
    ValidationError,
)
from bloqr_compiler.events import (
    EventDispatcher,
    HashComputedEventArgs,
    HashMismatchEventArgs,
    HashVerifiedEventArgs,
    ValidationEventArgs,
    ValidationSeverity,
)
from bloqr_compiler.hash_database import HashDatabaseEntry, load_hash_database, record_hash
from bloqr_compiler.output_publisher import publish_output

logger = logging.getLogger(__name__)


@dataclass
class PlatformInfo:
    """Platform-specific information."""
    os_name: str = ""
    os_version: str = ""
    architecture: str = ""
    is_windows: bool = False
    is_linux: bool = False
    is_macos: bool = False


@dataclass
class VersionInfo:
    """Version information for all components."""
    module_version: str = ""
    python_version: str = ""
    node_version: str | None = None
    hostlist_compiler_version: str | None = None
    hostlist_compiler_path: str | None = None
    platform: PlatformInfo = field(default_factory=PlatformInfo)

    def has_node(self) -> bool:
        """Check if Node.js is available."""
        return self.node_version is not None

    def has_compiler(self) -> bool:
        """Check if the compiler (adblock-compiler-core, via Deno) is available."""
        return self.hostlist_compiler_version is not None

    @classmethod
    def collect(cls) -> VersionInfo:
        """Collect version information from the system."""
        return get_version_info()


@dataclass
class CompilerResult:
    """Result of a compilation operation."""
    success: bool = False
    config_name: str = ""
    config_version: str = ""
    rule_count: int = 0
    output_path: str = ""
    output_hash: str = ""
    copied_to_rules: bool = False
    rules_destination: str | None = None
    elapsed_ms: int = 0
    start_time: datetime = field(default_factory=datetime.utcnow)
    end_time: datetime = field(default_factory=datetime.utcnow)
    error_message: str | None = None
    stdout: str = ""
    stderr: str = ""

    def elapsed_formatted(self) -> str:
        """Get elapsed time in human-readable format."""
        if self.elapsed_ms >= 1000:
            return f"{self.elapsed_ms / 1000:.2f}s"
        return f"{self.elapsed_ms}ms"

    def hash_short(self, length: int = 32) -> str:
        """Get shortened hash for display."""
        if len(self.output_hash) > length:
            return self.output_hash[:length]
        return self.output_hash

    def output_path_str(self) -> str:
        """Get output path as string."""
        return str(self.output_path) if self.output_path else ""

    def rules_destination_str(self) -> str | None:
        """Get rules destination as string or None."""
        return str(self.rules_destination) if self.rules_destination else None


def get_platform_info() -> PlatformInfo:
    """Get information about the current platform."""
    system = platform.system().lower()
    return PlatformInfo(
        os_name=platform.system(),
        os_version=platform.version(),
        architecture=platform.machine(),
        is_windows=system == "windows",
        is_linux=system == "linux",
        is_macos=system == "darwin",
    )


def find_command(command: str) -> str | None:
    """Find a command in PATH."""
    return shutil.which(command)


def find_rules_validate_binary() -> str | None:
    """
    Locate the `bloqr-validate` CLI (from `src/validation/cli`).

    Checks, in order: the `RULES_VALIDATE_PATH` environment variable, PATH, then a
    couple of dev-convenience fallback locations relative to this repo's Cargo
    workspace `target/` directory (debug/release). Returns `None` if not found -
    callers should skip validation silently in that case, matching the .NET
    integration's `IsAvailable` behavior.
    """
    env_override = os.environ.get("RULES_VALIDATE_PATH")
    if env_override and Path(env_override).is_file():
        return env_override

    on_path = shutil.which("bloqr-validate")
    if on_path:
        return on_path

    binary_name = "bloqr-validate.exe" if platform.system() == "Windows" else "bloqr-validate"
    # compiler.py -> bloqr_compiler/ -> python/ -> compilers/ -> src/ -> repo root
    repo_root = Path(__file__).resolve().parents[4]
    for profile in ("release", "debug"):
        candidate = repo_root / "target" / profile / binary_name
        if candidate.is_file():
            return str(candidate)

    return None


def get_version_info() -> VersionInfo:
    """Get version information for all components."""
    from bloqr_compiler import __version__

    info = VersionInfo(
        module_version=__version__,
        python_version=platform.python_version(),
        platform=get_platform_info(),
    )

    # Check Deno (reported via node_version for backward compatibility with
    # existing consumers of VersionInfo)
    deno_path = find_command("deno")
    if deno_path:
        try:
            result = subprocess.run(
                [deno_path, "--version"],
                capture_output=True,
                text=True,
                timeout=10,
            )
            if result.returncode == 0:
                info.node_version = result.stdout.strip()
        except Exception:
            pass

        info.hostlist_compiler_path = f"{deno_path} run {_JSR_PACKAGE_SPECIFIER}"
        try:
            result = subprocess.run(
                [deno_path, "run", *_DENO_PERMISSIONS, _JSR_PACKAGE_SPECIFIER, "--version"],
                capture_output=True,
                text=True,
                timeout=10,
            )
            if result.returncode == 0:
                info.hostlist_compiler_version = result.stdout.strip().split("\n")[0]
        except Exception:
            pass

    return info


def count_rules(file_path: str | Path) -> int:
    """
    Count non-empty, non-comment lines in a file.

    Args:
        file_path: Path to the file.

    Returns:
        Number of rules.
    """
    path = Path(file_path)
    if not path.exists():
        return 0

    count = 0
    with open(path, "r", encoding="utf-8") as f:
        for line in f:
            stripped = line.strip()
            if stripped and not stripped.startswith(("!", "#")):
                count += 1

    return count


async def count_rules_async(file_path: str | Path) -> int:
    """
    Asynchronously count non-empty, non-comment lines in a file.

    Args:
        file_path: Path to the file.

    Returns:
        Number of rules.
    """
    if not AIOFILES_AVAILABLE:
        # Fall back to sync version in thread pool
        loop = asyncio.get_event_loop()
        return await loop.run_in_executor(None, count_rules, file_path)

    path = Path(file_path)
    if not path.exists():
        return 0

    count = 0
    async with aiofiles.open(path, "r", encoding="utf-8") as f:
        async for line in f:
            stripped = line.strip()
            if stripped and not stripped.startswith(("!", "#")):
                count += 1

    return count


def compute_hash(file_path: str | Path) -> str:
    """
    Compute SHA-384 hash of a file.

    Args:
        file_path: Path to the file.

    Returns:
        Hex-encoded hash string.
    """
    sha384 = hashlib.sha384()
    with open(file_path, "rb") as f:
        for chunk in iter(lambda: f.read(8192), b""):
            sha384.update(chunk)
    return sha384.hexdigest()


async def compute_hash_async(file_path: str | Path) -> str:
    """
    Asynchronously compute SHA-384 hash of a file.

    Args:
        file_path: Path to the file.

    Returns:
        Hex-encoded hash string.
    """
    if not AIOFILES_AVAILABLE:
        # Fall back to sync version in thread pool
        loop = asyncio.get_event_loop()
        return await loop.run_in_executor(None, compute_hash, file_path)

    sha384 = hashlib.sha384()
    async with aiofiles.open(file_path, "rb") as f:
        while True:
            chunk = await f.read(8192)
            if not chunk:
                break
            sha384.update(chunk)
    return sha384.hexdigest()


def hash_short(hash_value: str, length: int = 32) -> str:
    """
    Get shortened hash for display.

    Args:
        hash_value: Full hash string.
        length: Maximum length to return.

    Returns:
        Shortened hash string.
    """
    if len(hash_value) > length:
        return hash_value[:length]
    return hash_value


def format_elapsed(elapsed_ms: int) -> str:
    """
    Format elapsed time in human-readable format.

    Args:
        elapsed_ms: Elapsed time in milliseconds.

    Returns:
        Formatted string (e.g., "1.50s" or "500ms").
    """
    if elapsed_ms >= 1000:
        return f"{elapsed_ms / 1000:.2f}s"
    return f"{elapsed_ms}ms"


async def _raise_hash_computed(
    event_dispatcher: EventDispatcher | None,
    item_identifier: str,
    item_type: str,
    hash_value: str,
    size_bytes: int,
) -> None:
    """Raise a HashComputed event if a dispatcher was supplied."""
    if event_dispatcher is None:
        return
    await event_dispatcher.raise_hash_computed(
        HashComputedEventArgs(
            item_identifier=item_identifier,
            item_type=item_type,
            hash=hash_value,
            size_bytes=size_bytes,
        )
    )


async def _verify_and_record_hash(
    hash_database_path: str | Path,
    item_identifier: str,
    item_type: str,
    computed_hash: str,
    settings: HashVerificationSettings,
    event_dispatcher: EventDispatcher | None,
) -> tuple[bool, str | None]:
    """
    Compare a freshly computed hash against the `.hashes.json` sidecar, raising
    HashVerified/HashMismatch events (if a dispatcher was supplied) and recording the hash
    when it is new or a mismatch was allowed to continue.

    Returns:
        Tuple of (can_continue, error_message).
    """
    size_bytes = Path(item_identifier).stat().st_size
    entries = load_hash_database(hash_database_path)
    existing = entries.get(item_identifier)

    if existing is None:
        # First time this item has been seen: bootstrap trust for future runs.
        record_hash(
            hash_database_path,
            item_identifier,
            HashDatabaseEntry(hash=computed_hash, size_bytes=size_bytes, item_type=item_type),
        )
        return True, None

    if existing.hash.lower() == computed_hash.lower():
        if event_dispatcher is not None:
            await event_dispatcher.raise_hash_verified(
                HashVerifiedEventArgs(
                    item_identifier=item_identifier,
                    item_type=item_type,
                    expected_hash=existing.hash,
                    actual_hash=computed_hash,
                    size_bytes=size_bytes,
                )
            )
        return True, None

    mismatch_args = HashMismatchEventArgs(
        item_identifier=item_identifier,
        item_type=item_type,
        expected_hash=existing.hash,
        actual_hash=computed_hash,
        size_bytes=size_bytes,
    )
    strict = settings.fail_on_mismatch or settings.mode.lower() == "strict"
    if not strict:
        mismatch_args.abort = False
        mismatch_args.allow_continuation = True

    if event_dispatcher is not None:
        await event_dispatcher.raise_hash_mismatch(mismatch_args)

    should_abort = mismatch_args.abort and not mismatch_args.allow_continuation
    if should_abort:
        return False, mismatch_args.abort_reason or f"Hash mismatch for {item_identifier}"

    # Continuation was allowed - accept the new hash as the trusted baseline going forward.
    record_hash(
        hash_database_path,
        item_identifier,
        HashDatabaseEntry(hash=computed_hash, size_bytes=size_bytes, item_type=item_type),
    )
    return True, None


async def _run_rules_validator(
    output_path: Path,
    event_dispatcher: EventDispatcher | None,
    allow_unvalidated: bool = False,
    fail_on_warnings: bool = False,
) -> tuple[bool, str | None]:
    """
    Shell out to the native `bloqr-validate` CLI to run its syntax check against the
    compiled output, and raise a `Validation` event with its findings.

    Fail-closed by default: any Error/Critical finding (`not validation_args.passed`)
    aborts compilation, and so does a missing/failed/unparseable validator run - a
    validator we couldn't run tells us nothing about the output's safety, so it can't
    be treated as "no findings". `fail_on_warnings` additionally escalates Warning
    findings to abort. A registered handler may still explicitly set
    `validation_args.abort` for custom logic, but no handler (or event_dispatcher) is
    required for the default checks to hold - this closes the "no handler/dispatcher
    was wired up, so nothing ever aborted" gap.

    Pass `allow_unvalidated=True` to revert to the legacy, opt-in-only behavior
    (silently continue on a run failure; only an explicit handler-set `abort` counts) -
    use only for deliberate debugging of unvalidated output.

    Returns:
        Tuple of (can_continue, error_message).
    """
    binary = find_rules_validate_binary()
    if binary is None:
        if allow_unvalidated:
            return True, None
        return False, (
            "bloqr-validate binary not found; cannot validate compiled output. "
            "Pass allow_unvalidated_output to bypass this check (not recommended)."
        )

    hash_db_path = output_path.parent / ".hashes.json"
    try:
        proc = subprocess.run(
            [binary, "--json", "file", str(output_path), "--hash-db", str(hash_db_path)],
            capture_output=True,
            text=True,
            timeout=60,
        )
    except (OSError, subprocess.TimeoutExpired) as e:
        logger.warning(f"bloqr-validate failed to run: {e}")
        if allow_unvalidated:
            return True, None
        return False, (
            f"bloqr-validate failed to run: {e}. "
            "Pass allow_unvalidated_output to bypass this check (not recommended)."
        )

    try:
        payload = json.loads(proc.stdout)
    except (json.JSONDecodeError, ValueError):
        logger.warning(f"bloqr-validate produced unparseable output: {proc.stdout!r}")
        if allow_unvalidated:
            return True, None
        return False, (
            "bloqr-validate produced unparseable output; cannot validate compiled "
            "output. Pass allow_unvalidated_output to bypass this check (not recommended)."
        )

    validation_args = ValidationEventArgs(stage_name="bloqr-validator")

    if "error" in payload:
        # bloqr-validate itself failed (e.g. unreadable file) - this is a validation
        # failure, not a mere finding, so it's added as an error (fail-closed default).
        validation_args.add_error("RV001", payload["error"])
    else:
        is_valid = payload.get("is_valid", False)
        messages = payload.get("messages", [])
        validation_args.items_validated = payload.get("valid_rules", 0) + payload.get(
            "invalid_rules", 0
        )

        if not messages:
            if not is_valid:
                validation_args.add_error(
                    "RV001",
                    "Output file failed bloqr-validator syntax validation "
                    f"({payload.get('invalid_rules', 0)} invalid rule(s) of "
                    f"{validation_args.items_validated}).",
                )
        else:
            for message in messages:
                if is_valid:
                    validation_args.add_warning("RV001", message)
                else:
                    validation_args.add_error("RV001", message)

    if event_dispatcher is not None:
        await event_dispatcher.raise_validation(validation_args)

    has_warnings = any(
        f.severity == ValidationSeverity.WARNING for f in validation_args.findings
    )
    should_abort = validation_args.abort or (
        not allow_unvalidated
        and (not validation_args.passed or (fail_on_warnings and has_warnings))
    )

    if should_abort:
        return False, validation_args.abort_reason or f"bloqr-validator validation failed for {output_path}"

    return True, None


def _resolve_relative_to_config(path: str, config_path: Path) -> Path:
    """Resolve a config-relative path (e.g. hashDatabasePath, output.path) against the
    config file's own directory, matching the .NET/Rust/TypeScript compilers' behavior."""
    candidate = Path(path)
    return candidate if candidate.is_absolute() else (config_path.parent / candidate).resolve()


_JSR_PACKAGE_SPECIFIER = "jsr:@bloqr/compiler-core/cli"
_DENO_PERMISSIONS = (
    "--allow-read",
    "--allow-write",
    "--allow-env",
    "--allow-net",
    "--allow-run",
)


def _get_compiler_command(config_path: str, output_path: str) -> tuple[list[str], str]:
    """
    Get the compiler command and working directory.

    Runs adblock-compiler-core (published as @bloqr/compiler-core on
    JSR) via Deno.

    Returns:
        Tuple of (command args, working directory).

    Raises:
        CompilerNotFoundError: If deno is not found.
    """
    deno_path = find_command("deno")

    if deno_path:
        return (
            [
                deno_path,
                "run",
                *_DENO_PERMISSIONS,
                _JSR_PACKAGE_SPECIFIER,
                "--config",
                config_path,
                "--output",
                output_path,
            ],
            str(Path(config_path).parent),
        )

    raise CompilerNotFoundError(["deno"])


class BloqrCompiler:
    """
    Main compiler class for AdGuard filter rules.

    Example:
        >>> compiler = BloqrCompiler()
        >>> result = compiler.compile("config.yaml", copy_to_rules=True)
        >>> if result.success:
        ...     print(f"Compiled {result.rule_count} rules")
    """

    def __init__(self, debug: bool = False):
        """
        Initialize the compiler.

        Args:
            debug: Enable debug logging.
        """
        self.debug = debug
        if debug:
            logging.basicConfig(level=logging.DEBUG)

    def compile(
        self,
        config_path: str | Path,
        output_path: str | Path | None = None,
        copy_to_rules: bool = False,
        rules_directory: str | Path | None = None,
        format: ConfigurationFormat | None = None,
        validate: bool = True,
        fail_on_warnings: bool = False,
        allow_unvalidated_output: bool = False,
        event_dispatcher: EventDispatcher | None = None,
    ) -> CompilerResult:
        """
        Compile filter rules.

        Args:
            config_path: Path to configuration file.
            output_path: Optional output file path.
            copy_to_rules: Copy output to rules directory.
            rules_directory: Custom rules directory path.
            format: Force configuration format.
            validate: Validate configuration before compiling.
            fail_on_warnings: Fail compilation if configuration has validation warnings.
            allow_unvalidated_output: Explicit opt-out of the mandatory rules-validator
                syntax check on compiled output. Security-relevant: leave this False
                (the default) in production - compiled output is validated and
                compilation fails closed by default. Use only for deliberate
                debugging of unvalidated output.
            event_dispatcher: Optional dispatcher for hash-verification events.

        Returns:
            Compilation result.
        """
        return compile_rules(
            config_path=config_path,
            output_path=output_path,
            copy_to_rules=copy_to_rules,
            event_dispatcher=event_dispatcher,
            rules_directory=rules_directory,
            format=format,
            debug=self.debug,
            validate=validate,
            fail_on_warnings=fail_on_warnings,
            allow_unvalidated_output=allow_unvalidated_output,
        )

    def read_config(
        self,
        config_path: str | Path,
        format: ConfigurationFormat | None = None,
    ) -> CompilerConfiguration:
        """
        Read configuration from a file.

        Args:
            config_path: Path to configuration file.
            format: Force configuration format.

        Returns:
            Parsed configuration.
        """
        return read_configuration(config_path, format)

    def validate_config(
        self,
        config: CompilerConfiguration,
        check_files: bool = False,
    ) -> tuple[bool, list[str], list[str]]:
        """
        Validate a configuration.

        Args:
            config: Configuration to validate.
            check_files: Check if local source files exist.

        Returns:
            Tuple of (is_valid, errors, warnings).
        """
        result = config.validate(check_files=check_files)
        return result.is_valid, result.errors, result.warnings

    def get_version_info(self) -> VersionInfo:
        """Get version information for all components."""
        return get_version_info()

    async def compile_async(
        self,
        config_path: str | Path,
        output_path: str | Path | None = None,
        copy_to_rules: bool = False,
        rules_directory: str | Path | None = None,
        format: ConfigurationFormat | None = None,
        validate: bool = True,
        fail_on_warnings: bool = False,
        allow_unvalidated_output: bool = False,
        event_dispatcher: EventDispatcher | None = None,
    ) -> CompilerResult:
        """
        Asynchronously compile filter rules.

        This method provides better performance for I/O-bound operations
        and can be used in async applications.

        Args:
            config_path: Path to configuration file.
            output_path: Optional output file path.
            copy_to_rules: Copy output to rules directory.
            rules_directory: Custom rules directory path.
            format: Force configuration format.
            validate: Validate configuration before compiling.
            fail_on_warnings: Fail compilation if configuration has validation warnings.
            allow_unvalidated_output: Explicit opt-out of the mandatory rules-validator
                syntax check on compiled output. Security-relevant: leave this False
                (the default) in production - compiled output is validated and
                compilation fails closed by default. Use only for deliberate
                debugging of unvalidated output.
            event_dispatcher: Optional dispatcher for hash-verification events.

        Returns:
            Compilation result.

        Example:
            >>> compiler = BloqrCompiler()
            >>> result = await compiler.compile_async("config.yaml")
            >>> if result.success:
            ...     print(f"Compiled {result.rule_count} rules")
        """
        return await compile_rules_async(
            config_path=config_path,
            output_path=output_path,
            copy_to_rules=copy_to_rules,
            rules_directory=rules_directory,
            format=format,
            debug=self.debug,
            validate=validate,
            fail_on_warnings=fail_on_warnings,
            allow_unvalidated_output=allow_unvalidated_output,
            event_dispatcher=event_dispatcher,
        )


def compile_rules(
    config_path: str | Path,
    output_path: str | Path | None = None,
    copy_to_rules: bool = False,
    rules_directory: str | Path | None = None,
    format: ConfigurationFormat | None = None,
    debug: bool = False,
    validate: bool = True,
    fail_on_warnings: bool = False,
    allow_unvalidated_output: bool = False,
    event_dispatcher: EventDispatcher | None = None,
) -> CompilerResult:
    """
    Compile filter rules using adblock-compiler-core (via Deno).

    Args:
        config_path: Path to configuration file.
        output_path: Optional output file path.
        copy_to_rules: Copy output to rules directory.
        rules_directory: Custom rules directory path.
        format: Force configuration format.
        debug: Enable debug logging.
        validate: Validate configuration before compiling.
        fail_on_warnings: Fail compilation if configuration has validation warnings.
        allow_unvalidated_output: Explicit opt-out of the mandatory rules-validator
            syntax check on compiled output. Security-relevant: leave this False (the
            default) in production - compiled output is validated and compilation
            fails closed by default. Use only for deliberate debugging of unvalidated
            output.
        event_dispatcher: Optional dispatcher to raise HashComputed/HashVerified/HashMismatch
            events at each stage (see docs/HASH_VERIFICATION.md). Hash recording/verification
            against the `.hashes.json` sidecar (config.hash_verification) happens regardless
            of whether a dispatcher is supplied - the dispatcher only adds observability.

    Returns:
        Compilation result.
    """
    result = CompilerResult(start_time=datetime.utcnow())
    config_path = Path(config_path).resolve()
    temp_config_path = None

    try:
        # Read configuration
        config = read_configuration(config_path, format)
        result.config_name = config.name
        result.config_version = config.version

        # Stage 1 of the hash-verification pipeline: hash the config file itself for the
        # audit trail. Not checked against the sidecar database - the database's own
        # location and policy live inside this file, so there's nothing to compare against.
        config_hash = compute_hash(config_path)
        asyncio.run(_raise_hash_computed(
            event_dispatcher, str(config_path), "config_file", config_hash, config_path.stat().st_size,
        ))

        # Validate configuration if requested
        if validate:
            validation_result = config.validate()
            if not validation_result.is_valid:
                raise ValidationError(validation_result.errors, validation_result.warnings)
            if validation_result.warnings:
                for warning in validation_result.warnings:
                    logger.warning(f"Config warning: {warning}")
                if fail_on_warnings:
                    raise ValidationError(
                        errors=[],
                        warnings=validation_result.warnings,
                        message="Configuration has warnings (fail_on_warnings is enabled)",
                    )

        # Determine output path
        if output_path:
            actual_output = Path(output_path).resolve()
        else:
            timestamp = datetime.utcnow().strftime("%Y%m%d-%H%M%S")
            output_dir = config_path.parent / "output"
            output_dir.mkdir(exist_ok=True)
            actual_output = output_dir / f"compiled-{timestamp}.txt"

        result.output_path = str(actual_output)

        # Convert to JSON if needed (adblock-compiler-core only supports JSON)
        detected_format = format or config._source_format
        if detected_format != ConfigurationFormat.JSON:
            temp_config_path = tempfile.NamedTemporaryFile(
                mode="w",
                suffix=".json",
                delete=False,
                encoding="utf-8",
            )
            temp_config_path.write(to_json(config))
            temp_config_path.close()
            compile_config_path = temp_config_path.name
            if debug:
                logger.debug(f"Created temp JSON config: {compile_config_path}")
        else:
            compile_config_path = str(config_path)

        # Get compiler command
        cmd, cwd = _get_compiler_command(compile_config_path, str(actual_output))

        if debug:
            logger.debug(f"Running: {' '.join(cmd)}")
            logger.debug(f"Working directory: {cwd}")

        # Run compilation
        try:
            proc = subprocess.run(
                cmd,
                cwd=cwd,
                capture_output=True,
                text=True,
                timeout=300,  # 5 minute timeout
            )
        except subprocess.TimeoutExpired:
            raise CompilerTimeoutError(300, " ".join(cmd))

        result.stdout = proc.stdout
        result.stderr = proc.stderr

        if proc.returncode != 0:
            raise CompilationError(
                f"Compiler exited with code {proc.returncode}",
                exit_code=proc.returncode,
                stdout=proc.stdout,
                stderr=proc.stderr,
            )

        # Verify output was created
        if not actual_output.exists():
            raise OutputNotCreatedError(str(actual_output))

        # Publish to the configured durable destination, if any, applying the conflict
        # strategy and archiving policy before anything downstream (hashing, copying) sees
        # the file.
        if config.output is not None and config.output.path:
            resolved_path = _resolve_relative_to_config(config.output.path, config_path)
            resolved_output = replace(config.output, path=str(resolved_path))
            publish_result = publish_output(actual_output, resolved_output, config.archiving)

            if not publish_result.success or publish_result.final_path is None:
                result.success = False
                result.error_message = publish_result.error_message
                logger.error(f"Failed to publish output: {publish_result.error_message}")
                return result

            actual_output = Path(publish_result.final_path)
            result.output_path = str(actual_output)
            if publish_result.archived_path:
                logger.info(f"Archived previous output to {publish_result.archived_path}")

        # Calculate statistics
        result.rule_count = count_rules(actual_output)
        result.output_hash = compute_hash(actual_output)
        result.success = True

        if debug:
            logger.debug(f"Compiled {result.rule_count} rules")
            logger.debug(f"Output hash: {result.hash_short()}...")

        asyncio.run(_raise_hash_computed(
            event_dispatcher, str(actual_output), "output_file",
            result.output_hash, actual_output.stat().st_size,
        ))

        if (
            config.hash_verification is not None
            and config.hash_verification.mode.lower() != "disabled"
            and config.hash_verification.hash_database_path
        ):
            hash_database_path = _resolve_relative_to_config(
                config.hash_verification.hash_database_path, config_path
            )
            can_continue, error_message = asyncio.run(_verify_and_record_hash(
                hash_database_path, str(actual_output), "output_file", result.output_hash,
                config.hash_verification, event_dispatcher,
            ))
            if not can_continue:
                result.success = False
                result.error_message = error_message
                return result

        can_continue, error_message = asyncio.run(
            _run_rules_validator(
                actual_output, event_dispatcher, allow_unvalidated_output, fail_on_warnings
            )
        )
        if not can_continue:
            result.success = False
            result.error_message = error_message
            return result

        # Copy to rules directory if requested
        if copy_to_rules:
            if rules_directory:
                rules_dir = Path(rules_directory)
            else:
                rules_dir = config_path.parent.parent.parent / "rules"

            try:
                rules_dir.mkdir(exist_ok=True)
                dest_path = rules_dir / "adguard_user_filter.txt"
                shutil.copy2(actual_output, dest_path)
                result.copied_to_rules = True
                result.rules_destination = str(dest_path)
                if debug:
                    logger.debug(f"Copied to: {dest_path}")
            except (OSError, IOError) as e:
                raise CopyError(str(actual_output), str(dest_path), str(e))

            copied_hash = compute_hash(dest_path)
            asyncio.run(_raise_hash_computed(
                event_dispatcher, str(dest_path), "copied_rules_file",
                copied_hash, dest_path.stat().st_size,
            ))

            if (
                config.hash_verification is not None
                and config.hash_verification.mode.lower() != "disabled"
                and config.hash_verification.hash_database_path
            ):
                hash_database_path = _resolve_relative_to_config(
                    config.hash_verification.hash_database_path, config_path
                )
                can_continue, error_message = asyncio.run(_verify_and_record_hash(
                    hash_database_path, str(dest_path), "copied_rules_file", copied_hash,
                    config.hash_verification, event_dispatcher,
                ))
                if not can_continue:
                    result.success = False
                    result.error_message = error_message
                    return result

    except (ValidationError, CompilationError, CompilerNotFoundError,
            OutputNotCreatedError, CopyError, CompilerTimeoutError) as e:
        result.success = False
        result.error_message = str(e)
        logger.error(f"Compilation failed: {e}")

    except Exception as e:
        result.success = False
        result.error_message = str(e)
        logger.error(f"Compilation failed: {e}")

    finally:
        # Clean up temp file
        if temp_config_path and os.path.exists(temp_config_path.name):
            os.unlink(temp_config_path.name)

        result.end_time = datetime.utcnow()
        result.elapsed_ms = int((result.end_time - result.start_time).total_seconds() * 1000)

    return result


async def compile_rules_async(
    config_path: str | Path,
    output_path: str | Path | None = None,
    copy_to_rules: bool = False,
    rules_directory: str | Path | None = None,
    format: ConfigurationFormat | None = None,
    debug: bool = False,
    validate: bool = True,
    fail_on_warnings: bool = False,
    allow_unvalidated_output: bool = False,
    event_dispatcher: EventDispatcher | None = None,
) -> CompilerResult:
    """
    Asynchronously compile filter rules using adblock-compiler-core (via Deno).

    This async version provides better performance for I/O-bound operations
    and allows compilation to be integrated into async applications.

    Args:
        config_path: Path to configuration file.
        output_path: Optional output file path.
        copy_to_rules: Copy output to rules directory.
        rules_directory: Custom rules directory path.
        format: Force configuration format.
        debug: Enable debug logging.
        validate: Validate configuration before compiling.
        fail_on_warnings: Fail compilation if configuration has validation warnings.
        allow_unvalidated_output: Explicit opt-out of the mandatory rules-validator
            syntax check on compiled output. Security-relevant: leave this False (the
            default) in production - compiled output is validated and compilation
            fails closed by default. Use only for deliberate debugging of unvalidated
            output.
        event_dispatcher: Optional dispatcher to raise HashComputed/HashVerified/HashMismatch
            events at each stage (see docs/HASH_VERIFICATION.md). Hash recording/verification
            against the `.hashes.json` sidecar (config.hash_verification) happens regardless
            of whether a dispatcher is supplied - the dispatcher only adds observability.

    Returns:
        Compilation result.
    """
    result = CompilerResult(start_time=datetime.utcnow())
    config_path = Path(config_path).resolve()
    temp_config_path = None

    try:
        # Read configuration
        config = read_configuration(config_path, format)
        result.config_name = config.name
        result.config_version = config.version

        # Stage 1 of the hash-verification pipeline: hash the config file itself for the
        # audit trail. Not checked against the sidecar database - the database's own
        # location and policy live inside this file, so there's nothing to compare against.
        config_hash = await compute_hash_async(config_path)
        await _raise_hash_computed(
            event_dispatcher, str(config_path), "config_file", config_hash, config_path.stat().st_size,
        )

        # Validate configuration if requested
        if validate:
            validation_result = config.validate()
            if not validation_result.is_valid:
                raise ValidationError(validation_result.errors, validation_result.warnings)
            if validation_result.warnings:
                for warning in validation_result.warnings:
                    logger.warning(f"Config warning: {warning}")
                if fail_on_warnings:
                    raise ValidationError(
                        errors=[],
                        warnings=validation_result.warnings,
                        message="Configuration has warnings (fail_on_warnings is enabled)",
                    )

        # Determine output path
        if output_path:
            actual_output = Path(output_path).resolve()
        else:
            timestamp = datetime.utcnow().strftime("%Y%m%d-%H%M%S")
            output_dir = config_path.parent / "output"
            output_dir.mkdir(exist_ok=True)
            actual_output = output_dir / f"compiled-{timestamp}.txt"

        result.output_path = str(actual_output)

        # Convert to JSON if needed (adblock-compiler-core only supports JSON)
        detected_format = format or config._source_format
        if detected_format != ConfigurationFormat.JSON:
            temp_config_path = tempfile.NamedTemporaryFile(
                mode="w",
                suffix=".json",
                delete=False,
                encoding="utf-8",
            )
            temp_config_path.write(to_json(config))
            temp_config_path.close()
            compile_config_path = temp_config_path.name
            if debug:
                logger.debug(f"Created temp JSON config: {compile_config_path}")
        else:
            compile_config_path = str(config_path)

        # Get compiler command
        cmd, cwd = _get_compiler_command(compile_config_path, str(actual_output))

        if debug:
            logger.debug(f"Running: {' '.join(cmd)}")
            logger.debug(f"Working directory: {cwd}")

        # Run compilation asynchronously
        try:
            proc = await asyncio.create_subprocess_exec(
                *cmd,
                cwd=cwd,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
            )
            stdout_bytes, stderr_bytes = await asyncio.wait_for(
                proc.communicate(),
                timeout=300,  # 5 minute timeout
            )
            stdout = stdout_bytes.decode("utf-8") if stdout_bytes else ""
            stderr = stderr_bytes.decode("utf-8") if stderr_bytes else ""
            returncode = proc.returncode
        except asyncio.TimeoutError:
            raise CompilerTimeoutError(300, " ".join(cmd))

        result.stdout = stdout
        result.stderr = stderr

        if returncode != 0:
            raise CompilationError(
                f"Compiler exited with code {returncode}",
                exit_code=returncode,
                stdout=stdout,
                stderr=stderr,
            )

        # Verify output was created
        if not actual_output.exists():
            raise OutputNotCreatedError(str(actual_output))

        # Publish to the configured durable destination, if any, applying the conflict
        # strategy and archiving policy before anything downstream (hashing, copying) sees
        # the file.
        if config.output is not None and config.output.path:
            resolved_path = _resolve_relative_to_config(config.output.path, config_path)
            resolved_output = replace(config.output, path=str(resolved_path))
            loop = asyncio.get_event_loop()
            publish_result = await loop.run_in_executor(
                None, publish_output, actual_output, resolved_output, config.archiving
            )

            if not publish_result.success or publish_result.final_path is None:
                result.success = False
                result.error_message = publish_result.error_message
                logger.error(f"Failed to publish output: {publish_result.error_message}")
                return result

            actual_output = Path(publish_result.final_path)
            result.output_path = str(actual_output)
            if publish_result.archived_path:
                logger.info(f"Archived previous output to {publish_result.archived_path}")

        # Calculate statistics asynchronously
        result.rule_count = await count_rules_async(actual_output)
        result.output_hash = await compute_hash_async(actual_output)
        result.success = True

        if debug:
            logger.debug(f"Compiled {result.rule_count} rules")
            logger.debug(f"Output hash: {result.hash_short()}...")

        await _raise_hash_computed(
            event_dispatcher, str(actual_output), "output_file",
            result.output_hash, actual_output.stat().st_size,
        )

        if (
            config.hash_verification is not None
            and config.hash_verification.mode.lower() != "disabled"
            and config.hash_verification.hash_database_path
        ):
            hash_database_path = _resolve_relative_to_config(
                config.hash_verification.hash_database_path, config_path
            )
            can_continue, error_message = await _verify_and_record_hash(
                hash_database_path, str(actual_output), "output_file", result.output_hash,
                config.hash_verification, event_dispatcher,
            )
            if not can_continue:
                result.success = False
                result.error_message = error_message
                return result

        can_continue, error_message = await _run_rules_validator(
            actual_output, event_dispatcher, allow_unvalidated_output, fail_on_warnings
        )
        if not can_continue:
            result.success = False
            result.error_message = error_message
            return result

        # Copy to rules directory if requested
        if copy_to_rules:
            if rules_directory:
                rules_dir = Path(rules_directory)
            else:
                rules_dir = config_path.parent.parent.parent / "rules"

            try:
                # Run file copy in thread pool to avoid blocking
                loop = asyncio.get_event_loop()
                await loop.run_in_executor(None, rules_dir.mkdir, True, 0o755)
                dest_path = rules_dir / "adguard_user_filter.txt"
                await loop.run_in_executor(None, shutil.copy2, actual_output, dest_path)
                result.copied_to_rules = True
                result.rules_destination = str(dest_path)
                if debug:
                    logger.debug(f"Copied to: {dest_path}")
            except (OSError, IOError) as e:
                raise CopyError(str(actual_output), str(dest_path), str(e))

            copied_hash = await compute_hash_async(dest_path)
            await _raise_hash_computed(
                event_dispatcher, str(dest_path), "copied_rules_file",
                copied_hash, dest_path.stat().st_size,
            )

            if (
                config.hash_verification is not None
                and config.hash_verification.mode.lower() != "disabled"
                and config.hash_verification.hash_database_path
            ):
                hash_database_path = _resolve_relative_to_config(
                    config.hash_verification.hash_database_path, config_path
                )
                can_continue, error_message = await _verify_and_record_hash(
                    hash_database_path, str(dest_path), "copied_rules_file", copied_hash,
                    config.hash_verification, event_dispatcher,
                )
                if not can_continue:
                    result.success = False
                    result.error_message = error_message
                    return result

    except (ValidationError, CompilationError, CompilerNotFoundError,
            OutputNotCreatedError, CopyError, CompilerTimeoutError) as e:
        result.success = False
        result.error_message = str(e)
        logger.error(f"Compilation failed: {e}")

    except Exception as e:
        result.success = False
        result.error_message = str(e)
        logger.error(f"Compilation failed: {e}")

    finally:
        # Clean up temp file
        if temp_config_path and os.path.exists(temp_config_path.name):
            os.unlink(temp_config_path.name)

        result.end_time = datetime.utcnow()
        result.elapsed_ms = int((result.end_time - result.start_time).total_seconds() * 1000)

    return result


def validate_configuration(
    config_path: str | Path,
    format: ConfigurationFormat | None = None,
    check_files: bool = False,
) -> tuple[bool, list[str], list[str]]:
    """
    Validate a configuration file without compiling.

    Args:
        config_path: Path to configuration file.
        format: Force configuration format.
        check_files: Check if local source files exist.

    Returns:
        Tuple of (is_valid, errors, warnings).
    """
    config = read_configuration(config_path, format)
    result = config.validate(check_files=check_files)
    return result.is_valid, result.errors, result.warnings
