/**
 * JSON Schema (draft-07) validation for parsed configuration objects.
 *
 * Validates against the same canonical schema (`schemas/compiler-config.schema.json`
 * at the repo root, bundled here as `../schemas/compiler-config.schema.json` so the
 * published `@bloqr/compiler-core` package carries its own copy) that the .NET
 * compiler's `CompilerConfigJsonSchemaValidator` validates against - see issue #518
 * (follow-up to #502). Kept in sync with the repo-root schema by
 * `schema-validation.test.ts`, which fails if the two files diverge.
 */

import type { ErrorObject, ValidateFunction } from 'ajv';
import * as AjvNamespace from 'ajv';
import schema from '../schemas/compiler-config.schema.json' with { type: 'json' };
import { ConfigurationError, ErrorCode } from './errors.ts';

/** The subset of the `ajv` instance API this module relies on. */
interface AjvLike {
  compile(schema: unknown): ValidateFunction;
}

/**
 * `ajv` ships as a CommonJS module (`module.exports = Ajv`, with `.default` set to the
 * same value for ESM interop). Under `deno check`, a default import of it (`import Ajv
 * from 'ajv'`) types the binding as the whole module namespace rather than the exported
 * class - a `.d.ts` resolution mismatch in Deno's npm compat layer, not a runtime bug (the
 * namespace *value* really is the constructable class at runtime; only its inferred type
 * is wrong). Importing the namespace and casting it past that mistyping avoids relying on
 * the broken default-import type while keeping the real runtime value.
 */
const AjvCtor = AjvNamespace as unknown as new (options?: Record<string, unknown>) => AjvLike;

const ajv: AjvLike = new AjvCtor({ allErrors: true, strict: false });
let validateFn: ValidateFunction | undefined;

function getValidator(): ValidateFunction {
  if (validateFn === undefined) {
    validateFn = ajv.compile(schema);
  }
  return validateFn as ValidateFunction;
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
    const errors: ErrorObject[] = validate.errors ?? [];
    const issues = errors
      .map((err) => `  ${err.instancePath || '(root)'}: ${err.message}`)
      .join('\n');
    throw new ConfigurationError(
      `Configuration schema validation failed:\n${issues}`,
      ErrorCode.CONFIG_VALIDATION_ERROR,
      { filePath },
    );
  }
}
