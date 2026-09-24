"""Tests for JSON Schema config validation, including a drift guard between the
bundled schema copy (`bloqr_compiler/schemas/compiler-config.schema.json`) and the
repo-root canonical schema (`schemas/compiler-config.schema.json`) - see issue #518.
"""

import json
from pathlib import Path

import pytest

from bloqr_compiler.config import ConfigurationFormat, read_configuration
from bloqr_compiler.errors import ValidationError
from bloqr_compiler.schema_validation import assert_json_schema_valid_configuration

_BUNDLED_SCHEMA_PATH = (
    Path(__file__).parent.parent / "bloqr_compiler" / "schemas" / "compiler-config.schema.json"
)
_CANONICAL_SCHEMA_PATH = (
    Path(__file__).parent.parent.parent.parent.parent / "schemas" / "compiler-config.schema.json"
)


def test_bundled_schema_copy_stays_in_sync_with_canonical() -> None:
    bundled = _BUNDLED_SCHEMA_PATH.read_text(encoding="utf-8")
    canonical = _CANONICAL_SCHEMA_PATH.read_text(encoding="utf-8")
    assert bundled == canonical, (
        "bloqr_compiler/schemas/compiler-config.schema.json has drifted from the "
        "repo-root schemas/compiler-config.schema.json - copy the canonical file over "
        "the bundled one (cp schemas/compiler-config.schema.json "
        "src/compilers/python/bloqr_compiler/schemas/compiler-config.schema.json)."
    )


class TestAssertJsonSchemaValidConfiguration:
    """Tests for assert_json_schema_valid_configuration."""

    def test_accepts_minimal_valid_configuration(self) -> None:
        assert_json_schema_valid_configuration(
            {"name": "Test", "sources": [{"source": "https://example.com/list.txt"}]}
        )

    def test_accepts_the_shipped_typescript_default_config_shape(self) -> None:
        # Regression coverage: this shape (with `$schema`, `output`, `archiving`) is what
        # the TypeScript compiler ships as its own default config and must keep validating
        # here too, since it's the same canonical schema.
        assert_json_schema_valid_configuration({
            "$schema": "../../../schemas/compiler-config.schema.json",
            "name": "AdGuard User Filter",
            "version": "1.0.0",
            "description": "Custom filter rules for AdGuard DNS",
            "license": "MIT",
            "homepage": "https://github.com/BloqrAI/bloqr-lists",
            "output": {
                "path": "../bloqr-blocklists/output/adguard_user_filter.txt",
                "conflictStrategy": "rename",
            },
            "archiving": {"enabled": True, "mode": "automatic", "retentionDays": 90},
            "sources": [
                {
                    "name": "User Rules",
                    "source": "../bloqr-blocklists/input/user-rules.txt",
                    "type": "adblock",
                }
            ],
            "transformations": ["RemoveComments", "Compress", "Validate"],
        })

    def test_accepts_conflict_detection_and_rule_optimizer_transformations(self) -> None:
        # Regression coverage: these were added to the canonical schema's enum in #518
        # after being missing since #512.
        assert_json_schema_valid_configuration({
            "name": "Test",
            "sources": [{"source": "https://example.com/list.txt"}],
            "transformations": ["ConflictDetection", "RuleOptimizer"],
        })

    def test_accepts_chunking_extensions_use_browser_and_output_file_name(self) -> None:
        # Regression coverage: these are TypeScript-orchestration-layer fields the schema
        # was expanded to support in #518, and Python configs must not be broken by them
        # existing in a shared/cross-language config file.
        assert_json_schema_valid_configuration({
            "name": "Test",
            "extensions": {"customKey": "customValue"},
            "chunking": {"enabled": True, "strategy": "source", "maxParallel": 4},
            "output": {"fileName": "output.txt", "conflictStrategy": "overwrite"},
            "sources": [{"source": "https://example.com/list.txt", "useBrowser": True}],
        })

    def test_rejects_config_missing_required_fields(self) -> None:
        with pytest.raises(ValidationError):
            assert_json_schema_valid_configuration({})

    def test_rejects_invalid_transformation_enum_value(self) -> None:
        with pytest.raises(ValidationError):
            assert_json_schema_valid_configuration({
                "name": "Test",
                "sources": [{"source": "https://example.com/list.txt"}],
                "transformations": ["NotARealTransformation"],
            })

    def test_rejects_unrecognized_top_level_property(self) -> None:
        with pytest.raises(ValidationError):
            assert_json_schema_valid_configuration({
                "name": "Test",
                "sources": [{"source": "https://example.com/list.txt"}],
                "bogusField": True,
            })

    def test_rejects_non_uri_homepage(self) -> None:
        # Regression coverage: jsonschema does not enforce "format" keywords (e.g.
        # homepage's format: "uri") unless a FormatChecker is explicitly supplied to the
        # validator - without it, this would silently pass.
        with pytest.raises(ValidationError):
            assert_json_schema_valid_configuration({
                "name": "Test",
                "homepage": "not a uri",
                "sources": [{"source": "https://example.com/list.txt"}],
            })

    def test_accepts_valid_homepage_uri(self) -> None:
        assert_json_schema_valid_configuration({
            "name": "Test",
            "homepage": "https://github.com/BloqrAI/bloqr-core",
            "sources": [{"source": "https://example.com/list.txt"}],
        })


class TestReadConfigurationSchemaValidation:
    """Integration coverage: read_configuration() now runs schema validation."""

    def test_rejects_invalid_transformation_that_the_old_validator_only_warned_about(
        self, tmp_path: Path
    ) -> None:
        # Regression coverage for #518's acceptance criteria: CompilerConfiguration.validate()
        # only ever downgraded an invalid transformation name to a warning
        # (result.add_warning(...), never add_error(...)), so a config with a typo'd
        # transformation name would previously load and "validate" successfully. Schema
        # validation, now chained ahead of it in read_configuration(), rejects it outright.
        config_file = tmp_path / "config.json"
        config_file.write_text(
            json.dumps({
                "name": "Test",
                "sources": [{"source": "https://example.com/list.txt"}],
                "transformations": ["NotARealTransformation"],
            })
        )

        with pytest.raises(ValidationError):
            read_configuration(config_file, format=ConfigurationFormat.JSON)

    def test_still_accepts_a_valid_configuration(self, tmp_path: Path) -> None:
        config_file = tmp_path / "config.json"
        config_file.write_text(
            json.dumps({
                "name": "Test",
                "sources": [{"source": "https://example.com/list.txt"}],
                "transformations": ["Deduplicate"],
            })
        )

        config = read_configuration(config_file, format=ConfigurationFormat.JSON)
        assert config.name == "Test"


class TestDirectlyConstructedConfigurationSchemaValidation:
    """Integration coverage: CompilerConfiguration.validate() also runs schema validation,
    not just read_configuration()'s file-based path - see the "Public config validation
    bypasses schema validation" finding on #518's PR: a directly constructed configuration
    (e.g. via BloqrCompiler.validate_config(), which never goes through read_configuration())
    must be checked too.
    """

    def test_validate_rejects_schema_invalid_transformation_on_a_directly_built_config(
        self,
    ) -> None:
        from bloqr_compiler.config import CompilerConfiguration, FilterSource

        config = CompilerConfiguration(
            name="Test",
            sources=[FilterSource(source="https://example.com/list.txt")],
            transformations=["NotARealTransformation"],
        )
        result = config.validate()
        assert result.is_valid is False
        assert any("NotARealTransformation" in e for e in result.errors)
