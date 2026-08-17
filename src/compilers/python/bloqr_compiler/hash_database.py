"""
The `.hashes.json` sidecar database: the primary trust mechanism for hash verification.

See docs/HASH_VERIFICATION.md. A hash embedded in the output file itself would have to be
recomputed on every manual edit to stay meaningful; this sidecar instead records what each
watched item looked like the last time the compiler verified it, keyed by absolute path.
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


@dataclass
class HashDatabaseEntry:
    """A single recorded hash for an item in the `.hashes.json` sidecar."""

    hash: str
    size_bytes: int
    computed_at: datetime = field(default_factory=lambda: datetime.now(timezone.utc))
    item_type: str = ""

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> HashDatabaseEntry:
        computed_at_raw = data.get("computedAt")
        computed_at = (
            datetime.fromisoformat(computed_at_raw.replace("Z", "+00:00"))
            if computed_at_raw
            else datetime.now(timezone.utc)
        )
        return cls(
            hash=data.get("hash", ""),
            size_bytes=data.get("sizeBytes", 0),
            computed_at=computed_at,
            item_type=data.get("itemType", ""),
        )

    def to_dict(self) -> dict[str, Any]:
        return {
            "hash": self.hash,
            "sizeBytes": self.size_bytes,
            "computedAt": self.computed_at.isoformat().replace("+00:00", "Z"),
            "itemType": self.item_type,
        }


def load_hash_database(database_path: str | Path) -> dict[str, HashDatabaseEntry]:
    """Load the sidecar database, returning an empty dict if it doesn't exist yet."""
    path = Path(database_path)
    if not path.exists():
        return {}

    with open(path, encoding="utf-8") as f:
        raw = json.load(f)

    return {key: HashDatabaseEntry.from_dict(value) for key, value in raw.items()}


def record_hash(
    database_path: str | Path,
    item_identifier: str,
    entry: HashDatabaseEntry,
) -> None:
    """Record (or overwrite) one item's hash entry in the sidecar database."""
    path = Path(database_path)
    entries = load_hash_database(path)
    entries[item_identifier] = entry

    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(
            {key: value.to_dict() for key, value in entries.items()},
            f,
            indent=2,
        )
