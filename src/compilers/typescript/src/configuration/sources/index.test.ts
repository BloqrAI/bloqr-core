/**
 * Unit tests for IConfigurationSource implementations.
 *
 * Run with:
 *   deno task test:src
 * or:
 *   deno test --allow-read --allow-write --allow-net --allow-env src/configuration/sources/index.test.ts
 */

import { assertEquals, assertRejects, assertThrows } from '@std/assert';
import {
  CliConfigurationSource,
  EnvConfigurationSource,
  FileConfigurationSource,
  ObjectConfigurationSource,
  OverrideConfigurationSource,
} from './index.ts';
import type { IFileSystem } from '../../types/index.ts';
import { SourceType } from '../../types/index.ts';

// ── ObjectConfigurationSource ─────────────────────────────────────────────────

Deno.test('ObjectConfigurationSource: sourceType is "object"', () => {
  const src = new ObjectConfigurationSource({});
  assertEquals(src.sourceType, 'object');
});

Deno.test('ObjectConfigurationSource: load() returns the supplied config', async () => {
  const config = { name: 'Test', sources: [{ source: 'https://a.com', type: SourceType.Hosts }] };
  const src = new ObjectConfigurationSource(config);
  const loaded = await src.load();
  assertEquals(loaded, config);
});

Deno.test('ObjectConfigurationSource: load() returns empty object when constructed with {}', async () => {
  const src = new ObjectConfigurationSource({});
  const loaded = await src.load();
  assertEquals(loaded, {});
});

// ── FileConfigurationSource ───────────────────────────────────────────────────

function makeFs(content: string): IFileSystem {
  return {
    readTextFile: async (_path: string) => content,
    writeTextFile: async () => {},
    exists: async () => true,
  };
}

function makeFailingFs(error: Error, exists: boolean = false): IFileSystem {
  return {
    readTextFile: async () => {
      throw error;
    },
    writeTextFile: async () => {},
    exists: async () => exists,
  };
}

Deno.test('FileConfigurationSource: sourceType is "file"', () => {
  const src = new FileConfigurationSource('./config.json', makeFs('{}'));
  assertEquals(src.sourceType, 'file');
});

Deno.test('FileConfigurationSource: load() returns parsed JSON from file', async () => {
  const fileContent = JSON.stringify({
    name: 'From File',
    sources: [{ source: 'https://example.com/hosts.txt', type: 'hosts' }],
  });
  const src = new FileConfigurationSource('./config.json', makeFs(fileContent));
  const loaded = await src.load();
  assertEquals((loaded as { name: string }).name, 'From File');
});

Deno.test('FileConfigurationSource: load() throws on invalid JSON', async () => {
  const src = new FileConfigurationSource('./bad.json', makeFs('not-valid-json{'));
  await assertRejects(() => src.load(), Error);
});

Deno.test('FileConfigurationSource: load() throws when file not found', async () => {
  const notFoundError = new Deno.errors.NotFound('not found');
  const src = new FileConfigurationSource('./missing.json', makeFailingFs(notFoundError));
  await assertRejects(() => src.load(), Error, 'not found');
});

Deno.test('FileConfigurationSource: load() wraps unexpected errors', async () => {
  const unexpectedError = new Error('unexpected error');
  const src = new FileConfigurationSource('./config.json', makeFailingFs(unexpectedError, true));
  await assertRejects(() => src.load(), Error, 'unexpected error');
});

// ── EnvConfigurationSource ────────────────────────────────────────────────────

Deno.test('EnvConfigurationSource: sourceType is "env"', () => {
  const src = new EnvConfigurationSource(() => undefined);
  assertEquals(src.sourceType, 'env');
});

Deno.test('EnvConfigurationSource: load() returns empty object when no env vars are set', async () => {
  const src = new EnvConfigurationSource(() => undefined);
  const loaded = await src.load();
  assertEquals(loaded, {});
});

Deno.test('EnvConfigurationSource: load() reads ADBLOCK_CONFIG_NAME', async () => {
  const src = new EnvConfigurationSource((k) =>
    k === 'ADBLOCK_CONFIG_NAME' ? 'Env Name' : undefined
  );
  const loaded = await src.load();
  assertEquals(loaded.name, 'Env Name');
});

Deno.test('EnvConfigurationSource: load() reads ADBLOCK_CONFIG_DESCRIPTION', async () => {
  const src = new EnvConfigurationSource((k) =>
    k === 'ADBLOCK_CONFIG_DESCRIPTION' ? 'Env Desc' : undefined
  );
  const loaded = await src.load();
  assertEquals(loaded.description, 'Env Desc');
});

Deno.test('EnvConfigurationSource: load() reads ADBLOCK_CONFIG_HOMEPAGE', async () => {
  const src = new EnvConfigurationSource((k) =>
    k === 'ADBLOCK_CONFIG_HOMEPAGE' ? 'https://example.com' : undefined
  );
  const loaded = await src.load();
  assertEquals(loaded.homepage, 'https://example.com');
});

