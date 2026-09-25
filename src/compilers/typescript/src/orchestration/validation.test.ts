/**
 * Tests for configuration validation and input sanitization
 * Deno-native testing implementation
 */

import { assertEquals, assertThrows } from '@std/assert';
import {
  assertValidConfiguration,
  checkFileSize,
  checkSourceCount,
  containsPathTraversal,
  DEFAULT_RESOURCE_LIMITS,
  validateConfiguration,
  validateSourcePath,
  validateUrl,
} from './validation.ts';
import { ConfigurationError, ResourceLimitError } from './errors.ts';
import { CANONICAL_TRANSFORMATION_ORDER } from '../transformations/TransformationRegistry.ts';

// validateConfiguration tests
Deno.test('validateConfiguration - validates a valid minimal configuration', () => {
  const config = {
    name: 'Test Filter',
    sources: [{ source: 'https://example.com/filter.txt' }],
  };

  const result = validateConfiguration(config);
  assertEquals(result.valid, true);
  assertEquals(result.errors.length, 0);
});

Deno.test('validateConfiguration - validates a complete configuration', () => {
  const config = {
    name: 'Test Filter',
    description: 'A test filter',
    version: '1.0.0',
    license: 'MIT',
    homepage: 'https://example.com',
    sources: [
      {
        source: 'https://example.com/filter.txt',
        name: 'Main filter',
        type: 'adblock',
        transformations: ['RemoveComments', 'Deduplicate'],
      },
    ],
    transformations: ['RemoveEmptyLines'],
    inclusions: ['/pattern/'],
    exclusions: ['/excluded/'],
  };

  const result = validateConfiguration(config);
  assertEquals(result.valid, true);
  assertEquals(result.errors.length, 0);
});

Deno.test('validateConfiguration - rejects non-object configuration', () => {
  assertEquals(validateConfiguration(null).valid, false);
  assertEquals(validateConfiguration('string').valid, false);
  assertEquals(validateConfiguration(123).valid, false);
});

Deno.test('validateConfiguration - requires name field', () => {
  const config = { sources: [{ source: 'https://example.com' }] };
  const result = validateConfiguration(config);

  assertEquals(result.valid, false);
  assertEquals(result.errors.some((e) => e.includes('name')), true);
});

Deno.test('validateConfiguration - rejects empty name', () => {
  const config = { name: '   ', sources: [{ source: 'https://example.com' }] };
  const result = validateConfiguration(config);

  assertEquals(result.valid, false);
  assertEquals(result.errors.some((e) => e.includes('name')), true);
});

Deno.test('validateConfiguration - requires sources field', () => {
  const config = { name: 'Test' };
  const result = validateConfiguration(config);

  assertEquals(result.valid, false);
  assertEquals(result.errors.some((e) => e.includes('sources')), true);
});

Deno.test('validateConfiguration - rejects empty sources array', () => {
  const config = { name: 'Test', sources: [] };
  const result = validateConfiguration(config);

  assertEquals(result.valid, false);
  assertEquals(result.errors.some((e) => e.includes('sources')), true);
});

Deno.test('validateConfiguration - validates source objects', () => {
  const config = {
    name: 'Test',
    sources: [{ name: 'Missing source field' }],
  };
  const result = validateConfiguration(config);

  assertEquals(result.valid, false);
  assertEquals(result.errors.some((e) => e.includes('sources[0]') && e.includes('source')), true);
});

Deno.test('validateConfiguration - validates source type values', () => {
  const config = {
    name: 'Test',
    sources: [{ source: 'https://example.com', type: 'invalid' }],
  };
  const result = validateConfiguration(config);

  assertEquals(result.valid, false);
  assertEquals(result.errors.some((e) => e.includes('type')), true);
});

Deno.test('validateConfiguration - validates transformation names', () => {
  const config = {
    name: 'Test',
    sources: [{ source: 'https://example.com' }],
    transformations: ['InvalidTransformation'],
  };
  const result = validateConfiguration(config);

  assertEquals(result.valid, false);
  assertEquals(result.errors.some((e) => e.includes('InvalidTransformation')), true);
});

Deno.test('validateConfiguration - accepts valid transformations', () => {
  const config = {
    name: 'Test',
    sources: [{ source: 'https://example.com' }],
    transformations: ['RemoveComments', 'Compress', 'Deduplicate'],
  };
  const result = validateConfiguration(config);

  assertEquals(result.valid, true);
});

