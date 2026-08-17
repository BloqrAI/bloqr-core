"""Tests for the configuration reader module."""

import json
from pathlib import Path

import pytest

from bloqr_compiler.config import (
    CompilerConfiguration,
    ConfigurationFormat,
    FilterSource,
    SourceType,
    Transformation,
    detect_format,
    read_configuration,
    to_json,
    to_yaml,
)
from bloqr_compiler.errors import ParseError, UnknownExtensionError


class TestDetectFormat:
    """Tests for detect_format function."""

    def test_detect_json(self) -> None:
        assert detect_format("config.json") == ConfigurationFormat.JSON
        assert detect_format("/path/to/config.JSON") == ConfigurationFormat.JSON

    def test_detect_yaml(self) -> None:
        assert detect_format("config.yaml") == ConfigurationFormat.YAML
        assert detect_format("config.yml") == ConfigurationFormat.YAML
        assert detect_format("/path/to/config.YAML") == ConfigurationFormat.YAML

    def test_detect_toml(self) -> None:
        assert detect_format("config.toml") == ConfigurationFormat.TOML
        assert detect_format("/path/to/config.TOML") == ConfigurationFormat.TOML

    def test_unknown_extension(self) -> None:
        with pytest.raises(UnknownExtensionError):
            detect_format("config.txt")

        with pytest.raises(UnknownExtensionError):
            detect_format("config.xml")


class TestReadConfiguration:
    """Tests for read_configuration function."""

    def test_read_json_config(self, tmp_path: Path) -> None:
        config_data = {
            "name": "Test Filter",
            "version": "1.0.0",
            "sources": [
                {"name": "Local", "source": "./rules.txt", "type": "adblock"}
            ],
            "transformations": ["Deduplicate"],
        }
        config_file = tmp_path / "config.json"
        config_file.write_text(json.dumps(config_data))

        config = read_configuration(config_file)

        assert config.name == "Test Filter"
        assert config.version == "1.0.0"
        assert len(config.sources) == 1
        assert config.sources[0].name == "Local"
        assert config._source_format == ConfigurationFormat.JSON

    def test_read_yaml_config(self, tmp_path: Path) -> None:
        yaml_content = """
name: YAML Test Filter
version: 2.0.0
sources:
  - name: Remote
    source: https://example.com/rules.txt
    type: adblock
transformations:
  - Validate
"""
        config_file = tmp_path / "config.yaml"
        config_file.write_text(yaml_content)

        config = read_configuration(config_file)

        assert config.name == "YAML Test Filter"
        assert config.version == "2.0.0"
        assert len(config.sources) == 1
        assert config._source_format == ConfigurationFormat.YAML

    def test_file_not_found(self) -> None:
        with pytest.raises(FileNotFoundError, match="Configuration file not found"):
            read_configuration("/nonexistent/path/config.json")

    def test_invalid_json(self, tmp_path: Path) -> None:
        config_file = tmp_path / "config.json"
        config_file.write_text("not valid json {{{")

        with pytest.raises(ParseError):
            read_configuration(config_file)

    def test_format_override(self, tmp_path: Path) -> None:
        yaml_content = """
name: Override Test
version: 1.0.0
sources: []
transformations: []
"""
        # Write YAML content with .txt extension
        config_file = tmp_path / "config.txt"
        config_file.write_text(yaml_content)

        # Force YAML format
        config = read_configuration(config_file, format=ConfigurationFormat.YAML)

        assert config.name == "Override Test"
        assert config._source_format == ConfigurationFormat.YAML


class TestSourceType:
    """Tests for SourceType enum."""

    def test_from_string_adblock(self) -> None:
        assert SourceType.from_string("adblock") == SourceType.ADBLOCK
        assert SourceType.from_string("ADBLOCK") == SourceType.ADBLOCK
        assert SourceType.from_string("adb") == SourceType.ADBLOCK

    def test_from_string_hosts(self) -> None:
        assert SourceType.from_string("hosts") == SourceType.HOSTS
        assert SourceType.from_string("HOSTS") == SourceType.HOSTS
        assert SourceType.from_string("host") == SourceType.HOSTS

    def test_from_string_invalid(self) -> None:
        with pytest.raises(ValueError, match="Unknown source type"):
            SourceType.from_string("invalid")


