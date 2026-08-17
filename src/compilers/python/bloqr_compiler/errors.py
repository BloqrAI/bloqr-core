"""
Custom error types for the rules compiler.

This module provides structured error handling with specific error types
for different failure scenarios, enabling better error messages and recovery.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import Any


class ErrorCode(Enum):
    """Error codes for categorizing compiler errors."""
    CONFIG_NOT_FOUND = "CONFIG_NOT_FOUND"
    UNKNOWN_EXTENSION = "UNKNOWN_EXTENSION"
    PARSE_ERROR = "PARSE_ERROR"
    VALIDATION_FAILED = "VALIDATION_FAILED"
    FILE_SYSTEM_ERROR = "FILE_SYSTEM_ERROR"
    COMPILER_NOT_FOUND = "COMPILER_NOT_FOUND"
    COMPILATION_FAILED = "COMPILATION_FAILED"
    OUTPUT_NOT_CREATED = "OUTPUT_NOT_CREATED"
    COPY_FAILED = "COPY_FAILED"
    PROCESS_ERROR = "PROCESS_ERROR"
    TIMEOUT_ERROR = "TIMEOUT_ERROR"


class CompilerError(Exception):
    """Base exception for all compiler errors."""

    def __init__(
        self,
        message: str,
        code: ErrorCode = ErrorCode.COMPILATION_FAILED,
        context: dict[str, Any] | None = None,
        cause: Exception | None = None,
    ):
        super().__init__(message)
        self.message = message
        self.code = code
        self.context = context or {}
        self.cause = cause

    def is_recoverable(self) -> bool:
        """Check if this error is potentially recoverable by the user."""
        recoverable_codes = {
            ErrorCode.CONFIG_NOT_FOUND,
            ErrorCode.UNKNOWN_EXTENSION,
            ErrorCode.VALIDATION_FAILED,
            ErrorCode.COMPILER_NOT_FOUND,
        }
        return self.code in recoverable_codes

    def __str__(self) -> str:
        base = f"[{self.code.value}] {self.message}"
        if self.context:
            context_str = ", ".join(f"{k}={v}" for k, v in self.context.items())
            base += f" ({context_str})"
        return base


class ConfigNotFoundError(CompilerError):
    """Raised when a configuration file cannot be found."""

    def __init__(self, path: str, searched_paths: list[str] | None = None):
        context = {"path": path}
        if searched_paths:
            context["searched"] = searched_paths
        super().__init__(
            f"Configuration file not found: {path}",
            code=ErrorCode.CONFIG_NOT_FOUND,
            context=context,
        )
        self.path = path
        self.searched_paths = searched_paths or []


class UnknownExtensionError(CompilerError):
    """Raised when a file has an unrecognized extension."""

    def __init__(self, extension: str, supported: list[str] | None = None):
        supported = supported or [".json", ".yaml", ".yml", ".toml"]
        super().__init__(
            f"Unknown configuration file extension: {extension}. "
            f"Supported: {', '.join(supported)}",
            code=ErrorCode.UNKNOWN_EXTENSION,
            context={"extension": extension, "supported": supported},
        )
        self.extension = extension
        self.supported = supported


class ParseError(CompilerError):
    """Raised when configuration parsing fails."""

    def __init__(
        self,
        message: str,
        format: str,
        path: str | None = None,
        line: int | None = None,
        column: int | None = None,
    ):
        context: dict[str, Any] = {"format": format}
        if path:
            context["path"] = path
        if line is not None:
            context["line"] = line
        if column is not None:
            context["column"] = column

        super().__init__(
            message,
            code=ErrorCode.PARSE_ERROR,
            context=context,
        )
        self.format = format
        self.path = path
        self.line = line
        self.column = column


class ValidationError(CompilerError):
    """Raised when configuration validation fails."""

    def __init__(self, errors: list[str], warnings: list[str] | None = None):
        message = "Configuration validation failed:\n" + "\n".join(f"  - {e}" for e in errors)
        if warnings:
            message += "\nWarnings:\n" + "\n".join(f"  - {w}" for w in warnings)

        super().__init__(
            message,
            code=ErrorCode.VALIDATION_FAILED,
            context={"error_count": len(errors), "warning_count": len(warnings or [])},
        )
        self.errors = errors
        self.warnings = warnings or []


class CompilerNotFoundError(CompilerError):
    """Raised when hostlist-compiler is not installed."""

    def __init__(self, searched_commands: list[str] | None = None):
        searched = searched_commands or ["hostlist-compiler", "npx"]
        super().__init__(
            "hostlist-compiler not found. Install with: npm install -g @adguard/hostlist-compiler",
            code=ErrorCode.COMPILER_NOT_FOUND,
            context={"searched_commands": searched},
        )
        self.searched_commands = searched


class CompilationError(CompilerError):
    """Raised when the compilation process fails."""

    def __init__(
        self,
        message: str,
        exit_code: int | None = None,
        stdout: str = "",
        stderr: str = "",
    ):
        context: dict[str, Any] = {}
        if exit_code is not None:
            context["exit_code"] = exit_code
        if stderr:
            context["stderr_preview"] = stderr[:200] + "..." if len(stderr) > 200 else stderr

        super().__init__(
            message,
            code=ErrorCode.COMPILATION_FAILED,
            context=context,
        )
        self.exit_code = exit_code
        self.stdout = stdout
        self.stderr = stderr


class OutputNotCreatedError(CompilerError):
    """Raised when compilation succeeds but output file is not created."""

    def __init__(self, expected_path: str):
        super().__init__(
            f"Compilation completed but output file was not created: {expected_path}",
            code=ErrorCode.OUTPUT_NOT_CREATED,
            context={"expected_path": expected_path},
        )
        self.expected_path = expected_path


class CopyError(CompilerError):
    """Raised when copying to rules directory fails."""

    def __init__(self, source: str, destination: str, reason: str = ""):
        message = f"Failed to copy output to rules directory: {source} -> {destination}"
        if reason:
            message += f" ({reason})"
        super().__init__(
            message,
            code=ErrorCode.COPY_FAILED,
            context={"source": source, "destination": destination},
        )
        self.source = source
        self.destination = destination


class TimeoutError(CompilerError):
    """Raised when compilation times out."""

    def __init__(self, timeout_seconds: int, command: str = ""):
        super().__init__(
            f"Compilation timed out after {timeout_seconds} seconds",
            code=ErrorCode.TIMEOUT_ERROR,
            context={"timeout_seconds": timeout_seconds, "command": command},
        )
        self.timeout_seconds = timeout_seconds
        self.command = command


@dataclass
class ValidationResult:
    """Result of configuration validation."""
    is_valid: bool = True
    errors: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)

    def add_error(self, message: str) -> None:
        """Add an error message and mark as invalid."""
        self.errors.append(message)
        self.is_valid = False

    def add_warning(self, message: str) -> None:
        """Add a warning message."""
        self.warnings.append(message)

    def raise_if_invalid(self) -> None:
        """Raise ValidationError if validation failed."""
        if not self.is_valid:
            raise ValidationError(self.errors, self.warnings)
