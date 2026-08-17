"""
Configuration reader with multi-format support (JSON, YAML, TOML).
"""

from __future__ import annotations

import json
import re
import sys
from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path
from typing import Any

from bloqr_compiler.errors import (
    ParseError,
    UnknownExtensionError,
    ValidationResult,
)


class ConfigurationFormat(Enum):
    """Supported configuration file formats."""
    JSON = "json"
    YAML = "yaml"
    TOML = "toml"


class SourceType(Enum):
    """Supported filter source types."""
    ADBLOCK = "adblock"
    HOSTS = "hosts"

    @classmethod
    def from_string(cls, value: str) -> SourceType:
        """Parse source type from string (case-insensitive)."""
        normalized = value.lower().strip()
        if normalized in ("adblock", "adb"):
            return cls.ADBLOCK
        if normalized in ("hosts", "host"):
            return cls.HOSTS
        raise ValueError(f"Unknown source type: {value}. Valid types: adblock, hosts")


class Transformation(Enum):
    """Available transformations for filter rules.

    These transformations are applied during compilation to modify
    and optimize the filter rules.
    """
    REMOVE_COMMENTS = "RemoveComments"
    COMPRESS = "Compress"
    REMOVE_MODIFIERS = "RemoveModifiers"
    VALIDATE = "Validate"
    VALIDATE_ALLOW_IP = "ValidateAllowIp"
    DEDUPLICATE = "Deduplicate"
    INVERT_ALLOW = "InvertAllow"
    REMOVE_EMPTY_LINES = "RemoveEmptyLines"
    TRIM_LINES = "TrimLines"
    INSERT_FINAL_NEW_LINE = "InsertFinalNewLine"
    CONVERT_TO_ASCII = "ConvertToAscii"

    @classmethod
    def from_string(cls, value: str) -> Transformation:
        """Parse transformation from string (case-insensitive)."""
        # Map various casings to enum values
        mapping = {t.value.lower(): t for t in cls}
        normalized = value.lower().strip()
        if normalized in mapping:
            return mapping[normalized]
        raise ValueError(
            f"Unknown transformation: {value}. Valid transformations: "
            f"{', '.join(t.value for t in cls)}"
        )

    @classmethod
    def all_names(cls) -> list[str]:
        """Get list of all transformation names."""
        return [t.value for t in cls]

    @classmethod
    def is_valid(cls, value: str) -> bool:
        """Check if a transformation name is valid."""
        try:
            cls.from_string(value)
            return True
        except ValueError:
            return False

    @classmethod
    def get_invalid(cls, values: list[str]) -> list[str]:
        """Get list of invalid transformation names."""
        return [v for v in values if not cls.is_valid(v)]

    @classmethod
    def recommended(cls) -> list[Transformation]:
        """Get recommended transformations for typical compilation."""
        return [
            cls.VALIDATE,
            cls.DEDUPLICATE,
            cls.REMOVE_EMPTY_LINES,
            cls.TRIM_LINES,
            cls.INSERT_FINAL_NEW_LINE,
        ]

    @classmethod
    def minimal(cls) -> list[Transformation]:
        """Get minimal transformations that preserve original content."""
        return [cls.DEDUPLICATE, cls.INSERT_FINAL_NEW_LINE]

    @classmethod
    def hosts_file(cls) -> list[Transformation]:
        """Get transformations optimized for hosts file sources."""
        return [
            cls.COMPRESS,
            cls.VALIDATE,
            cls.DEDUPLICATE,
            cls.REMOVE_EMPTY_LINES,
            cls.TRIM_LINES,
            cls.INSERT_FINAL_NEW_LINE,
        ]