class TestTransformation:
    """Tests for Transformation enum."""

    def test_all_transformations_exist(self) -> None:
        expected = [
            "RemoveComments", "Compress", "RemoveModifiers", "Validate",
            "ValidateAllowIp", "Deduplicate", "InvertAllow", "RemoveEmptyLines",
            "TrimLines", "InsertFinalNewLine", "ConvertToAscii"
        ]
        assert Transformation.all_names() == expected

    def test_from_string(self) -> None:
        assert Transformation.from_string("Deduplicate") == Transformation.DEDUPLICATE
        assert Transformation.from_string("deduplicate") == Transformation.DEDUPLICATE
        assert Transformation.from_string("DEDUPLICATE") == Transformation.DEDUPLICATE

    def test_from_string_invalid(self) -> None:
        with pytest.raises(ValueError, match="Unknown transformation"):
            Transformation.from_string("Invalid")

    def test_is_valid(self) -> None:
        assert Transformation.is_valid("Deduplicate") is True
        assert Transformation.is_valid("Invalid") is False

    def test_get_invalid(self) -> None:
        values = ["Deduplicate", "Invalid", "Validate", "BadOne"]
        invalid = Transformation.get_invalid(values)
        assert invalid == ["Invalid", "BadOne"]

    def test_recommended(self) -> None:
        rec = Transformation.recommended()
        assert Transformation.VALIDATE in rec
        assert Transformation.DEDUPLICATE in rec
        assert Transformation.REMOVE_EMPTY_LINES in rec

    def test_minimal(self) -> None:
        min_set = Transformation.minimal()
        assert len(min_set) == 2
        assert Transformation.DEDUPLICATE in min_set
        assert Transformation.INSERT_FINAL_NEW_LINE in min_set

    def test_hosts_file(self) -> None:
        hosts = Transformation.hosts_file()
        assert Transformation.COMPRESS in hosts
        assert Transformation.VALIDATE in hosts


class TestFilterSource:
    """Tests for FilterSource class."""

    def test_from_dict(self) -> None:
        data = {
            "name": "Test Source",
            "source": "./rules.txt",
            "type": "hosts",
            "inclusions": ["*"],
            "exclusions": ["excluded"],
        }

        source = FilterSource.from_dict(data)

        assert source.name == "Test Source"
        assert source.source == "./rules.txt"
        assert source.type == "hosts"
        assert source.inclusions == ["*"]
        assert source.exclusions == ["excluded"]

    def test_from_dict_defaults(self) -> None:
        source = FilterSource.from_dict({})

        assert source.name == ""
        assert source.source == ""
        assert source.type == "adblock"
        assert source.inclusions == []
        assert source.exclusions == []

    def test_from_dict_with_transformations(self) -> None:
        data = {
            "name": "Test",
            "source": "https://example.com/list.txt",
            "transformations": ["Compress", "Validate"],
        }
        source = FilterSource.from_dict(data)
        assert source.transformations == ["Compress", "Validate"]

    def test_is_url(self) -> None:
        url_source = FilterSource(source="https://example.com/list.txt")
        local_source = FilterSource(source="./local/rules.txt")

        assert url_source.is_url() is True
        assert local_source.is_url() is False

    def test_is_local(self) -> None:
        url_source = FilterSource(source="https://example.com/list.txt")
        local_source = FilterSource(source="./local/rules.txt")

        assert url_source.is_local() is False
        assert local_source.is_local() is True

    def test_get_source_type(self) -> None:
        source = FilterSource(source="./rules.txt", type="hosts")
        assert source.get_source_type() == SourceType.HOSTS

    def test_to_dict(self) -> None:
        source = FilterSource(
            name="Test",
            source="./rules.txt",
            type="hosts",
            transformations=["Compress"],
        )
        data = source.to_dict()

        assert data["source"] == "./rules.txt"
        assert data["name"] == "Test"
        assert data["type"] == "hosts"
        assert data["transformations"] == ["Compress"]


