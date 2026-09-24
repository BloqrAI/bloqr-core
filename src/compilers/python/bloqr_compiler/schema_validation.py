"""
JSON Schema (draft-07) validation for parsed configuration dictionaries.

Validates against the same canonical schema (`schemas/compiler-config.schema.json`
at the repo root, bundled here as `bloqr_compiler/schemas/compiler-config.schema.json`
so the published `bloqr-compiler` package carries its own copy) that the .NET
compiler's `CompilerConfigJsonSchemaValidator` and the TypeScript compiler's
`assertJsonSchemaValidConfiguration` validate against - see issue #518 (follow-up
to #502). Kept in sync with the repo-root schema by `test_schema_validation.py`,
which fails if the two files diverge.
"""

from __future__ import annotations

import json
from functools import lru_cache
from pathlib import Path
from typing import Any

import jsonschema

from bloqr_compiler.errors import ValidationError

_SCHEMA_PATH = Path(__file__).parent / "schemas" / "compiler-config.schema.json"


@lru_cache(maxsize=1)
def _get_validator() -> jsonschema.protocols.Validator:
    with _SCHEMA_PATH.open(encoding="utf-8") as f:
        schema = json.load(f)
    validator_cls = jsonschema.validators.validator_for(schema)
    validator_cls.check_schema(schema)
    return validator_cls(schema)


def assert_json_schema_valid_configuration(data: dict[str, Any]) -> None:
    """
    Validate a parsed configuration dict against the canonical JSON Schema and
    raise a `ValidationError` with every violation if it doesn't conform.

    This is deliberately independent of, and runs ahead of, the hand-written
    checks in `CompilerConfiguration.validate()`: it catches structural/type/enum
    errors against the schema documented in `docs/configuration-reference.md` and
    shared with every other compiler wrapper, while `CompilerConfiguration.validate()`
    continues to catch anything specific to this package (e.g. local file existence,
    regex pattern validity).

    Args:
        data: The parsed (but not yet deserialized into `CompilerConfiguration`)
            configuration dict, as returned by the format-specific parser.

    Raises:
        ValidationError: If `data` fails schema validation.
    """
    validator = _get_validator()
    errors = sorted(validator.iter_errors(data), key=lambda e: list(e.path))
    if errors:
        messages = [
            f"{'.'.join(str(p) for p in error.path) or '(root)'}: {error.message}"
            for error in errors
        ]
        raise ValidationError(messages)