@dataclass
class FilterSource:
    """Represents a source filter list to compile."""
    name: str = ""
    source: str = ""
    type: str = "adblock"
    transformations: list[str] = field(default_factory=list)
    inclusions: list[str] = field(default_factory=list)
    exclusions: list[str] = field(default_factory=list)

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> FilterSource:
        """Create a FilterSource from a dictionary."""
        return cls(
            name=data.get("name", ""),
            source=data.get("source", ""),
            type=data.get("type", "adblock"),
            transformations=data.get("transformations", []),
            inclusions=data.get("inclusions", []),
            exclusions=data.get("exclusions", []),
        )

    def to_dict(self) -> dict[str, Any]:
        """Convert to dictionary for serialization."""
        result: dict[str, Any] = {"source": self.source}
        if self.name:
            result["name"] = self.name
        if self.type != "adblock":
            result["type"] = self.type
        if self.transformations:
            result["transformations"] = self.transformations
        if self.inclusions:
            result["inclusions"] = self.inclusions
        if self.exclusions:
            result["exclusions"] = self.exclusions
        return result

    def is_url(self) -> bool:
        """Check if source is a URL."""
        return self.source.startswith(("http://", "https://"))

    def is_local(self) -> bool:
        """Check if source is a local file path."""
        return not self.is_url()

    def get_source_type(self) -> SourceType:
        """Get the source type as an enum."""
        return SourceType.from_string(self.type)


@dataclass
class OutputSettings:
    """Output file settings: destination path and conflict-handling strategy."""
    path: str = ""
    conflict_strategy: str = "rename"

    VALID_CONFLICT_STRATEGIES = ("rename", "overwrite", "error")

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> OutputSettings:
        return cls(
            path=data.get("path", ""),
            conflict_strategy=data.get("conflictStrategy", "rename"),
        )

    def to_dict(self) -> dict[str, Any]:
        result: dict[str, Any] = {"path": self.path}
        if self.conflict_strategy != "rename":
            result["conflictStrategy"] = self.conflict_strategy
        return result


@dataclass
class HashVerificationSettings:
    """Hash-verification settings for the `.hashes.json` sidecar database."""
    mode: str = "warning"
    require_hashes_for_remote: bool = False
    fail_on_mismatch: bool = False
    hash_database_path: str = ""

    VALID_MODES = ("strict", "warning", "disabled")

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> HashVerificationSettings:
        return cls(
            mode=data.get("mode", "warning"),
            require_hashes_for_remote=data.get("requireHashesForRemote", False),
            fail_on_mismatch=data.get("failOnMismatch", False),
            hash_database_path=data.get("hashDatabasePath", ""),
        )

    def to_dict(self) -> dict[str, Any]:
        result: dict[str, Any] = {}
        if self.mode != "warning":
            result["mode"] = self.mode
        if self.require_hashes_for_remote:
            result["requireHashesForRemote"] = self.require_hashes_for_remote
        if self.fail_on_mismatch:
            result["failOnMismatch"] = self.fail_on_mismatch
        if self.hash_database_path:
            result["hashDatabasePath"] = self.hash_database_path
        return result


@dataclass
class ArchivingSettings:
    """Archiving settings applied when the output conflict strategy overwrites a file."""
    enabled: bool = False
    mode: str = "automatic"
    retention_days: int = 90

    VALID_MODES = ("automatic", "interactive", "disabled")

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> ArchivingSettings:
        return cls(
            enabled=data.get("enabled", False),
            mode=data.get("mode", "automatic"),
            retention_days=data.get("retentionDays", 90),
        )

    def to_dict(self) -> dict[str, Any]:
        result: dict[str, Any] = {}
        if self.enabled:
            result["enabled"] = self.enabled
        if self.mode != "automatic":
            result["mode"] = self.mode
        if self.retention_days != 90:
            result["retentionDays"] = self.retention_days
        return result


