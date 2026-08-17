"""
Tests for the bloqr-validator CLI wiring helpers in bloqr_compiler.compiler (#361).

`_run_rules_validator` shells out to the `bloqr-validate` binary rather than binding
against the native library directly, so these tests mock `find_rules_validate_binary`
and `subprocess.run` rather than requiring a real compiled binary in the test
environment - consistent with the rest of this suite not exercising `compile_rules`
end-to-end (see test_hash_verification_wiring.py's docstring).
"""

import asyncio
import json
import subprocess
from pathlib import Path

import pytest

from bloqr_compiler import compiler as compiler_module
from bloqr_compiler.compiler import _run_rules_validator, find_rules_validate_binary
from bloqr_compiler.events import CompilationEventHandler, EventDispatcher, ValidationEventArgs


class RecordingHandler(CompilationEventHandler):
    def __init__(self) -> None:
        self.validations: list[ValidationEventArgs] = []

    async def on_validation(self, args: ValidationEventArgs) -> None:
        self.validations.append(args)


def _fake_completed_process(stdout: str, returncode: int = 0) -> subprocess.CompletedProcess:
    return subprocess.CompletedProcess(args=["bloqr-validate"], returncode=returncode, stdout=stdout, stderr="")


class TestFindRulesValidateBinary:
    def test_env_override_wins_when_file_exists(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        fake_binary = tmp_path / "bloqr-validate"
        fake_binary.write_text("#!/bin/sh\n")
        monkeypatch.setenv("RULES_VALIDATE_PATH", str(fake_binary))

        assert find_rules_validate_binary() == str(fake_binary)

    def test_returns_none_when_not_found_anywhere(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.delenv("RULES_VALIDATE_PATH", raising=False)
        monkeypatch.setattr(compiler_module.shutil, "which", lambda _cmd: None)
        # Point the module's own __file__ at a fake repo layout under tmp_path so the
        # dev-convenience target/release|debug fallback candidates don't exist either.
        fake_file = tmp_path / "src" / "compilers" / "python" / "bloqr_compiler" / "compiler.py"
        monkeypatch.setattr(compiler_module, "__file__", str(fake_file))

        assert find_rules_validate_binary() is None


class TestRunRulesValidator:
    def test_no_op_when_binary_not_found(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.setattr(compiler_module, "find_rules_validate_binary", lambda: None)
        output = tmp_path / "output.txt"
        output.write_text("||example.com^\n")

        can_continue, error = asyncio.run(_run_rules_validator(output, None))

        assert can_continue
        assert error is None

    def test_valid_syntax_raises_no_findings_and_continues(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.setattr(compiler_module, "find_rules_validate_binary", lambda: "bloqr-validate")
        payload = json.dumps(
            {"is_valid": True, "format": "adblock", "valid_rules": 1, "invalid_rules": 0, "messages": []}
        )
        monkeypatch.setattr(subprocess, "run", lambda *a, **k: _fake_completed_process(payload))

        output = tmp_path / "output.txt"
        output.write_text("||example.com^\n")
        handler = RecordingHandler()
        dispatcher = EventDispatcher()
        dispatcher.add_handler(handler)

        can_continue, error = asyncio.run(_run_rules_validator(output, dispatcher))

        assert can_continue
        assert error is None
        assert len(handler.validations) == 1
        assert handler.validations[0].passed
        assert handler.validations[0].items_validated == 1

    def test_invalid_syntax_with_messages_raises_error_findings(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.setattr(compiler_module, "find_rules_validate_binary", lambda: "bloqr-validate")
        payload = json.dumps(
            {
                "is_valid": False,
                "format": "adblock",
                "valid_rules": 1,
                "invalid_rules": 1,
                "messages": ["line 2: malformed rule"],
            }
        )
        monkeypatch.setattr(subprocess, "run", lambda *a, **k: _fake_completed_process(payload, returncode=1))

        output = tmp_path / "output.txt"
        output.write_text("||example.com^\nbad rule\n")
        handler = RecordingHandler()
        dispatcher = EventDispatcher()
        dispatcher.add_handler(handler)

        can_continue, error = asyncio.run(_run_rules_validator(output, dispatcher))

        # Findings are informational by default - only an explicit handler abort should stop compilation.
        assert can_continue
        assert error is None
        assert not handler.validations[0].passed

    def test_handler_can_abort_on_findings(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.setattr(compiler_module, "find_rules_validate_binary", lambda: "bloqr-validate")
        payload = json.dumps(
            {
                "is_valid": False,
                "format": "adblock",
                "valid_rules": 0,
                "invalid_rules": 1,
                "messages": [],
            }
        )
        monkeypatch.setattr(subprocess, "run", lambda *a, **k: _fake_completed_process(payload, returncode=1))

        class AbortingHandler(CompilationEventHandler):
            async def on_validation(self, args: ValidationEventArgs) -> None:
                args.abort = True
                args.abort_reason = "aborted by test handler"

        output = tmp_path / "output.txt"
        output.write_text("bad rule\n")
        dispatcher = EventDispatcher()
        dispatcher.add_handler(AbortingHandler())

        can_continue, error = asyncio.run(_run_rules_validator(output, dispatcher))

        assert not can_continue
        assert error == "aborted by test handler"

    def test_subprocess_error_is_swallowed(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.setattr(compiler_module, "find_rules_validate_binary", lambda: "bloqr-validate")

        def _raise(*_args, **_kwargs):
            raise OSError("binary not executable")

        monkeypatch.setattr(subprocess, "run", _raise)

        output = tmp_path / "output.txt"
        output.write_text("||example.com^\n")

        can_continue, error = asyncio.run(_run_rules_validator(output, None))

        assert can_continue
        assert error is None
