"""
Tests for the hash-verification wiring helpers in rules_compiler.compiler (#273).

These exercise `_verify_and_record_hash` and `_raise_hash_computed` directly rather than a
full `compile_rules` run, since that requires a real `deno`/`@bloqr/compiler-core` toolchain
that isn't available in this test environment (consistent with the rest of this test suite,
which does not exercise `compile_rules` end-to-end either).
"""

import asyncio
from pathlib import Path

import pytest

from rules_compiler.compiler import _raise_hash_computed, _verify_and_record_hash
from rules_compiler.config import HashVerificationSettings
from rules_compiler.events import (
    CompilationEventHandler,
    EventDispatcher,
    HashComputedEventArgs,
    HashMismatchEventArgs,
    HashVerifiedEventArgs,
)
from rules_compiler.hash_database import load_hash_database


class RecordingHandler(CompilationEventHandler):
    def __init__(self) -> None:
        self.computed: list[HashComputedEventArgs] = []
        self.verified: list[HashVerifiedEventArgs] = []
        self.mismatched: list[HashMismatchEventArgs] = []

    async def on_hash_computed(self, args: HashComputedEventArgs) -> None:
        self.computed.append(args)

    async def on_hash_verified(self, args: HashVerifiedEventArgs) -> None:
        self.verified.append(args)

    async def on_hash_mismatch(self, args: HashMismatchEventArgs) -> None:
        self.mismatched.append(args)


class TestRaiseHashComputed:
    def test_no_op_without_a_dispatcher(self) -> None:
        # Should not raise even with event_dispatcher=None.
        asyncio.run(_raise_hash_computed(None, "item", "output_file", "a" * 96, 10))

    def test_raises_the_event_to_registered_handlers(self) -> None:
        handler = RecordingHandler()
        dispatcher = EventDispatcher()
        dispatcher.add_handler(handler)

        asyncio.run(_raise_hash_computed(dispatcher, "item", "output_file", "a" * 96, 10))

        assert len(handler.computed) == 1
        assert handler.computed[0].item_identifier == "item"
        assert handler.computed[0].hash == "a" * 96


class TestVerifyAndRecordHash:
    def test_first_time_item_bootstraps_trust_without_verifying(self, tmp_path: Path) -> None:
        db_path = tmp_path / ".hashes.json"
        item = tmp_path / "output.txt"
        item.write_text("content")
        settings = HashVerificationSettings(mode="warning", hash_database_path=str(db_path))

        can_continue, error = asyncio.run(
            _verify_and_record_hash(db_path, str(item), "output_file", "a" * 96, settings, None)
        )

        assert can_continue
        assert error is None
        entries = load_hash_database(db_path)
        assert entries[str(item)].hash == "a" * 96

    def test_matching_hash_raises_verified_and_continues(self, tmp_path: Path) -> None:
        db_path = tmp_path / ".hashes.json"
        item = tmp_path / "output.txt"
        item.write_text("content")
        settings = HashVerificationSettings(mode="warning", hash_database_path=str(db_path))
        asyncio.run(_verify_and_record_hash(db_path, str(item), "output_file", "a" * 96, settings, None))

        handler = RecordingHandler()
        dispatcher = EventDispatcher()
        dispatcher.add_handler(handler)
        can_continue, error = asyncio.run(
            _verify_and_record_hash(db_path, str(item), "output_file", "a" * 96, settings, dispatcher)
        )

        assert can_continue
        assert error is None
        assert len(handler.verified) == 1
        assert handler.verified[0].actual_hash == "a" * 96

    def test_mismatch_in_warning_mode_continues_and_records_new_hash(self, tmp_path: Path) -> None:
        db_path = tmp_path / ".hashes.json"
        item = tmp_path / "output.txt"
        item.write_text("content")
        settings = HashVerificationSettings(mode="warning", hash_database_path=str(db_path))
        asyncio.run(_verify_and_record_hash(db_path, str(item), "output_file", "a" * 96, settings, None))

        can_continue, error = asyncio.run(
            _verify_and_record_hash(db_path, str(item), "output_file", "b" * 96, settings, None)
        )

        assert can_continue
        assert error is None
        entries = load_hash_database(db_path)
        assert entries[str(item)].hash == "b" * 96

    def test_mismatch_in_strict_mode_aborts_and_does_not_record(self, tmp_path: Path) -> None:
        db_path = tmp_path / ".hashes.json"
        item = tmp_path / "output.txt"
        item.write_text("content")
        settings = HashVerificationSettings(mode="strict", hash_database_path=str(db_path))
        asyncio.run(_verify_and_record_hash(db_path, str(item), "output_file", "a" * 96, settings, None))

        can_continue, error = asyncio.run(
            _verify_and_record_hash(db_path, str(item), "output_file", "b" * 96, settings, None)
        )

        assert not can_continue
        assert error is not None
        entries = load_hash_database(db_path)
        assert entries[str(item)].hash == "a" * 96  # unchanged - mismatch aborted before recording

    def test_fail_on_mismatch_aborts_even_in_warning_mode(self, tmp_path: Path) -> None:
        db_path = tmp_path / ".hashes.json"
        item = tmp_path / "output.txt"
        item.write_text("content")
        settings = HashVerificationSettings(
            mode="warning", fail_on_mismatch=True, hash_database_path=str(db_path)
        )
        asyncio.run(_verify_and_record_hash(db_path, str(item), "output_file", "a" * 96, settings, None))

        can_continue, error = asyncio.run(
            _verify_and_record_hash(db_path, str(item), "output_file", "b" * 96, settings, None)
        )

        assert not can_continue

    def test_mismatch_dispatches_to_handlers(self, tmp_path: Path) -> None:
        db_path = tmp_path / ".hashes.json"
        item = tmp_path / "output.txt"
        item.write_text("content")
        settings = HashVerificationSettings(mode="warning", hash_database_path=str(db_path))
        asyncio.run(_verify_and_record_hash(db_path, str(item), "output_file", "a" * 96, settings, None))

        handler = RecordingHandler()
        dispatcher = EventDispatcher()
        dispatcher.add_handler(handler)
        asyncio.run(
            _verify_and_record_hash(db_path, str(item), "output_file", "b" * 96, settings, dispatcher)
        )

        assert len(handler.mismatched) == 1
        assert handler.mismatched[0].expected_hash == "a" * 96
        assert handler.mismatched[0].actual_hash == "b" * 96