@dataclass
class CompilerConfiguration:
    """Configuration for the hostlist-compiler."""
    name: str = ""
    description: str = ""
    homepage: str = ""
    license: str = ""
    version: str = ""
    sources: list[FilterSource] = field(default_factory=list)
    transformations: list[str] = field(default_factory=list)
    inclusions: list[str] = field(default_factory=list)
    exclusions: list[str] = field(default_factory=list)
    output: OutputSettings | None = None
    hash_verification: HashVerificationSettings | None = None
    archiving: ArchivingSettings | None = None

    # Metadata (not serialized)
    _source_format: ConfigurationFormat | None = field(default=None, repr=False)
    _source_path: str | None = field(default=None, repr=False)

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> CompilerConfiguration:
        """Create a CompilerConfiguration from a dictionary."""
        sources = [FilterSource.from_dict(s) for s in data.get("sources", [])]
        return cls(
            name=data.get("name", ""),
            description=data.get("description", ""),
            homepage=data.get("homepage", ""),
            license=data.get("license", ""),
            version=data.get("version", ""),
            sources=sources,
            transformations=data.get("transformations", []),
            inclusions=data.get("inclusions", []),
            exclusions=data.get("exclusions", []),
            output=OutputSettings.from_dict(data["output"]) if "output" in data else None,
            hash_verification=HashVerificationSettings.from_dict(data["hashVerification"])
            if "hashVerification" in data else None,
            archiving=ArchivingSettings.from_dict(data["archiving"]) if "archiving" in data else None,
        )

    def to_dict(self) -> dict[str, Any]:
        """Convert to a dictionary for JSON serialization."""
        result: dict[str, Any] = {"name": self.name}

        if self.description:
            result["description"] = self.description
        if self.homepage:
            result["homepage"] = self.homepage
        if self.license:
            result["license"] = self.license
        if self.version:
            result["version"] = self.version
        if self.output is not None:
            result["output"] = self.output.to_dict()
        if self.hash_verification is not None:
            result["hashVerification"] = self.hash_verification.to_dict()
        if self.archiving is not None:
            result["archiving"] = self.archiving.to_dict()

        result["sources"] = [s.to_dict() for s in self.sources]

        if self.transformations:
            result["transformations"] = self.transformations
        if self.inclusions:
            result["inclusions"] = self.inclusions
        if self.exclusions:
            result["exclusions"] = self.exclusions

        return result

    def validate(self, check_files: bool = False) -> ValidationResult:
        """
        Validate the configuration.

        Args:
            check_files: If True, check that local source files exist.

        Returns:
            ValidationResult with errors and warnings.
        """
        result = ValidationResult()

        # Check required fields
        if not self.name or not self.name.strip():
            result.add_error("Configuration 'name' is required")

        if not self.sources:
            result.add_error("At least one source is required")

        if self.output is not None and self.output.conflict_strategy not in OutputSettings.VALID_CONFLICT_STRATEGIES:
            result.add_error(
                f"Invalid conflict strategy '{self.output.conflict_strategy}'. "
                f"Valid strategies are: {', '.join(OutputSettings.VALID_CONFLICT_STRATEGIES)}"
            )

        if self.hash_verification is not None:
            if self.hash_verification.mode not in HashVerificationSettings.VALID_MODES:
                result.add_error(
                    f"Invalid hash verification mode '{self.hash_verification.mode}'. "
                    f"Valid modes are: {', '.join(HashVerificationSettings.VALID_MODES)}"
                )
            if (
                self.hash_verification.mode != "disabled"
                and not self.hash_verification.hash_database_path
            ):
                result.add_warning(
                    "Hash verification is enabled but no hashDatabasePath was specified; "
                    "hashes cannot be recorded or checked."
                )

        if self.archiving is not None:
            if self.archiving.mode not in ArchivingSettings.VALID_MODES:
                result.add_error(
                    f"Invalid archiving mode '{self.archiving.mode}'. "
                    f"Valid modes are: {', '.join(ArchivingSettings.VALID_MODES)}"
                )
            if self.archiving.retention_days < 1:
                result.add_error("Retention days must be at least 1")

        # Validate sources
        for i, source in enumerate(self.sources):
            source_id = source.name or f"sources[{i}]"

            if not source.source or not source.source.strip():
                result.add_error(f"Source '{source_id}' is missing 'source' field")

            # Validate source type
            try:
                SourceType.from_string(source.type)
            except ValueError:
                result.add_error(
                    f"Source '{source_id}' has invalid type '{source.type}'. "
                    f"Valid types: adblock, hosts"
                )

            # Validate source-specific transformations
            invalid_transforms = Transformation.get_invalid(source.transformations)
            if invalid_transforms:
                result.add_warning(
                    f"Source '{source_id}' has invalid transformations: "
                    f"{', '.join(invalid_transforms)}"
                )

            # Check local files if requested
            if check_files and source.is_local():
                source_path = Path(source.source)
                if self._source_path:
                    # Resolve relative to config file
                    config_dir = Path(self._source_path).parent
                    source_path = config_dir / source.source
                if not source_path.exists():
                    result.add_warning(
                        f"Source file for '{source_id}' not found: {source_path}"
                    )

            # Validate regex patterns in inclusions/exclusions
            for pattern in source.inclusions + source.exclusions:
                if pattern.startswith("/") and pattern.endswith("/"):
                    try:
                        re.compile(pattern[1:-1])
                    except re.error as e:
                        result.add_warning(
                            f"Source '{source_id}' has invalid regex pattern "
                            f"'{pattern}': {e}"
                        )

        # Validate global transformations
        invalid_transforms = Transformation.get_invalid(self.transformations)
        if invalid_transforms:
            result.add_warning(
                f"Invalid global transformations: {', '.join(invalid_transforms)}"
            )

        # Validate global regex patterns
        for pattern in self.inclusions + self.exclusions:
            if pattern.startswith("/") and pattern.endswith("/"):
                try:
                    re.compile(pattern[1:-1])
                except re.error as e:
                    result.add_warning(f"Invalid global regex pattern '{pattern}': {e}")

        return result

    def local_sources_count(self) -> int:
        """Count the number of local file sources."""
        return sum(1 for s in self.sources if s.is_local())

    def remote_sources_count(self) -> int:
        """Count the number of remote URL sources."""
        return sum(1 for s in self.sources if s.is_url())

    def get_transformations(self) -> list[Transformation]:
        """Get transformations as enum values (valid ones only)."""
        result = []
        for t in self.transformations:
            try:
                result.append(Transformation.from_string(t))
            except ValueError:
                pass  # Skip invalid transformations
        return result


