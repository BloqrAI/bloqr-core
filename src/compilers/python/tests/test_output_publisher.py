"""Tests for the output conflict-strategy/archiving publisher module."""

from pathlib import Path

from bloqr_compiler.config import ArchivingSettings, OutputSettings
from bloqr_compiler.output_publisher import publish_output


class TestPublishOutput:
    def test_copies_to_destination_when_nothing_exists_there_yet(self, tmp_path: Path) -> None:
        compiled = tmp_path / "compiled.txt"
        compiled.write_text("rules")
        destination = tmp_path / "published" / "output.txt"

        result = publish_output(compiled, OutputSettings(path=str(destination)), None)

        assert result.success
        assert Path(result.final_path).read_text() == "rules"

    def test_returns_source_path_unchanged_when_output_path_is_blank(self, tmp_path: Path) -> None:
        compiled = tmp_path / "compiled.txt"
        compiled.write_text("rules")

        result = publish_output(compiled, OutputSettings(path=""), None)

        assert result.success
        assert result.final_path == str(compiled)

    def test_error_strategy_fails_when_destination_already_exists(self, tmp_path: Path) -> None:
        compiled = tmp_path / "compiled.txt"
        compiled.write_text("new")
        destination = tmp_path / "output.txt"
        destination.write_text("old")

        result = publish_output(
            compiled, OutputSettings(path=str(destination), conflict_strategy="error"), None
        )

        assert not result.success
        assert "already exists" in result.error_message
        assert destination.read_text() == "old"

    def test_rename_strategy_leaves_existing_file_untouched(self, tmp_path: Path) -> None:
        compiled = tmp_path / "compiled.txt"
        compiled.write_text("new")
        destination = tmp_path / "output.txt"
        destination.write_text("old")

        result = publish_output(
            compiled, OutputSettings(path=str(destination), conflict_strategy="rename"), None
        )

        assert result.success
        assert destination.read_text() == "old"
        assert Path(result.final_path).name == "output_1.txt"
        assert Path(result.final_path).read_text() == "new"

    def test_rename_strategy_increments_past_existing_numbered_files(self, tmp_path: Path) -> None:
        compiled = tmp_path / "compiled.txt"
        compiled.write_text("new")
        destination = tmp_path / "output.txt"
        destination.write_text("old")
        (tmp_path / "output_1.txt").write_text("also old")

        result = publish_output(
            compiled, OutputSettings(path=str(destination), conflict_strategy="rename"), None
        )

        assert Path(result.final_path).name == "output_2.txt"

    def test_overwrite_strategy_without_archiving_replaces_the_file(self, tmp_path: Path) -> None:
        compiled = tmp_path / "compiled.txt"
        compiled.write_text("new")
        destination = tmp_path / "output.txt"
        destination.write_text("old")

        result = publish_output(
            compiled, OutputSettings(path=str(destination), conflict_strategy="overwrite"), None
        )

        assert result.success
        assert result.archived_path is None
        assert destination.read_text() == "new"

    def test_overwrite_strategy_with_archiving_moves_old_file_to_archive_dir(
        self, tmp_path: Path
    ) -> None:
        compiled = tmp_path / "compiled.txt"
        compiled.write_text("new")
        destination = tmp_path / "output.txt"
        destination.write_text("old")

        result = publish_output(
            compiled,
            OutputSettings(path=str(destination), conflict_strategy="overwrite"),
            ArchivingSettings(enabled=True, retention_days=90),
        )

        assert result.success
        assert result.archived_path is not None
        archived = Path(result.archived_path)
        assert archived.parent.name == "archive"
        assert archived.read_text() == "old"
        assert destination.read_text() == "new"

    def test_prunes_archive_entries_older_than_retention_days(self, tmp_path: Path) -> None:
        import os
        import time

        compiled = tmp_path / "compiled.txt"
        compiled.write_text("new")
        destination = tmp_path / "output.txt"
        destination.write_text("old")

        archive_dir = tmp_path / "archive"
        archive_dir.mkdir()
        stale_entry = archive_dir / "output-20200101T000000000Z.txt"
        stale_entry.write_text("ancient")
        old_time = time.time() - (200 * 86400)
        os.utime(stale_entry, (old_time, old_time))

        publish_output(
            compiled,
            OutputSettings(path=str(destination), conflict_strategy="overwrite"),
            ArchivingSettings(enabled=True, retention_days=90),
        )

        assert not stale_entry.exists()
