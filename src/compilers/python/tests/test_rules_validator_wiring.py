"""
Tests for the bloqr-validator CLI wiring helpers in bloqr_compiler.compiler (#361).

`_run_rules_validator` shells out to the `bloqr-validate` binary rather than binding
against the native library directly, so these tests mock `find_rules_validate_binary`
and `subprocess.run` rather than requiring a real compiled binary in the test
environment - consistent with the rest of this suite not exercising `compile_rules`
end-to-end (see test_hash_verification_wiring.py's docstring).
"""

from __future__ import annotations

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
    def test_fails_closed_by_default_when_binary_not_found(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.setattr(compiler_module, "find_rules_validate_binary", lambda: None)
        output = tmp_path / "output.txt"
        output.write_text("||example.com^\n")

        can_continue, error = asyncio.run(_run_rules_validator(output, None))

        assert not can_continue
        assert error is not None
        assert "not found" in error

    def test_skips_missing_binary_when_allow_unvalidated_set(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.setattr(compiler_module, "find_rules_validate_binary", lambda: None)
        output = tmp_path / "output.txt"
        output.write_text("||example.com^\n")

        can_continue, error = asyncio.run(
            _run_rules_validator(output, None, allow_unvalidated=True)
        )

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

    def test_engine_is_forwarded_to_the_cli_when_set(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.setattr(compiler_module, "find_rules_validate_binary", lambda: "bloqr-validate")
        payload = json.dumps(
            {"is_valid": True, "format": "adblock", "valid_rules": 1, "invalid_rules": 0, "messages": []}
        )
        commands: list[list[str]] = []

        def fake_run(command: list[str], **_kwargs: object) -> subprocess.CompletedProcess:
            commands.append(command)
            return _fake_completed_process(payload)

        monkeypatch.setattr(subprocess, "run", fake_run)
        output = tmp_path / "output.browser.txt"
        output.write_text("example.com##.ad\n")

        can_continue, _ = asyncio.run(_run_rules_validator(output, None, engine="browser"))

        assert can_continue
        assert commands[0][-2:] == ["--engine", "browser"]

    def test_engine_flag_is_omitted_when_not_set(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.setattr(compiler_module, "find_rules_validate_binary", lambda: "bloqr-validate")
        payload = json.dumps(
            {"is_valid": True, "format": "adblock", "valid_rules": 1, "invalid_rules": 0, "messages": []}
        )
        commands: list[list[str]] = []

        def fake_run(command: list[str], **_kwargs: object) -> subprocess.CompletedProcess:
            commands.append(command)
            return _fake_completed_process(payload)

        monkeypatch.setattr(subprocess, "run", fake_run)
        output = tmp_path / "output.txt"
        output.write_text("||example.com^\n")

        asyncio.run(_run_rules_validator(output, None))

        assert "--engine" not in commands[0]

    def test_invalid_syntax_with_messages_fails_closed_by_default(
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

        # Fail-closed by default: an Error finding aborts even though no handler
        # was registered to explicitly set abort - this is the gap the
        # fail-closed rewrite closes.
        assert not can_continue
        assert error is not None
        assert not handler.validations[0].passed

    def test_invalid_syntax_does_not_abort_when_allow_unvalidated_set(
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

        can_continue, error = asyncio.run(
            _run_rules_validator(output, dispatcher, allow_unvalidated=True)
        )

        # Explicit opt-out reverts to legacy behavior: only a handler-set abort counts.
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

    def test_subprocess_error_fails_closed_by_default(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.setattr(compiler_module, "find_rules_validate_binary", lambda: "bloqr-validate")

        def _raise(*_args, **_kwargs):
            raise OSError("binary not executable")

        monkeypatch.setattr(subprocess, "run", _raise)

        output = tmp_path / "output.txt"
        output.write_text("||example.com^\n")

        can_continue, error = asyncio.run(_run_rules_validator(output, None))

        assert not can_continue
        assert error is not None

    def test_subprocess_error_is_swallowed_when_allow_unvalidated_set(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.setattr(compiler_module, "find_rules_validate_binary", lambda: "bloqr-validate")

        def _raise(*_args, **_kwargs):
            raise OSError("binary not executable")

        monkeypatch.setattr(subprocess, "run", _raise)

        output = tmp_path / "output.txt"
        output.write_text("||example.com^\n")

        can_continue, error = asyncio.run(
            _run_rules_validator(output, None, allow_unvalidated=True)
        )

        assert can_continue
        assert error is None

    def test_fail_on_warnings_aborts_on_warning_findings(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.setattr(compiler_module, "find_rules_validate_binary", lambda: "bloqr-validate")
        payload = json.dumps(
            {
                "is_valid": True,
                "format": "adblock",
                "valid_rules": 3,
                "invalid_rules": 0,
                "messages": ["suspicious but syntactically valid rule at line 2"],
            }
        )
        monkeypatch.setattr(subprocess, "run", lambda *a, **k: _fake_completed_process(payload))

        output = tmp_path / "output.txt"
        output.write_text("||example.com^\n")

        can_continue, error = asyncio.run(
            _run_rules_validator(output, None, fail_on_warnings=True)
        )

        assert not can_continue
        assert error is not None


class TestCompileRulesValidatesBrowserArtifactWithBrowserEngine:
    """`compile_rules`/`compile_rules_async` must validate the browser-syntax artifact with
    `engine="browser"` and the DNS artifact without an engine (BLO-459).

    The compiler subprocess is replaced with a tiny script that writes both artifacts, and
    `_run_rules_validator` with a recorder, so the assertions pin the argument each call
    site passes rather than the validator's own behavior (covered above).
    """

    @staticmethod
    def _wire(
        tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> tuple[Path, Path, Path, list[tuple[str, str | None]]]:
        import sys

        config = tmp_path / "config.json"
        config.write_text(
            json.dumps({"name": "t", "sources": [{"source": "https://example.com/list.txt"}]})
        )
        dns_output = tmp_path / "output.txt"
        browser_output = tmp_path / "output.browser.txt"
        script = tmp_path / "fake_compiler.py"
        script.write_text(
            "import sys\n"
            "open(sys.argv[1], 'w').write('||example.com^\\n')\n"
            "open(sys.argv[2], 'w').write('example.com##.ad\\n')\n"
        )

        def fake_command(
            _config: str, output: str, _engine: str | None, browser: str | None
        ) -> tuple[list[str], str]:
            return [sys.executable, str(script), output, str(browser)], str(tmp_path)

        calls: list[tuple[str, str | None]] = []

        async def fake_validator(
            output_path: Path,
            _dispatcher: EventDispatcher | None,
            _allow_unvalidated: bool = False,
            _fail_on_warnings: bool = False,
            engine: str | None = None,
        ) -> tuple[bool, str | None]:
            calls.append((output_path.name, engine))
            return True, None

        monkeypatch.setattr(compiler_module, "_get_compiler_command", fake_command)
        monkeypatch.setattr(compiler_module, "_run_rules_validator", fake_validator)
        return config, dns_output, browser_output, calls

    def test_sync_compile_validates_browser_artifact_with_browser_engine(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        config, dns_output, browser_output, calls = self._wire(tmp_path, monkeypatch)

        result = compiler_module.compile_rules(
            config, output_path=dns_output, browser_output_path=browser_output, validate=False
        )

        assert result.success, result.error_message
        assert calls == [("output.txt", None), ("output.browser.txt", "browser")]

    def test_async_compile_validates_browser_artifact_with_browser_engine(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        config, dns_output, browser_output, calls = self._wire(tmp_path, monkeypatch)

        result = asyncio.run(
            compiler_module.compile_rules_async(
                config, output_path=dns_output, browser_output_path=browser_output, validate=False
            )
        )

        assert result.success, result.error_message
        assert calls == [("output.txt", None), ("output.browser.txt", "browser")]