def detect_format(file_path: str | Path) -> ConfigurationFormat:
    """
    Detect configuration format from file extension.

    Args:
        file_path: Path to the configuration file.

    Returns:
        Detected configuration format.

    Raises:
        UnknownExtensionError: If the extension is not recognized.
    """
    path = Path(file_path)
    ext = path.suffix.lower()

    format_map = {
        ".json": ConfigurationFormat.JSON,
        ".yaml": ConfigurationFormat.YAML,
        ".yml": ConfigurationFormat.YAML,
        ".toml": ConfigurationFormat.TOML,
    }

    if ext not in format_map:
        raise UnknownExtensionError(ext, list(format_map.keys()))

    return format_map[ext]


def _parse_json(content: str, path: str | None = None) -> dict[str, Any]:
    """Parse JSON content."""
    try:
        return json.loads(content)
    except json.JSONDecodeError as e:
        raise ParseError(
            f"Invalid JSON: {e.msg}",
            format="json",
            path=path,
            line=e.lineno,
            column=e.colno,
        ) from e


def _parse_yaml(content: str, path: str | None = None) -> dict[str, Any]:
    """Parse YAML content."""
    try:
        import yaml
        data = yaml.safe_load(content)
        if not isinstance(data, dict):
            raise ParseError(
                "Invalid YAML: root must be an object",
                format="yaml",
                path=path,
            )
        return data
    except ImportError:
        raise ImportError(
            "PyYAML is required for YAML support. Install with: pip install pyyaml"
        )
    except yaml.YAMLError as e:
        line = getattr(e, "problem_mark", None)
        raise ParseError(
            f"Invalid YAML: {e}",
            format="yaml",
            path=path,
            line=line.line + 1 if line else None,
            column=line.column + 1 if line else None,
        ) from e


