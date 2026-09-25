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
import * as AjvFormatsNamespace from 'ajv-formats';
import schema from '../schemas/compiler-config.schema.json' with { type: 'json' };
import { ConfigurationError, ErrorCode } from './errors.ts';

/** The subset of the `ajv` instance API this module relies on. */
interface AjvLike {
  compile(schema: unknown): ValidateFunction;
}

/**
 * `ajv`'s own package does not implement `format` keywords (e.g. this schema's
 * `homepage: { format: "uri" }`) at all - they're silently never checked without
 * `ajv-formats` registered. Resolved through the same namespace/`.default` interop
 * dance as `AjvCtor` below, since `ajv-formats` is also a CJS module with a default
 * export.
 */
const addFormats = ((AjvFormatsNamespace as unknown as { default?: unknown }).default ??
  AjvFormatsNamespace) as (ajv: AjvLike, options?: unknown) => void;

/** Shape of the `ajv` module namespace once ESM/CJS interop unwraps it. */
interface AjvModuleShape {
  default: new (options?: Record<string, unknown>) => AjvLike;
}

/**
 * `ajv` ships as a CommonJS module (`module.exports = Ajv`, with `.default` also set to
 * the same class for ESM interop). A plain default import of it (`import Ajv from 'ajv'`)
 * both mistypes under `deno check` (the binding types as the whole module namespace, which
 * has no construct signature - a `.d.ts` resolution quirk in Deno's npm compat layer) *and*
 * fails at runtime under real ESM interop (Bun/Node resolve the default import to the
 * namespace object itself, not the class it wraps: `new Ajv(...)` throws "Module is not a
 * constructor"). Importing the namespace and reading `.default` off it explicitly - which is
 * where ESM interop actually places the class - avoids both problems; the `?? AjvNamespace`
 * fallback covers any runtime where the namespace import unwraps directly to `module.exports`
 * instead.
 */
const AjvCtor = ((AjvNamespace as unknown as AjvModuleShape).default ??
  AjvNamespace) as unknown as new (options?: Record<string, unknown>) => AjvLike;

const ajv: AjvLike = new AjvCtor({ allErrors: true, strict: false });
addFormats(ajv);
let validateFn: ValidateFunction | undefined;

function getValidator(): ValidateFunction {
  if (validateFn === undefined) {
    validateFn = ajv.compile(schema);
  }
  return validateFn as ValidateFunction;
}

/**
 * Validates `config` against the canonical JSON Schema and returns one message per
 * violation (empty array if `config` conforms). Used both by
 * {@linkcode assertJsonSchemaValidConfiguration} (the throwing form used by the
 * file-reading path) and by `validation.ts`'s `validateConfiguration()` (the
 * `ValidationResult`-returning form used by every public in-memory validation API), so
 * schema validation can't be bypassed by calling one instead of the other.
 */
export function getJsonSchemaValidationErrors(config: unknown): string[] {
  const validate = getValidator();
  if (validate(config)) {
    return [];
  }
  const errors: ErrorObject[] = validate.errors ?? [];
  return errors.map((err) => `${err.instancePath || '(root)'}: ${err.message}`);
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
  const issues = getJsonSchemaValidationErrors(config);
  if (issues.length > 0) {
    throw new ConfigurationError(
      `Configuration schema validation failed:\n${issues.map((i) => `  ${i}`).join('\n')}`,
      ErrorCode.CONFIG_VALIDATION_ERROR,
      { filePath },
    );
  }
}