Deno.test('validateConfiguration - accepts every registered transformation (issue #502 regression)', () => {
  // Guards against the validator's transformation list drifting out of sync
  // with TransformationRegistry's actual registered defaults again - it
  // previously hand-duplicated the list and rejected valid, registered
  // transformations.
  for (const transformation of CANONICAL_TRANSFORMATION_ORDER) {
    const config = {
      name: 'Test',
      sources: [{ source: 'https://example.com' }],
      transformations: [transformation],
    };
    const result = validateConfiguration(config);

    assertEquals(
      result.valid,
      true,
      `expected '${transformation}' to be accepted, got errors: ${result.errors.join(', ')}`,
    );
  }
});

Deno.test('validateConfiguration - rejects transformation names that are not registered', () => {
  // Guards against the validator accepting a name that TransformationRegistry
  // wouldn't actually register - which would let a config "succeed" and then
  // have TransformationPipeline.transform() silently skip the unrecognized
  // transformation at compile time (issue #502).
  const config = {
    name: 'Test',
    sources: [{ source: 'https://example.com' }],
    transformations: ['NotARealTransformation'],
  };
  const result = validateConfiguration(config);

  assertEquals(result.valid, false);
  assertEquals(result.errors.some((e) => e.includes('NotARealTransformation')), true);
});

Deno.test('validateConfiguration - warns about invalid homepage URL', () => {
  const config = {
    name: 'Test',
    sources: [{ source: 'https://example.com' }],
    homepage: 'not-a-url',
  };
  const result = validateConfiguration(config);

  assertEquals(result.warnings.some((w) => w.includes('homepage')), true);
});

// assertValidConfiguration tests
Deno.test('assertValidConfiguration - does not throw for valid configuration', () => {
  const config = {
    name: 'Test',
    sources: [{ source: 'https://example.com' }],
  };

  // Should not throw
  assertValidConfiguration(config);
});

Deno.test('assertValidConfiguration - throws ConfigurationError for invalid configuration', () => {
  const config = { name: '' };

  assertThrows(
    () => assertValidConfiguration(config),
    ConfigurationError,
  );
});

// containsPathTraversal tests
Deno.test('containsPathTraversal - detects .. sequences', () => {
  assertEquals(containsPathTraversal('../file.txt'), true);
  assertEquals(containsPathTraversal('path/../file.txt'), true);
  assertEquals(containsPathTraversal('../../etc/passwd'), true);
});

Deno.test('containsPathTraversal - detects null bytes', () => {
  assertEquals(containsPathTraversal('file\0.txt'), true);
});

Deno.test('containsPathTraversal - allows safe relative paths', () => {
  assertEquals(containsPathTraversal('file.txt'), false);
  assertEquals(containsPathTraversal('./file.txt'), false);
  assertEquals(containsPathTraversal('path/to/file.txt'), false);
});

Deno.test('containsPathTraversal - allows absolute paths', () => {
  assertEquals(containsPathTraversal('/absolute/path/file.txt'), false);
});

// validateUrl tests
Deno.test('validateUrl - accepts http URLs', () => {
  assertEquals(validateUrl('http://example.com'), true);
  assertEquals(validateUrl('http://example.com/path'), true);
});

Deno.test('validateUrl - accepts https URLs', () => {
  assertEquals(validateUrl('https://example.com'), true);
  assertEquals(validateUrl('https://example.com/path?query=1'), true);
});

Deno.test('validateUrl - rejects other protocols', () => {
  assertEquals(validateUrl('ftp://example.com'), false);
  assertEquals(validateUrl('file:///path'), false);
  assertEquals(validateUrl('javascript:alert(1)'), false);
});

Deno.test('validateUrl - rejects invalid URLs', () => {
  assertEquals(validateUrl('not-a-url'), false);
  assertEquals(validateUrl(''), false);
});

// validateSourcePath tests
Deno.test('validateSourcePath - validates http URLs', () => {
  const result = validateSourcePath('https://example.com/filter.txt');
  assertEquals(result.valid, true);
  assertEquals(result.isUrl, true);
});