Deno.test('EnvConfigurationSource: load() reads ADBLOCK_CONFIG_LICENSE', async () => {
  const src = new EnvConfigurationSource((k) => k === 'ADBLOCK_CONFIG_LICENSE' ? 'MIT' : undefined);
  const loaded = await src.load();
  assertEquals(loaded.license, 'MIT');
});

Deno.test('EnvConfigurationSource: load() reads ADBLOCK_CONFIG_VERSION', async () => {
  const src = new EnvConfigurationSource((k) =>
    k === 'ADBLOCK_CONFIG_VERSION' ? '1.2.3' : undefined
  );
  const loaded = await src.load();
  assertEquals(loaded.version, '1.2.3');
});

Deno.test('EnvConfigurationSource: load() reads all env vars when all are set', async () => {
  const env: Record<string, string> = {
    ADBLOCK_CONFIG_NAME: 'All Set',
    ADBLOCK_CONFIG_DESCRIPTION: 'A description',
    ADBLOCK_CONFIG_HOMEPAGE: 'https://homepage.example.com',
    ADBLOCK_CONFIG_LICENSE: 'Apache-2.0',
    ADBLOCK_CONFIG_VERSION: '2.0.0',
  };
  const src = new EnvConfigurationSource((k) => env[k]);
  const loaded = await src.load();
  assertEquals(loaded.name, 'All Set');
  assertEquals(loaded.description, 'A description');
  assertEquals(loaded.homepage, 'https://homepage.example.com');
  assertEquals(loaded.license, 'Apache-2.0');
  assertEquals(loaded.version, '2.0.0');
});

Deno.test('EnvConfigurationSource: does not set undefined keys (absent vars do not override)', async () => {
  const src = new EnvConfigurationSource((k) =>
    k === 'ADBLOCK_CONFIG_NAME' ? 'Only Name' : undefined
  );
  const loaded = await src.load();
  assertEquals(Object.keys(loaded).length, 1);
  assertEquals('description' in loaded, false);
  assertEquals('homepage' in loaded, false);
  assertEquals('license' in loaded, false);
  assertEquals('version' in loaded, false);
});

// ── CliConfigurationSource ────────────────────────────────────────────────────

Deno.test('CliConfigurationSource: sourceType is "cli"', () => {
  const src = new CliConfigurationSource(['https://example.com/hosts.txt']);
  assertEquals(src.sourceType, 'cli');
});

Deno.test('CliConfigurationSource: load() with hosts type produces correct sources', async () => {
  const src = new CliConfigurationSource(['https://example.com/hosts.txt'], 'hosts');
  const loaded = await src.load();
  assertEquals(Array.isArray(loaded.sources), true);
  assertEquals((loaded.sources as { source: string }[])[0].source, 'https://example.com/hosts.txt');
  assertEquals((loaded.sources as { type: string }[])[0].type, SourceType.Hosts);
});

Deno.test('CliConfigurationSource: load() with adblock type produces correct sources', async () => {
  const src = new CliConfigurationSource(['https://example.com/list.txt'], 'adblock');
  const loaded = await src.load();
  assertEquals((loaded.sources as { type: string }[])[0].type, SourceType.Adblock);
});

Deno.test('CliConfigurationSource: load() with multiple inputs produces multiple sources', async () => {
  const inputs = [
    'https://example.com/hosts1.txt',
    'https://example.com/hosts2.txt',
    'https://example.com/hosts3.txt',
  ];
  const src = new CliConfigurationSource(inputs, 'hosts');
  const loaded = await src.load();
  assertEquals((loaded.sources as unknown[]).length, 3);
});

Deno.test('CliConfigurationSource: load() defaults to hosts type when not specified', async () => {
  const src = new CliConfigurationSource(['https://example.com/hosts.txt']);
  const loaded = await src.load();
  assertEquals((loaded.sources as { type: string }[])[0].type, SourceType.Hosts);
});

// ── OverrideConfigurationSource ───────────────────────────────────────────────

Deno.test('OverrideConfigurationSource: sourceType is "override"', () => {
  const src = new OverrideConfigurationSource('{"name":"Test"}');
  assertEquals(src.sourceType, 'override');
});

Deno.test('OverrideConfigurationSource: load() returns the parsed JSON', async () => {
  const src = new OverrideConfigurationSource(
    '{"name":"Override Name","description":"Override Desc"}',
  );
  const loaded = await src.load();
  assertEquals((loaded as { name: string }).name, 'Override Name');
  assertEquals((loaded as { description: string }).description, 'Override Desc');
});

Deno.test('OverrideConfigurationSource: load() returns empty object for empty JSON object', async () => {
  const src = new OverrideConfigurationSource('{}');
  const loaded = await src.load();
  assertEquals(loaded, {});
});

Deno.test('OverrideConfigurationSource: constructor throws on invalid JSON', () => {
  assertThrows(() => new OverrideConfigurationSource('invalid-json'), Error);
});

Deno.test('OverrideConfigurationSource: error message contains original JSON on parse failure', () => {
  assertThrows(
    () => new OverrideConfigurationSource('{bad}'),
    Error,
    '{bad}',
  );
});