class TestCompilerConfiguration:
    """Tests for CompilerConfiguration class."""

    def test_from_dict(self) -> None:
        data = {
            "name": "My Filter",
            "description": "A filter list",
            "homepage": "https://example.com",
            "license": "MIT",
            "version": "1.0.0",
            "sources": [
                {"name": "Local", "source": "./rules.txt"}
            ],
            "transformations": ["Deduplicate", "Validate"],
            "inclusions": ["*"],
        }

        config = CompilerConfiguration.from_dict(data)

        assert config.name == "My Filter"
        assert config.description == "A filter list"
        assert config.version == "1.0.0"
        assert len(config.sources) == 1
        assert config.transformations == ["Deduplicate", "Validate"]

    def test_to_dict(self) -> None:
        config = CompilerConfiguration(
            name="Test",
            version="1.0.0",
            sources=[FilterSource(name="Local", source="./rules.txt")],
            transformations=["Deduplicate"],
        )

        data = config.to_dict()

        assert data["name"] == "Test"
        assert data["version"] == "1.0.0"
        assert len(data["sources"]) == 1
        assert data["sources"][0]["source"] == "./rules.txt"

    def test_validate_valid_config(self) -> None:
        config = CompilerConfiguration(
            name="Test",
            sources=[FilterSource(source="https://example.com/rules.txt")],
        )
        result = config.validate()
        assert result.is_valid is True
        assert len(result.errors) == 0

    def test_validate_missing_name(self) -> None:
        config = CompilerConfiguration(
            name="",
            sources=[FilterSource(source="./rules.txt")],
        )
        result = config.validate()
        assert result.is_valid is False
        assert any("name" in e.lower() for e in result.errors)

    def test_validate_no_sources(self) -> None:
        config = CompilerConfiguration(name="Test", sources=[])
        result = config.validate()
        assert result.is_valid is False
        assert any("source" in e.lower() for e in result.errors)

    def test_validate_invalid_transformation(self) -> None:
        config = CompilerConfiguration(
            name="Test",
            sources=[FilterSource(source="./rules.txt")],
            transformations=["Deduplicate", "InvalidTransform"],
        )
        result = config.validate()
        assert result.is_valid is True  # Invalid transformations are warnings
        assert len(result.warnings) > 0

    def test_local_sources_count(self) -> None:
        config = CompilerConfiguration(
            name="Test",
            sources=[
                FilterSource(source="./local1.txt"),
                FilterSource(source="https://example.com/remote.txt"),
                FilterSource(source="./local2.txt"),
            ],
        )
        assert config.local_sources_count() == 2

    def test_remote_sources_count(self) -> None:
        config = CompilerConfiguration(
            name="Test",
            sources=[
                FilterSource(source="./local1.txt"),
                FilterSource(source="https://example.com/remote1.txt"),
                FilterSource(source="https://example.com/remote2.txt"),
            ],
        )
        assert config.remote_sources_count() == 2

    def test_get_transformations(self) -> None:
        config = CompilerConfiguration(
            name="Test",
            sources=[],
            transformations=["Deduplicate", "Invalid", "Validate"],
        )
        transforms = config.get_transformations()
        assert len(transforms) == 2
        assert Transformation.DEDUPLICATE in transforms
        assert Transformation.VALIDATE in transforms


class TestToJson:
    """Tests for to_json function."""

    def test_converts_to_json(self) -> None:
        config = CompilerConfiguration(
            name="Test",
            version="1.0.0",
            sources=[FilterSource(name="Local", source="./rules.txt")],
        )

        json_str = to_json(config)
        parsed = json.loads(json_str)

        assert parsed["name"] == "Test"
        assert parsed["version"] == "1.0.0"
        assert "_source_format" not in parsed
        assert "_source_path" not in parsed


class TestToYaml:
    """Tests for to_yaml function."""

    def test_converts_to_yaml(self) -> None:
        config = CompilerConfiguration(
            name="Test YAML",
            version="1.0.0",
            sources=[FilterSource(name="Source", source="./rules.txt")],
        )

        yaml_str = to_yaml(config)

        assert "name: Test YAML" in yaml_str
        assert "version: '1.0.0'" in yaml_str or "version: 1.0.0" in yaml_str
        assert "sources:" in yaml_str
