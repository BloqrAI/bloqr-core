"""
Applies a compiler config's `output` conflict strategy and `archiving` policy to a freshly
compiled file, publishing it to its durable, configured destination.

See docs/HASH_VERIFICATION.md's "Output conflict strategy and archiving" section - this
mirrors Bloqr.Compiler.Core's OutputPublisher (.NET) exactly, so the same config produces
the same on-disk result regardless of which compiler ran it.
"""

from __future__ import annotations

import shutil
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

from rules_compiler.config import ArchivingSettings, OutputSettings

_ARCHIVE_DIRECTORY_NAME = "archive"


@dataclass
class OutputPublishResult:
    """Result of publishing a compiled file to its configured destination."""

    success: bool = True
    final_path: str | None = None
    archived_path: str | None = None
    error_message: str | None = None


def publish_output(
    compiled_file_path: str | Path,
    output: OutputSettings,
    archiving: ArchivingSettings | None,
) -> OutputPublishResult:
    """
    Publish a compiled file to `output.path`, applying its conflict strategy and,
    for "overwrite", the archiving policy first.
    """
    compiled_path = Path(compiled_file_path)

    if not output.path:
        return OutputPublishResult(success=True, final_path=str(compiled_path))

    destination_path = Path(output.path).resolve()
    destination_path.parent.mkdir(parents=True, exist_ok=True)

    if not destination_path.exists():
        shutil.copy2(compiled_path, destination_path)
        return OutputPublishResult(success=True, final_path=str(destination_path))

    strategy = output.conflict_strategy.lower()

    if strategy == "error":
        return OutputPublishResult(
            success=False,
            error_message=(
                f"Output file already exists at {destination_path} and "
                "conflictStrategy is 'error'."
            ),
        )

    if strategy == "overwrite":
        archived_path = None
        if archiving is not None and archiving.enabled:
            archived_path = _archive(destination_path, archiving)

        shutil.copy2(compiled_path, destination_path)
        return OutputPublishResult(
            success=True, final_path=str(destination_path), archived_path=archived_path
        )

    # "rename" and any unrecognized value: never touch the existing file.
    renamed_path = _next_available_path(destination_path)
    shutil.copy2(compiled_path, renamed_path)
    return OutputPublishResult(success=True, final_path=str(renamed_path))


def _next_available_path(destination_path: Path) -> Path:
    directory = destination_path.parent
    base_name = destination_path.stem
    extension = destination_path.suffix

    i = 1
    while True:
        candidate = directory / f"{base_name}_{i}{extension}"
        if not candidate.exists():
            return candidate
        i += 1


def _archive(destination_path: Path, archiving: ArchivingSettings) -> str:
    directory = destination_path.parent
    base_name = destination_path.stem
    extension = destination_path.suffix

    archive_directory = directory / _ARCHIVE_DIRECTORY_NAME
    archive_directory.mkdir(parents=True, exist_ok=True)

    timestamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%f")[:-3] + "Z"
    archived_path = archive_directory / f"{base_name}-{timestamp}{extension}"

    shutil.move(str(destination_path), str(archived_path))

    _prune_archive(archive_directory, base_name, extension, archiving.retention_days)

    return str(archived_path)


def _prune_archive(
    archive_directory: Path, base_name: str, extension: str, retention_days: int
) -> None:
    if retention_days <= 0:
        return

    cutoff = time.time() - (retention_days * 86400)
    for file_path in archive_directory.glob(f"{base_name}-*{extension}"):
        try:
            if file_path.stat().st_mtime >= cutoff:
                continue
            file_path.unlink()
        except OSError:
            pass