Deno.test('validateSourcePath - validates file paths', () => {
  const result = validateSourcePath('filters/list.txt');
  assertEquals(result.valid, true);
  assertEquals(result.isUrl, false);
});

Deno.test('validateSourcePath - rejects path traversal in file paths', () => {
  const result = validateSourcePath('../../../etc/passwd');
  assertEquals(result.valid, false);
  assertEquals(result.error?.includes('traversal'), true);
});

Deno.test('validateSourcePath - rejects empty source', () => {
  const result = validateSourcePath('');
  assertEquals(result.valid, false);
});

// checkFileSize tests
Deno.test('checkFileSize - does not throw when under limit', () => {
  // Should not throw
  checkFileSize(1000, 2000, 'test');
});

Deno.test('checkFileSize - throws ResourceLimitError when over limit', () => {
  assertThrows(
    () => checkFileSize(3000, 2000, 'test'),
    ResourceLimitError,
  );
});

// checkSourceCount tests
Deno.test('checkSourceCount - does not throw when under limit', () => {
  // Should not throw
  checkSourceCount(10, 100);
});

Deno.test('checkSourceCount - throws ResourceLimitError when over limit', () => {
  assertThrows(
    () => checkSourceCount(200, 100),
    ResourceLimitError,
  );
});

Deno.test('checkSourceCount - uses default limit', () => {
  // Should not throw
  checkSourceCount(10);

  assertThrows(
    () => checkSourceCount(DEFAULT_RESOURCE_LIMITS.maxSources + 1),
    ResourceLimitError,
  );
});

// DEFAULT_RESOURCE_LIMITS tests
Deno.test('DEFAULT_RESOURCE_LIMITS - has reasonable defaults', () => {
  assertEquals(DEFAULT_RESOURCE_LIMITS.maxConfigFileSize > 0, true);
  assertEquals(DEFAULT_RESOURCE_LIMITS.maxOutputFileSize > 0, true);
  assertEquals(DEFAULT_RESOURCE_LIMITS.maxSources > 0, true);
  assertEquals(DEFAULT_RESOURCE_LIMITS.compilationTimeoutMs > 0, true);
  assertEquals(DEFAULT_RESOURCE_LIMITS.maxPathLength > 0, true);
});

// Regression coverage (Copilot review on #528/#518): validateConfiguration() is the shared
// implementation behind every public in-memory ValidationResult API (BloqrCompiler.validate()/
// .validateConfig()), which previously ran only the hand-written checks below and could report
// a schema-invalid configuration as valid - unlike readConfiguration()'s file-reading path,
// which schema-validates independently. validateConfiguration() now runs the same canonical
// JSON Schema check first and folds violations into `errors`.

Deno.test('validateConfiguration - rejects an unrecognized top-level property', () => {
  const config = {
    name: 'x',
    sources: [{ source: 's' }],
    bogusField: true,
  };

  const result = validateConfiguration(config);
  assertEquals(result.valid, false);
});

Deno.test('validateConfiguration - rejects an unrecognized source property', () => {
  const config = {
    name: 'x',
    sources: [{ source: 's', bogus: true }],
  };

  const result = validateConfiguration(config);
  assertEquals(result.valid, false);
});

Deno.test('validateConfiguration - rejects a schema-invalid version string', () => {
  const config = {
    name: 'x',
    version: 'not-a-semver',
    sources: [{ source: 's' }],
  };

  const result = validateConfiguration(config);
  assertEquals(result.valid, false);
});

// Regression coverage: readConfiguration() returns an ExtendedConfiguration tagged with
// _sourceFormat/_sourcePath (and splitIntoChunks() adds _chunkMetadata) - internal
// bookkeeping added *after* its own schema check runs. validateConfiguration() must not
// re-reject that same object as schema-invalid "additional properties" when called on it
// directly, as runValidationMode() (the --validate CLI flag) does - this was a real CI
// failure caught by the Bun smoke test running against the shipped compiler-config.json.
Deno.test('validateConfiguration - does not reject internal metadata as additional properties', () => {
  const config = {
    name: 'Test',
    sources: [{ source: 'https://example.com/list.txt' }],
    _sourceFormat: 'json',
    _sourcePath: '/tmp/compiler-config.json',
  };

  const result = validateConfiguration(config);
  assertEquals(result.valid, true);
  assertEquals(result.errors.length, 0);
});
