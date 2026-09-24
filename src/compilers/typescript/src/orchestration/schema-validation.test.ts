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
      output: { path: '../bloqr-blocklists/output/adguard_user_filter.txt', conflictStrategy: 'rename' },
      archiving: { enabled: true, mode: 'automatic', retentionDays: 90 },
      sources: [{ name: 'User Rules', source: '../bloqr-blocklists/input/user-rules.txt', type: 'adblock' }],
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
