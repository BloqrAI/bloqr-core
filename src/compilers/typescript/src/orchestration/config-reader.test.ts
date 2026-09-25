/**
 * Tests for configuration reader
 * Deno-native testing implementation
 */

import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { assertEquals, assertThrows } from '@std/assert';
import {
  detectFormat,
  readConfiguration,
  stripForCoreCompile,
  stripInternalMetadata,
  toJson,
} from './config-reader.ts';
import { ConfigurationError } from './errors.ts';
import type { IConfiguration } from '../index.ts';

function withTempConfigFile<T>(content: unknown, fn: (configPath: string) => T): T {
  const dir = mkdtempSync(join(tmpdir(), 'config-reader-test-'));
  try {
    const configPath = join(dir, 'config.json');
    writeFileSync(configPath, JSON.stringify(content));
    return fn(configPath);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
}

Deno.test('detectFormat - detects JSON format from .json extension', () => {
  assertEquals(detectFormat('config.json'), 'json');
  assertEquals(detectFormat('/path/to/compiler-config.json'), 'json');
});

Deno.test('detectFormat - detects YAML format from .yaml extension', () => {
  assertEquals(detectFormat('config.yaml'), 'yaml');
  assertEquals(detectFormat('/path/to/compiler-config.yaml'), 'yaml');
});

Deno.test('detectFormat - detects YAML format from .yml extension', () => {
  assertEquals(detectFormat('config.yml'), 'yaml');
  assertEquals(detectFormat('/path/to/compiler-config.yml'), 'yaml');
});

Deno.test('detectFormat - detects TOML format from .toml extension', () => {
  assertEquals(detectFormat('config.toml'), 'toml');
  assertEquals(detectFormat('/path/to/compiler-config.toml'), 'toml');
});

Deno.test('detectFormat - is case-insensitive for extensions', () => {
  assertEquals(detectFormat('config.JSON'), 'json');
  assertEquals(detectFormat('config.YAML'), 'yaml');
  assertEquals(detectFormat('config.TOML'), 'toml');
});

Deno.test('detectFormat - throws error for unknown extension .xml', () => {
  assertThrows(
    () => detectFormat('config.xml'),
    Error,
    'Unknown configuration file extension: .xml',
  );
});

Deno.test('detectFormat - throws error for unknown extension .txt', () => {
  assertThrows(
    () => detectFormat('config.txt'),
    Error,
    'Unknown configuration file extension: .txt',
  );
});

// Regression coverage for #427: readConfiguration() tags the config object it returns with
// `_sourceFormat`/`_sourcePath` (and splitIntoChunks() adds `_chunkMetadata`) - orchestration-
// layer-only bookkeeping that the core engine's strict schema validator rejects as unknown
// properties if it ever reaches compile(). stripInternalMetadata()/toJson() must remove all
// three, not just the two ExtendedConfiguration originally declared.

Deno.test('stripInternalMetadata - removes _sourceFormat and _sourcePath', () => {
  const config = {
    name: 'Test',
    sources: [],
    _sourceFormat: 'json',
    _sourcePath: '/tmp/config.json',
  } as IConfiguration & { _sourceFormat: string; _sourcePath: string };

  const clean = stripInternalMetadata(config);

  assertEquals(Object.hasOwn(clean, '_sourceFormat'), false);
  assertEquals(Object.hasOwn(clean, '_sourcePath'), false);
  assertEquals(clean.name, 'Test');
});

Deno.test('stripInternalMetadata - removes _chunkMetadata', () => {
  const config = {
    name: 'Test (chunk 1/2)',
    sources: [],
    _chunkMetadata: { index: 0, total: 2, estimatedRules: 0, sources: [] },
  } as IConfiguration & { _chunkMetadata: unknown };

  const clean = stripInternalMetadata(config);

  assertEquals(Object.hasOwn(clean, '_chunkMetadata'), false);
});

Deno.test('stripInternalMetadata - leaves ordinary configuration fields untouched', () => {
  const config: IConfiguration = {
    name: 'Test',
    description: 'A test config',
    version: '1.0.0',
    sources: [{ name: 'src', source: 'rules.txt', type: undefined }],
    transformations: undefined,
  };

  const clean = stripInternalMetadata(config);

  assertEquals(clean.name, 'Test');
  assertEquals(clean.description, 'A test config');
  assertEquals(clean.version, '1.0.0');
  assertEquals(clean.sources.length, 1);
});

// Regression coverage (Copilot review on #528/#518): output/chunking/archiving/
// hashVerification/$schema are all valid per the canonical schema (a config using them
// passes readConfiguration()'s schema check), but the core engine's ConfigurationSchema
// (configuration/schemas.ts) is `.strict()` and doesn't model any of them - so they must
// be stripped before compile(), same as internal metadata, or a schema-valid config
// fails at the actual compile call. stripForCoreCompile() is the function that does that;
// stripInternalMetadata()/toJson() deliberately do NOT strip them, since those sections
// are user-visible configuration, not internal bookkeeping.

Deno.test('stripForCoreCompile - removes output/chunking/archiving/hashVerification/$schema', () => {
  const config = {
    name: 'Test',
    sources: [],
    output: { fileName: 'out.txt' },
    chunking: { enabled: true },
    archiving: { enabled: true },
    hashVerification: { mode: 'strict' },
    $schema: '../../../../../schemas/compiler-config.schema.json',
  } as unknown as IConfiguration;

  const clean = stripForCoreCompile(config);

  assertEquals(Object.hasOwn(clean, 'output'), false);
  assertEquals(Object.hasOwn(clean, 'chunking'), false);
  assertEquals(Object.hasOwn(clean, 'archiving'), false);
  assertEquals(Object.hasOwn(clean, 'hashVerification'), false);
  assertEquals(Object.hasOwn(clean, '$schema'), false);
  assertEquals(clean.name, 'Test');
});

Deno.test('stripForCoreCompile - also removes internal metadata', () => {
  const config = {
    name: 'Test',
    sources: [],
    _sourceFormat: 'json',
    _sourcePath: '/tmp/config.json',
    chunking: { enabled: true },
  } as unknown as IConfiguration;

  const clean = stripForCoreCompile(config);

  assertEquals(Object.hasOwn(clean, '_sourceFormat'), false);
  assertEquals(Object.hasOwn(clean, '_sourcePath'), false);
  assertEquals(Object.hasOwn(clean, 'chunking'), false);
});

Deno.test('stripForCoreCompile - leaves extensions (allowed by the core schema) untouched', () => {
  const config = {
    name: 'Test',
    sources: [],
    extensions: { team: 'security' },
  } as unknown as IConfiguration;

  const clean = stripForCoreCompile(config) as IConfiguration & {
    extensions?: Record<string, unknown>;
  };

  assertEquals(clean.extensions, { team: 'security' });
});

Deno.test('toJson - keeps output/chunking (user-visible, unlike internal metadata)', () => {
  const config = {
    name: 'Test',
    sources: [],
    output: { fileName: 'out.txt' },
    chunking: { enabled: true },
  } as unknown as IConfiguration;

  const json = toJson(config);

  assertEquals(json.includes('output'), true);
  assertEquals(json.includes('chunking'), true);
});

Deno.test('toJson - serialized output never contains internal metadata keys', () => {
  const config = {
    name: 'Test',
    sources: [],
    _sourceFormat: 'json',
    _sourcePath: '/tmp/config.json',
    _chunkMetadata: { index: 0, total: 1, estimatedRules: 0, sources: [] },
  } as IConfiguration & { _sourceFormat: string; _sourcePath: string; _chunkMetadata: unknown };

  const json = toJson(config);

  assertEquals(json.includes('_sourceFormat'), false);
  assertEquals(json.includes('_sourcePath'), false);
  assertEquals(json.includes('_chunkMetadata'), false);
});

// Regression coverage for #518: proves readConfiguration() itself - not just
// assertJsonSchemaValidConfiguration() called directly - rejects a schema-invalid
// config. Without this, removing or reordering the schema-validation call inside
// readConfiguration() would leave schema-validation.test.ts green while the actual
// CLI/compile config-read path silently stopped enforcing the canonical schema.
Deno.test('readConfiguration - rejects a schema-invalid config file (invalid transformation enum value)', () => {
  withTempConfigFile(
    {
      name: 'Test',
      sources: [{ source: 'https://example.com/list.txt' }],
      transformations: ['NotARealTransformation'],
    },
    (configPath) => {
      assertThrows(
        () => readConfiguration(configPath),
        ConfigurationError,
      );
    },
  );
});

Deno.test('readConfiguration - accepts a schema-valid config file', () => {
  withTempConfigFile(
    {
      name: 'Test',
      sources: [{ source: 'https://example.com/list.txt' }],
      transformations: ['Deduplicate'],
    },
    (configPath) => {
      const config = readConfiguration(configPath);
      assertEquals(config.name, 'Test');
    },
  );
});
