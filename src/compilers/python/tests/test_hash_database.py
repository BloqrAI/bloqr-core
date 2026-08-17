"""Tests for the .hashes.json sidecar database module."""

import json
from pathlib import Path

from bloqr_compiler.hash_database import (
    HashDatabaseEntry,
    load_hash_database,
    record_hash,
)


class TestLoadHashDatabase:
    def test_returns_empty_dict_when_file_missing(self, tmp_path: Path) -> None:
        entries = load_hash_database(tmp_path / "does-not-exist.json")

        assert entries == {}

    def test_loads_recorded_entries(self, tmp_path: Path) -> None:
        db_path = tmp_path / ".hashes.json"
        record_hash(
            db_path,
            "/abs/path/output.txt",
            HashDatabaseEntry(hash="a" * 96, size_bytes=1024, item_type="output_file"),
        )

        entries = load_hash_database(db_path)

        assert "/abs/path/output.txt" in entries
        assert entries["/abs/path/output.txt"].hash == "a" * 96
        assert entries["/abs/path/output.txt"].size_bytes == 1024
        assert entries["/abs/path/output.txt"].item_type == "output_file"


class TestRecordHash:
    def test_creates_parent_directory(self, tmp_path: Path) -> None:
        db_path = tmp_path / "nested" / "dir" / ".hashes.json"

        record_hash(db_path, "item", HashDatabaseEntry(hash="b" * 96, size_bytes=1))

        assert db_path.exists()

    def test_preserves_existing_entries_when_adding_a_new_one(self, tmp_path: Path) -> None:
        db_path = tmp_path / ".hashes.json"
        record_hash(db_path, "item-1", HashDatabaseEntry(hash="a" * 96, size_bytes=1))
        record_hash(db_path, "item-2", HashDatabaseEntry(hash="b" * 96, size_bytes=2))

        entries = load_hash_database(db_path)

        assert set(entries.keys()) == {"item-1", "item-2"}

    def test_overwrites_existing_entry_for_the_same_item(self, tmp_path: Path) -> None:
        db_path = tmp_path / ".hashes.json"
        record_hash(db_path, "item", HashDatabaseEntry(hash="a" * 96, size_bytes=1))
        record_hash(db_path, "item", HashDatabaseEntry(hash="c" * 96, size_bytes=3))

        entries = load_hash_database(db_path)

        assert len(entries) == 1
        assert entries["item"].hash == "c" * 96

    def test_writes_valid_json_matching_the_documented_format(self, tmp_path: Path) -> None:
        db_path = tmp_path / ".hashes.json"
        record_hash(
            db_path, "/abs/output.txt",
            HashDatabaseEntry(hash="d" * 96, size_bytes=48213, item_type="output_file"),
        )

        raw = json.loads(db_path.read_text())

        assert set(raw["/abs/output.txt"].keys()) == {"hash", "sizeBytes", "computedAt", "itemType"}
        assert raw["/abs/output.txt"]["hash"] == "d" * 96
