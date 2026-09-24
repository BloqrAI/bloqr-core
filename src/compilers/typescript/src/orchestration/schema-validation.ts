/**
 * JSON Schema (draft-07) validation for parsed configuration objects.
 *
 * Validates against the same canonical schema (`schemas/compiler-config.schema.json`
 * at the repo root, bundled here as `../schemas/compiler-config.schema.json` so the
 * published `@bloqr/compiler-core` package carries its own copy) that the .NET
 * compiler's `CompilerConfigJsonSchemaValidator` validates against - see issue #518
 * (follow-up to #502). Kept in sync with the repo-root schema by
 * `schema-sync.test.ts`, which fails if the two files diverge.
 */

import Ajv, { type ValidateFunction } from 'ajv';
import schema from '../schemas/compiler-config.schema.json' with { type: 'json' };
import { ConfigurationError, ErrorCode } from './errors.ts';

const ajv = new Ajv({ allErrors: true, strict: false });
let validateFn: ValidateFunction | undefined;

function getValidator(): ValidateFunction {
  validateFn ??= ajv.compile(schema);
  return validateFn;
}

/**
 * Validates `config` against the canonical JSON Schema and throws a
 * {@linkcode ConfigurationError} with every violation if it doesn't conform.
 *
 * This is deliberately independent of, and runs ahead of, the hand-written
 * checks in `validation.ts`: it catches structural/type/enum errors against
 * the schema documented in `docs/configuration-reference.md` and shared with
 * every other compiler wrapper, while `validation.ts` continues to catch
 * anything specific to this orchestration layer.
 *
 * @throws ConfigurationError if `config` fails schema validation.
 */
export function assertJsonSchemaValidConfiguration(config: unknown, filePath: string): void {
  const validate = getValidator();
  if (!validate(config)) {
    const issues = (validate.errors ?? [])
      .map((err) => `  ${err.instancePath || '(root)'}: ${err.message}`)
      .join('\n');
    throw new ConfigurationError(
      `Configuration schema validation failed:\n${issues}`,
      ErrorCode.CONFIG_VALIDATION_ERROR,
      { filePath },
    );
  }
}
