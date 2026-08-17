"""Tests for the CLI module."""

import json
from pathlib import Path

import pytest

from bloqr_compiler.cli import create_parser, main


class TestParser:
    """Tests for argument parser."""

    def test_parse_config_short(self) -> None:
        parser = create_parser()
        args = parser.parse_args(["-c", "config.yaml"])
        assert args.config == "config.yaml"

    def test_parse_config_long(self) -> None:
        parser = create_parser()
        args = parser.parse_args(["--config", "config.json"])
        assert args.config == "config.json"

    def test_parse_positional_config(self) -> None:
        parser = create_parser()
        args = parser.parse_args(["config.yaml"])
        assert args.config_path == "config.yaml"

    def test_parse_output(self) -> None:
        parser = create_parser()
        args = parser.parse_args(["-o", "output.txt"])
        assert args.output == "output.txt"

    def test_parse_copy_to_rules(self) -> None:
        parser = create_parser()
        args = parser.parse_args(["-r"])
        assert args.copy_to_rules is True

    def test_parse_rules_dir(self) -> None:
        parser = create_parser()
        args = parser.parse_args(["--rules-dir", "/path/to/rules"])
        assert args.rules_dir == "/path/to/rules"

    def test_parse_format(self) -> None:
        parser = create_parser()
        args = parser.parse_args(["-f", "yaml"])
        assert args.format == "yaml"

    def test_parse_version(self) -> None:
        parser = create_parser()
        args = parser.parse_args(["-v"])
        assert args.version is True

    def test_parse_version_info_alias(self) -> None:
        parser = create_parser()
        args = parser.parse_args(["-V"])
        assert args.version_info is True

    def test_parse_debug(self) -> None:
        parser = create_parser()
        args = parser.parse_args(["-d"])
        assert args.debug is True

    def test_parse_show_config(self) -> None:
        parser = create_parser()
        args = parser.parse_args(["--show-config"])
        assert args.show_config is True

    def test_parse_validate(self) -> None:
        parser = create_parser()
        args = parser.parse_args(["--validate"])
        assert args.validate is True

    def test_parse_check_files(self) -> None:
        parser = create_parser()
        args = parser.parse_args(["--check-files"])
        assert args.check_files is True

    def test_parse_transformations(self) -> None:
        parser = create_parser()
        args = parser.parse_args(["--transformations"])
        assert args.transformations is True

    def test_parse_multiple_args(self) -> None:
        parser = create_parser()
        args = parser.parse_args(["-c", "config.yaml", "-o", "out.txt", "-r", "-d"])

        assert args.config == "config.yaml"
        assert args.output == "out.txt"
        assert args.copy_to_rules is True
        assert args.debug is True

    def test_parse_all_new_options(self) -> None:
        parser = create_parser()
        args = parser.parse_args([
            "config.yaml",
            "--rules-dir", "/rules",
            "--validate",
            "--check-files",
        ])

        assert args.config_path == "config.yaml"
        assert args.rules_dir == "/rules"
        assert args.validate is True
        assert args.check_files is True


class TestMain:
    """Tests for main function."""

    def test_version_returns_zero(self) -> None:
        result = main(["--version"])
        assert result == 0

    def test_version_info_alias_returns_zero(self) -> None:
        result = main(["-V"])
        assert result == 0

    def test_transformations_returns_zero(self) -> None:
        result = main(["--transformations"])
        assert result == 0

    def test_missing_config_returns_one(self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
        # Change to temp directory with no config
        monkeypatch.chdir(tmp_path)
        result = main([])
        assert result == 1

    def test_show_config_returns_zero(self, tmp_path: Path) -> None:
        config_data = {
            "name": "Test Filter",
            "version": "1.0.0",
            "sources": [{"source": "https://example.com/rules.txt"}],
        }
        config_file = tmp_path / "config.json"
        config_file.write_text(json.dumps(config_data))

        result = main(["--show-config", "-c", str(config_file)])
        assert result == 0

    def test_show_config_with_positional_arg(self, tmp_path: Path) -> None:
        config_data = {
            "name": "Test Filter",
            "version": "1.0.0",
            "sources": [{"source": "https://example.com/rules.txt"}],
        }
        config_file = tmp_path / "config.json"
        config_file.write_text(json.dumps(config_data))

        result = main([str(config_file), "--show-config"])
        assert result == 0

    def test_validate_valid_config(self, tmp_path: Path) -> None:
        config_data = {
            "name": "Valid Filter",
            "version": "1.0.0",
            "sources": [{"source": "https://example.com/rules.txt"}],
        }
        config_file = tmp_path / "config.json"
        config_file.write_text(json.dumps(config_data))

        result = main(["--validate", "-c", str(config_file)])
        assert result == 0

    def test_validate_invalid_config(self, tmp_path: Path) -> None:
        config_data = {
            "name": "",  # Missing name
            "sources": [],  # No sources
        }
        config_file = tmp_path / "config.json"
        config_file.write_text(json.dumps(config_data))

        result = main(["--validate", "-c", str(config_file)])
        assert result == 1

    def test_nonexistent_config_returns_one(self, tmp_path: Path) -> None:
        result = main(["-c", str(tmp_path / "nonexistent.json")])
        assert result == 1


class TestDefaultConfigSearch:
    """Tests for default configuration file search."""

    def test_finds_json_config(self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
        config_data = {
            "name": "Default JSON",
            "sources": [{"source": "https://example.com/rules.txt"}],
        }
        config_file = tmp_path / "compiler-config.json"
        config_file.write_text(json.dumps(config_data))

        monkeypatch.chdir(tmp_path)
        result = main(["--show-config"])
        assert result == 0

    def test_finds_yaml_config(self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
        yaml_content = """
name: Default YAML
sources:
  - source: https://example.com/rules.txt
"""
        config_file = tmp_path / "compiler-config.yaml"
        config_file.write_text(yaml_content)

        monkeypatch.chdir(tmp_path)
        result = main(["--show-config"])
        assert result == 0