def _parse_toml(content: str, path: str | None = None) -> dict[str, Any]:
    """Parse TOML content."""
    try:
        # Python 3.11+ has tomllib in stdlib
        if sys.version_info >= (3, 11):
            import tomllib
            return tomllib.loads(content)
        else:
            import tomli
            return tomli.loads(content)
    except ImportError:
        raise ImportError(
            "TOML support requires Python 3.11+ or tomli package. "
            "Install with: pip install tomli"
        )
    except Exception as e:
        raise ParseError(
            f"Invalid TOML: {e}",
            format="toml",
            path=path,
        ) from e


def read_configuration(
    config_path: str | Path,
    format: ConfigurationFormat | None = None,
) -> CompilerConfiguration:
    """
    Read and parse configuration from a file.

    Args:
        config_path: Path to the configuration file.
        format: Optional format override. If None, detected from extension.

    Returns:
        Parsed compiler configuration.

    Raises:
        FileNotFoundError: If the file doesn't exist.
        ParseError: If parsing fails.
        UnknownExtensionError: If extension is not recognized.
    """
    path = Path(config_path)

    if not path.exists():
        raise FileNotFoundError(f"Configuration file not found: {config_path}")

    detected_format = format or detect_format(path)
    content = path.read_text(encoding="utf-8")

    parsers = {
        ConfigurationFormat.JSON: _parse_json,
        ConfigurationFormat.YAML: _parse_yaml,
        ConfigurationFormat.TOML: _parse_toml,
    }

    data = parsers[detected_format](content, str(path))
    config = CompilerConfiguration.from_dict(data)
    config._source_format = detected_format
    config._source_path = str(path)

    return config


def to_json(config: CompilerConfiguration, indent: int = 2) -> str:
    """
    Convert configuration to JSON string.

    Args:
        config: The configuration to convert.
        indent: JSON indentation level.

    Returns:
        JSON string representation.
    """
    return json.dumps(config.to_dict(), indent=indent)


def to_yaml(config: CompilerConfiguration) -> str:
    """
    Convert configuration to YAML string.

    Args:
        config: The configuration to convert.

    Returns:
        YAML string representation.

    Raises:
        ImportError: If PyYAML is not installed.
    """
    try:
        import yaml
        return yaml.dump(
            config.to_dict(),
            default_flow_style=False,
            sort_keys=False,
            allow_unicode=True,
        )
    except ImportError:
        raise ImportError(
            "PyYAML is required for YAML export. Install with: pip install pyyaml"
        )


def to_toml(config: CompilerConfiguration) -> str:
    """
    Convert configuration to TOML string.

    Args:
        config: The configuration to convert.

    Returns:
        TOML string representation.

    Raises:
        ImportError: If tomlkit is not installed.
    """
    try:
        import tomlkit
        doc = tomlkit.document()
        data = config.to_dict()

        # Add top-level fields
        for key in ["name", "description", "homepage", "license", "version"]:
            if key in data and data[key]:
                doc.add(key, data[key])

        # Add sources as array of tables
        if data.get("sources"):
            sources_array = tomlkit.aot()
            for source in data["sources"]:
                source_table = tomlkit.table()
                for k, v in source.items():
                    if v:  # Skip empty values
                        source_table.add(k, v)
                sources_array.append(source_table)
            doc.add("sources", sources_array)

        # Add optional arrays
        for key in ["transformations", "inclusions", "exclusions"]:
            if key in data and data[key]:
                doc.add(key, data[key])

        return tomlkit.dumps(doc)
    except ImportError:
        raise ImportError(
            "tomlkit is required for TOML export. Install with: pip install tomlkit"
        )
