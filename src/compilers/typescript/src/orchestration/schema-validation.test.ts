/**
 * Tests for JSON Schema config validation, including a drift guard between the
 * bundled schema copy (`src/schemas/compiler-config.schema.json`) and the
 * repo-root canonical schema (`schemas/compiler-config.schema.json`) - see issue #518.
 */

import { assertEquals, assertThrows } from '@std/assert';
import { fromFileUrl } from '@std/path';
import { assertJsonSchemaValidConfiguration } from './schema-validation.ts';
import { ConfigurationError } from './errors.ts';

Deno.test('bundled schema copy stays in sync with the repo-root canonical schema', () => {
  const bundledPath = fromFileUrl(import.meta.resolve('../schemas/compiler-config.schema.json'));
  const canonicalPath = fromFileUrl(
    import.meta.resolve('../../../../../schemas/compiler-config.schema.json'),
  );

  const bundled = Deno.readTextFileSync(bundledPath);
  const canonical = Deno.readTextFileSync(canonicalPath);

  assertEquals(
    bundled,
    canonical,
    'src/schemas/compiler-config.schema.json has drifted from the repo-root ' +
      'schemas/compiler-config.schema.json - copy the canonical file over the bundled one ' +
      '(cp schemas/compiler-config.schema.json src/compilers/typescript/src/schemas/compiler-config.schema.json).',
  );
});

Deno.test('assertJsonSchemaValidConfiguration - accepts a minimal valid configuration', () => {
  assertJsonSchemaValidConfiguration(
    { name: 'Test', sources: [{ source: 'https://example.com/list.txt' }] },
    'config.json',
  );
});

Deno.test('assertJsonSchemaValidConfiguration - accepts the shipped default compiler-config.json shape', () => {
  // Regression coverage: this shape (with `$schema`, `output`, `archiving`, and
  // Compress without a preceding Deduplicate) is what ships as this package's own
  // default config (compiler-config.json) and must keep validating.
  assertJsonSchemaValidConfiguration(
    {
      $schema: '../../../schemas/compiler-config.schema.json',
      name: 'AdGuard User Filter',
      version: '1.0.0',
      description: 'Custom filter rules for AdGuard DNS',
      license: 'MIT',
      homepage: 'https://github.com/BloqrAI/bloqr-lists',
      output: {
        path: '../bloqr-blocklists/output/adguard_user_filter.txt',
        conflictStrategy: 'rename',
      },
      archiving: { enabled: true, mode: 'automatic', retentionDays: 90 },
      sources: [{
        name: 'User Rules',
        source: '../bloqr-blocklists/input/user-rules.txt',
        type: 'adblock',
      }],
      transformations: ['RemoveComments', 'Compress', 'Validate'],
    },
    'compiler-config.json',
  );
});

Deno.test('assertJsonSchemaValidConfiguration - rejects a config missing required fields', () => {
  assertThrows(
    () => assertJsonSchemaValidConfiguration({}, 'config.json'),
    ConfigurationError,
  );
});

Deno.test('assertJsonSchemaValidConfiguration - rejects a non-URI homepage', () => {
  // Regression coverage: ajv does not implement "format" keywords (e.g. homepage's
  // format: "uri") at all without ajv-formats registered - without it, this would
  // silently pass.
  assertThrows(
    () =>
      assertJsonSchemaValidConfiguration(
        {
          name: 'Test',
          homepage: 'not a uri',
          sources: [{ source: 'https://example.com/list.txt' }],
        },
        'config.json',
      ),
    ConfigurationError,
  );
});

Deno.test('assertJsonSchemaValidConfiguration - accepts a valid homepage URI', () => {
  assertJsonSchemaValidConfiguration(
    {
      name: 'Test',
      homepage: 'https://github.com/BloqrAI/bloqr-core',
      sources: [{ source: 'https://example.com/list.txt' }],
    },
    'config.json',
  );
});

Deno.test('assertJsonSchemaValidConfiguration - rejects an invalid transformation enum value', () => {
  assertThrows(
    () =>
      assertJsonSchemaValidConfiguration(
        {
          name: 'Test',
          sources: [{ source: 'https://example.com/list.txt' }],
          transformations: ['NotARealTransformation'],
        },
        'config.json',
      ),
    ConfigurationError,
  );
});

Deno.test('assertJsonSchemaValidConfiguration - rejects an unrecognized top-level property', () => {
  assertThrows(
    () =>
      assertJsonSchemaValidConfiguration(
        { name: 'Test', sources: [{ source: 'https://example.com/list.txt' }], bogusField: true },
        'config.json',
      ),
    ConfigurationError,
  );
});

Deno.test('assertJsonSchemaValidConfiguration - accepts ConflictDetection/RuleOptimizer transformations', () => {
  // Regression coverage: these were added to TransformationRegistry in #512 but the
  // canonical JSON schema's enum wasn't updated to match until #518.
  assertJsonSchemaValidConfiguration(
    {
      name: 'Test',
      sources: [{ source: 'https://example.com/list.txt' }],
      transformations: ['ConflictDetection', 'RuleOptimizer'],
    },
    'config.json',
  );
});

Deno.test('assertJsonSchemaValidConfiguration - accepts the shipped compiler-config-chunked.json shape', () => {
  // Regression coverage: ExtendedConfiguration.chunking (orchestration/types.ts) is read by
  // compileFilters() (compiler.ts's config.chunking?.* accesses) and documented in the
  // README, and is exactly what compiler-config-chunked.json - shipped alongside this
  // package - uses. The schema previously had no `chunking` property at all, so turning on
  // real validation would have rejected this file outright.
  assertJsonSchemaValidConfiguration(
    {
      name: 'Test Chunked Compilation',
      version: '1.0.0',
      description: 'Test configuration for chunked parallel compilation',
      license: 'MIT',
      chunking: { enabled: true, strategy: 'source', maxParallel: 4 },
      sources: [{
        name: 'AdGuard Base',
        source: 'https://filters.adtidy.org/extension/chromium/filters/2.txt',
      }],
      transformations: ['RemoveComments', 'Compress', 'Validate'],
    },
    'compiler-config-chunked.json',
  );
});

Deno.test('assertJsonSchemaValidConfiguration - accepts top-level extensions and per-source useBrowser', () => {
  // Regression coverage: IConfiguration.extensions and ISource.useBrowser (types/index.ts)
  // are both part of the public configuration contract - the existing Zod ConfigurationSchema
  // already accepts them - but were missing from the JSON schema.
  assertJsonSchemaValidConfiguration(
    {
      name: 'Test',
      extensions: { customKey: 'customValue' },
      sources: [{ source: 'https://example.com/list.txt', useBrowser: true }],
    },
    'config.json',
  );
});

Deno.test('assertJsonSchemaValidConfiguration - accepts output.fileName', () => {
  // Regression coverage: OutputConfig.fileName (orchestration/types.ts) is a documented
  // alternative to output.path, but was missing from the JSON schema.
  assertJsonSchemaValidConfiguration(
    {
      name: 'Test',
      output: { fileName: 'output.txt', conflictStrategy: 'overwrite' },
      sources: [{ source: 'https://example.com/list.txt' }],
    },
    'config.json',
  );
});
