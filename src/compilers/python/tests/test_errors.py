"""Tests for the errors module."""

import pytest

from bloqr_compiler.errors import (
    CompilationError,
    CompilerError,
    CompilerNotFoundError,
    ConfigNotFoundError,
    CopyError,
    ErrorCode,
    OutputNotCreatedError,
    ParseError,
    TimeoutError,
    UnknownExtensionError,
    ValidationError,
    ValidationResult,
)


class TestErrorCode:
    """Tests for ErrorCode enum."""

    def test_all_codes_exist(self) -> None:
        codes = [
            ErrorCode.CONFIG_NOT_FOUND,
            ErrorCode.UNKNOWN_EXTENSION,
            ErrorCode.PARSE_ERROR,
            ErrorCode.VALIDATION_FAILED,
            ErrorCode.FILE_SYSTEM_ERROR,
            ErrorCode.COMPILER_NOT_FOUND,
            ErrorCode.COMPILATION_FAILED,
            ErrorCode.OUTPUT_NOT_CREATED,
            ErrorCode.COPY_FAILED,
            ErrorCode.PROCESS_ERROR,
            ErrorCode.TIMEOUT_ERROR,
        ]
        assert len(codes) == 11


class TestCompilerError:
    """Tests for CompilerError base class."""

    def test_basic_error(self) -> None:
        error = CompilerError("Something went wrong")
        assert str(error) == "[COMPILATION_FAILED] Something went wrong"
        assert error.message == "Something went wrong"
        assert error.code == ErrorCode.COMPILATION_FAILED

    def test_with_context(self) -> None:
        error = CompilerError(
            "Error occurred",
            context={"file": "test.txt", "line": 42},
        )
        error_str = str(error)
        assert "Error occurred" in error_str
        assert "file=test.txt" in error_str
        assert "line=42" in error_str

    def test_is_recoverable(self) -> None:
        recoverable = CompilerError("test", code=ErrorCode.CONFIG_NOT_FOUND)
        not_recoverable = CompilerError("test", code=ErrorCode.COMPILATION_FAILED)

        assert recoverable.is_recoverable() is True
        assert not_recoverable.is_recoverable() is False


class TestConfigNotFoundError:
    """Tests for ConfigNotFoundError."""

    def test_basic_error(self) -> None:
        error = ConfigNotFoundError("/path/to/config.json")
        assert error.path == "/path/to/config.json"
        assert error.code == ErrorCode.CONFIG_NOT_FOUND
        assert "not found" in str(error).lower()

    def test_with_searched_paths(self) -> None:
        error = ConfigNotFoundError(
            "/path/to/config.json",
            searched_paths=["/dir1/config.json", "/dir2/config.json"],
        )
        assert error.searched_paths == ["/dir1/config.json", "/dir2/config.json"]


class TestUnknownExtensionError:
    """Tests for UnknownExtensionError."""

    def test_basic_error(self) -> None:
        error = UnknownExtensionError(".txt")
        assert error.extension == ".txt"
        assert error.code == ErrorCode.UNKNOWN_EXTENSION
        assert ".txt" in str(error)

    def test_shows_supported(self) -> None:
        error = UnknownExtensionError(".xml")
        error_str = str(error)
        assert ".json" in error_str or "supported" in error_str.lower()


class TestParseError:
    """Tests for ParseError."""

    def test_basic_error(self) -> None:
        error = ParseError("Invalid syntax", format="json")
        assert error.format == "json"
        assert error.code == ErrorCode.PARSE_ERROR

    def test_with_location(self) -> None:
        error = ParseError(
            "Unexpected token",
            format="json",
            path="/path/to/file.json",
            line=10,
            column=5,
        )
        assert error.line == 10
        assert error.column == 5
        assert error.path == "/path/to/file.json"


class TestValidationError:
    """Tests for ValidationError."""

    def test_single_error(self) -> None:
        error = ValidationError(["Missing name field"])
        assert len(error.errors) == 1
        assert error.code == ErrorCode.VALIDATION_FAILED

    def test_multiple_errors(self) -> None:
        error = ValidationError([
            "Missing name field",
            "No sources defined",
        ])
        assert len(error.errors) == 2

    def test_with_warnings(self) -> None:
        error = ValidationError(
            ["Critical error"],
            warnings=["Minor warning"],
        )
        assert len(error.warnings) == 1


class TestCompilerNotFoundError:
    """Tests for CompilerNotFoundError."""

    def test_basic_error(self) -> None:
        error = CompilerNotFoundError()
        assert error.code == ErrorCode.COMPILER_NOT_FOUND
        assert "deno" in str(error).lower()

    def test_with_searched(self) -> None:
        error = CompilerNotFoundError(["deno"])
        assert error.searched_commands == ["deno"]

    def test_message_reflects_searched_commands(self) -> None:
        # Regression test: the message must be built from searched_commands, not
        # hardcoded to a tool that may not be the one actually being looked for - see #424.
        error = CompilerNotFoundError(["some-other-tool"])
        assert "some-other-tool" in str(error)
        assert "hostlist-compiler" not in str(error).lower()


class TestCompilationError:
    """Tests for CompilationError."""

    def test_basic_error(self) -> None:
        error = CompilationError("Compilation failed")
        assert error.code == ErrorCode.COMPILATION_FAILED

    def test_with_details(self) -> None:
        error = CompilationError(
            "Process failed",
            exit_code=1,
            stdout="output",
            stderr="error message",
        )
        assert error.exit_code == 1
        assert error.stdout == "output"
        assert error.stderr == "error message"


class TestOutputNotCreatedError:
    """Tests for OutputNotCreatedError."""

    def test_basic_error(self) -> None:
        error = OutputNotCreatedError("/path/to/output.txt")
        assert error.expected_path == "/path/to/output.txt"
        assert error.code == ErrorCode.OUTPUT_NOT_CREATED


class TestCopyError:
    """Tests for CopyError."""

    def test_basic_error(self) -> None:
        error = CopyError("/source/file.txt", "/dest/file.txt")
        assert error.source == "/source/file.txt"
        assert error.destination == "/dest/file.txt"
        assert error.code == ErrorCode.COPY_FAILED

    def test_with_reason(self) -> None:
        error = CopyError("/source", "/dest", "Permission denied")
        assert "Permission denied" in str(error)


class TestTimeoutError:
    """Tests for TimeoutError."""

    def test_basic_error(self) -> None:
        error = TimeoutError(300)
        assert error.timeout_seconds == 300
        assert error.code == ErrorCode.TIMEOUT_ERROR
        assert "300" in str(error)


class TestValidationResult:
    """Tests for ValidationResult dataclass."""

    def test_starts_valid(self) -> None:
        result = ValidationResult()
        assert result.is_valid is True
        assert len(result.errors) == 0
        assert len(result.warnings) == 0

    def test_add_error(self) -> None:
        result = ValidationResult()
        result.add_error("Something is wrong")

        assert result.is_valid is False
        assert len(result.errors) == 1
        assert result.errors[0] == "Something is wrong"

    def test_add_warning(self) -> None:
        result = ValidationResult()
        result.add_warning("Minor issue")

        assert result.is_valid is True  # Warnings don't invalidate
        assert len(result.warnings) == 1

    def test_raise_if_invalid(self) -> None:
        result = ValidationResult()
        result.add_error("Error")

        with pytest.raises(ValidationError):
            result.raise_if_invalid()

    def test_raise_if_invalid_valid(self) -> None:
        result = ValidationResult()
        result.add_warning("Warning")

        # Should not raise
        result.raise_if_invalid()
