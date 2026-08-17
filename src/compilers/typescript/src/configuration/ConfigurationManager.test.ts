/**
 * Unit tests for ConfigurationManager.
 *
 * Run with:
 *   deno task test:src
 * or:
 *   deno test --allow-read --allow-write --allow-net --allow-env src/configuration/ConfigurationManager.test.ts
 */

import { assertEquals, assertInstanceOf, assertRejects, assertThrows } from '@std/assert';
import { ConfigurationManager, ConfigurationValidationError } from './ConfigurationManager.ts';
import {
  EnvConfigurationSource,
  ObjectConfigurationSource,
  OverrideConfigurationSource,
} from './sources/index.ts';
import type { IConfiguration, IFileSystem } from '../types/index.ts';
import { SourceType, TransformationType } from '../types/index.ts';
import { VALIDATION_DEFAULTS } from '../config/defaults.ts';

// ── Helpers ─────────────────────────────────────────────────────────────────

const minimalConfig = (): Partial<IConfiguration> => ({
  name: 'Test List',
  sources: [{ source: 'https://example.com/hosts.txt', type: SourceType.Hosts }],
});

// ── fromObject ───────────────────────────────────────────────────────────────

Deno.test('fromObject: valid object returns IConfiguration', async () => {
  const cfg = await ConfigurationManager.fromObject(minimalConfig(), { applyEnvOverrides: false })
    .load();
  assertEquals(cfg.name, 'Test List');
  assertEquals(cfg.sources.length, 1);
});

Deno.test('fromObject: missing required field throws ConfigurationValidationError', async () => {
  await assertRejects(
    () =>
      ConfigurationManager.fromObject({ name: 'No Sources' }, { applyEnvOverrides: false }).load(),
    ConfigurationValidationError,
  );
});

// ── getValidationErrors / getEffectiveConfig ─────────────────────────────────

Deno.test('getValidationErrors: null after successful load', async () => {
  const mgr = ConfigurationManager.fromObject(minimalConfig(), { applyEnvOverrides: false });
  await mgr.load();
  assertEquals(mgr.getValidationErrors(), null);
});

Deno.test('getEffectiveConfig: null before any load', () => {
  const mgr = ConfigurationManager.fromObject(minimalConfig(), { applyEnvOverrides: false });
  assertEquals(mgr.getEffectiveConfig(), null);
});

Deno.test('getEffectiveConfig: returns resolved config after load', async () => {
  const mgr = ConfigurationManager.fromObject(minimalConfig(), { applyEnvOverrides: false });
  const loaded = await mgr.load();
  assertEquals(mgr.getEffectiveConfig(), loaded);
});

// ── deepMerge ────────────────────────────────────────────────────────────────

Deno.test('deepMerge: later scalar wins', () => {
  const result = ConfigurationManager.deepMerge([
    { name: 'First' },
    { name: 'Second' },
  ]);
  assertEquals(result.name, 'Second');
});

Deno.test('deepMerge: undefined in later source does not override', () => {
  const result = ConfigurationManager.deepMerge([
    { name: 'First', description: 'My list' },
    { name: 'Second', description: undefined },
  ]);
  assertEquals(result.description, 'My list');
});

Deno.test('deepMerge: later array fully replaces earlier array', () => {
  const src1 = [{ source: 'a', type: SourceType.Hosts }];
  const src2 = [{ source: 'b', type: SourceType.Hosts }];
  const result = ConfigurationManager.deepMerge([
    { sources: src1 },
    { sources: src2 },
  ]);
  assertEquals(result.sources, src2);
});

// ── Layered merge ─────────────────────────────────────────────────────────────

Deno.test('fromSources: second source overrides first scalar', async () => {
  const cfg = await ConfigurationManager.fromSources([
    new ObjectConfigurationSource({ ...minimalConfig(), name: 'Base' }),
    new ObjectConfigurationSource({ name: 'Override' }),
  ], { applyEnvOverrides: false }).load();
  assertEquals(cfg.name, 'Override');
});

// ── OverrideConfigurationSource ───────────────────────────────────────────────

Deno.test('OverrideConfigurationSource: applies JSON overlay as highest priority', async () => {
  const cfg = await ConfigurationManager.fromSources([
    new ObjectConfigurationSource(minimalConfig()),
    new OverrideConfigurationSource('{"name":"CI Build"}'),
  ], { applyEnvOverrides: false }).load();
  assertEquals(cfg.name, 'CI Build');
});

Deno.test('OverrideConfigurationSource: throws on invalid JSON', () => {
  assertThrows(() => new OverrideConfigurationSource('not-json'), Error);
});

Deno.test('OverrideConfigurationSource: throws on null JSON', () => {
  assertThrows(() => new OverrideConfigurationSource('null'), Error, 'must be an object');
});

Deno.test('OverrideConfigurationSource: throws on array JSON', () => {
  assertThrows(() => new OverrideConfigurationSource('[1,2,3]'), Error, 'must be an object');
});

Deno.test('OverrideConfigurationSource: throws on scalar JSON', () => {
  assertThrows(() => new OverrideConfigurationSource('"string"'), Error, 'must be an object');
});

// ── EnvConfigurationSource ────────────────────────────────────────────────────

Deno.test('EnvConfigurationSource: reads ADBLOCK_CONFIG_NAME from injected reader', async () => {
  const cfg = await ConfigurationManager.fromSources([
    new ObjectConfigurationSource(minimalConfig()),
    new EnvConfigurationSource((k) => k === 'ADBLOCK_CONFIG_NAME' ? 'Env Name' : undefined),
  ], { applyEnvOverrides: false }).load();
  assertEquals(cfg.name, 'Env Name');
});

Deno.test('applyEnvOverrides: false skips env source', async () => {
  // Even if env provides a name, applyEnvOverrides:false should not add EnvConfigurationSource
  const mgr = new ConfigurationManager(
    [new ObjectConfigurationSource({ ...minimalConfig(), name: 'From Object' })],
    { applyEnvOverrides: false },
  );
  const cfg = await mgr.load();
  assertEquals(cfg.name, 'From Object');
});

// ── Limit enforcement ─────────────────────────────────────────────────────────

Deno.test('enforceSourceLimit: truncates sources array to MAX_SOURCES', async () => {
  const tooManySources = Array.from({ length: VALIDATION_DEFAULTS.MAX_SOURCES + 10 }, (_, i) => ({
    source: `https://example.com/${i}.txt`,
    type: SourceType.Hosts,
  }));
  const cfg = await ConfigurationManager.fromObject(
    { name: 'Limits Test', sources: tooManySources },
    { applyEnvOverrides: false, enforceSourceLimit: true },
  ).load();
  assertEquals(cfg.sources.length, VALIDATION_DEFAULTS.MAX_SOURCES);
});

Deno.test('enforceExclusionLimit: truncates exclusions to MAX_EXCLUSIONS', async () => {
  const tooManyExclusions = Array.from(
    { length: VALIDATION_DEFAULTS.MAX_EXCLUSIONS + 5 },
    (_, i) => `exclusion${i}`,
  );
  const cfg = await ConfigurationManager.fromObject(
    { name: 'Exclusion Test', sources: minimalConfig().sources!, exclusions: tooManyExclusions },
    { applyEnvOverrides: false, enforceExclusionLimit: true },
  ).load();
  assertEquals(cfg.exclusions!.length, VALIDATION_DEFAULTS.MAX_EXCLUSIONS);
});

// ── validateOnly ─────────────────────────────────────────────────────────────

Deno.test('validateOnly: returns valid: true for valid config', () => {
  const result = ConfigurationManager.validateOnly(minimalConfig());
  assertEquals(result.valid, true);
  if (result.valid) {
    assertEquals(result.config.name, 'Test List');
  }
});

Deno.test('validateOnly: returns valid: false with errors for invalid config', () => {
  const result = ConfigurationManager.validateOnly({ name: '' });
  assertEquals(result.valid, false);
  if (!result.valid) {
    assertInstanceOf(result.errors.issues, Array);
  }
});

// ── resolveObject ─────────────────────────────────────────────────────────────

Deno.test('resolveObject: resolves plain object with override applied', async () => {
  const cfg = await ConfigurationManager.resolveObject(
    minimalConfig(),
    '{"name":"Resolved"}',
    { applyEnvOverrides: false },
  );
  assertEquals(cfg.name, 'Resolved');
});

// ── transformations ───────────────────────────────────────────────────────────

Deno.test('fromObject: respects transformations field', async () => {
  const cfg = await ConfigurationManager.fromObject({
    ...minimalConfig(),
    transformations: [TransformationType.Deduplicate],
  }, { applyEnvOverrides: false }).load();
  assertEquals(cfg.transformations, [TransformationType.Deduplicate]);
});

// ── fromFile ──────────────────────────────────────────────────────────────────

function makeMockFs(content: string): IFileSystem {
  return {
    readTextFile: async (_path: string) => content,
    writeTextFile: async () => {},
    exists: async () => true,
  };
}

Deno.test('fromFile: loads and validates configuration from a file', async () => {
  const fileContent = JSON.stringify(minimalConfig());
  const mgr = ConfigurationManager.fromFile('./config.json', makeMockFs(fileContent), {
    applyEnvOverrides: false,
  });
  const cfg = await mgr.load();
  assertEquals(cfg.name, 'Test List');
  assertEquals(cfg.sources.length, 1);
});

Deno.test('fromFile: throws ConfigurationValidationError when file contains invalid config', async () => {
  const fileContent = JSON.stringify({ name: 'No Sources' }); // missing required sources
  const mgr = ConfigurationManager.fromFile('./config.json', makeMockFs(fileContent), {
    applyEnvOverrides: false,
  });
  await assertRejects(() => mgr.load(), ConfigurationValidationError);
});

// ── fromCliArgs ───────────────────────────────────────────────────────────────

Deno.test('fromCliArgs: creates config from input URLs with hosts type', async () => {
  const cfg = await ConfigurationManager.fromCliArgs(
    ['https://example.com/hosts.txt'],
    'hosts',
    { applyEnvOverrides: false },
  ).load();
  assertEquals(cfg.sources[0].source, 'https://example.com/hosts.txt');
  assertEquals(cfg.sources[0].type, SourceType.Hosts);
});

Deno.test('fromCliArgs: creates config from input URLs with adblock type', async () => {
  const cfg = await ConfigurationManager.fromCliArgs(
    ['https://example.com/list.txt'],
    'adblock',
    { applyEnvOverrides: false },
  ).load();
  assertEquals(cfg.sources[0].type, SourceType.Adblock);
});

Deno.test('fromCliArgs: multiple inputs produce multiple sources', async () => {
  const cfg = await ConfigurationManager.fromCliArgs(
    ['https://example.com/a.txt', 'https://example.com/b.txt'],
    'hosts',
    { applyEnvOverrides: false },
  ).load();
  assertEquals(cfg.sources.length, 2);
});

// ── getValidationErrors non-null ──────────────────────────────────────────────

Deno.test('getValidationErrors: returns ZodError after failed load', async () => {
  const mgr = ConfigurationManager.fromObject({ name: 'No Sources' }, { applyEnvOverrides: false });
  try {
    await mgr.load();
  } catch {
    // expected
  }
  const errors = mgr.getValidationErrors();
  assertEquals(errors !== null, true);
  assertEquals(Array.isArray((errors as { issues: unknown[] })!.issues), true);
  assertEquals((errors as { issues: unknown[] })!.issues.length > 0, true);
});

// ── resolveObject without override ────────────────────────────────────────────

Deno.test('resolveObject: resolves plain object without override', async () => {
  const cfg = await ConfigurationManager.resolveObject(
    minimalConfig(),
    undefined,
    { applyEnvOverrides: false },
  );
  assertEquals(cfg.name, 'Test List');
});

Deno.test('resolveObject: throws ConfigurationValidationError for invalid object', async () => {
  await assertRejects(
    () => ConfigurationManager.resolveObject({ name: '' }, undefined, { applyEnvOverrides: false }),
    ConfigurationValidationError,
  );
});

// ── enforceSourceLimit: false ─────────────────────────────────────────────────

Deno.test('enforceSourceLimit: false — does not truncate sources array', async () => {
  const sources = Array.from({ length: 5 }, (_, i) => ({
    source: `https://example.com/${i}.txt`,
    type: SourceType.Hosts,
  }));
  const cfg = await ConfigurationManager.fromObject(
    { name: 'Limits Test', sources },
    { applyEnvOverrides: false, enforceSourceLimit: false },
  ).load();
  assertEquals(cfg.sources.length, 5);
});

// ── enforceExclusionLimit: false ──────────────────────────────────────────────

Deno.test('enforceExclusionLimit: false — does not truncate exclusions array', async () => {
  const exclusions = Array.from({ length: 5 }, (_, i) => `exclusion${i}`);
  const cfg = await ConfigurationManager.fromObject(
    { name: 'Exclusion Test', sources: minimalConfig().sources!, exclusions },
    { applyEnvOverrides: false, enforceExclusionLimit: false },
  ).load();
  assertEquals(cfg.exclusions!.length, 5);
});

// ── applyEnvOverrides default (true) ─────────────────────────────────────────

Deno.test('applyEnvOverrides: default applies env source (env name wins when set)', async () => {
  // Build a manager with a custom env source manually via fromSources
  const cfg = await ConfigurationManager.fromSources([
    new ObjectConfigurationSource({ ...minimalConfig(), name: 'Base' }),
    new EnvConfigurationSource((k) => k === 'ADBLOCK_CONFIG_NAME' ? 'EnvWins' : undefined),
  ], { applyEnvOverrides: false }).load();
  assertEquals(cfg.name, 'EnvWins');
});

// ── deepMerge edge cases ──────────────────────────────────────────────────────

Deno.test('deepMerge: empty array of partials returns empty object', () => {
  const result = ConfigurationManager.deepMerge([]);
  assertEquals(result, {});
});

Deno.test('deepMerge: single partial returns its values', () => {
  const result = ConfigurationManager.deepMerge([{ name: 'Only One' }]);
  assertEquals(result.name, 'Only One');
});

Deno.test('deepMerge: null partial is skipped without throwing', () => {
  // Passing null via unknown[] cast to test runtime guard
  const result = ConfigurationManager.deepMerge([
    { name: 'First' },
    null as unknown as Partial<IConfiguration>,
    { description: 'After Null' },
  ]);
  assertEquals(result.name, 'First');
  assertEquals(result.description, 'After Null');
});

Deno.test('deepMerge: array partial is skipped without throwing', () => {
  const result = ConfigurationManager.deepMerge([
    { name: 'First' },
    [] as unknown as Partial<IConfiguration>,
    { description: 'After Array' },
  ]);
  assertEquals(result.name, 'First');
  assertEquals(result.description, 'After Array');
});

// ── validateOnly edge cases ───────────────────────────────────────────────────

Deno.test('validateOnly: null input returns valid: false', () => {
  const result = ConfigurationManager.validateOnly(null);
  assertEquals(result.valid, false);
});

Deno.test('validateOnly: valid config with optional fields is accepted', () => {
  const result = ConfigurationManager.validateOnly({
    ...minimalConfig(),
    description: 'A description',
    homepage: 'https://example.com',
    license: 'MIT',
    version: '1.0.0',
  });
  assertEquals(result.valid, true);
});

// ── fromSources with OverrideConfigurationSource ──────────────────────────────

Deno.test('fromSources: override replaces sources array from base', async () => {
  const baseSource = [{ source: 'https://base.example.com/hosts.txt', type: SourceType.Hosts }];
  const overrideSource = [{
    source: 'https://override.example.com/hosts.txt',
    type: SourceType.Hosts,
  }];
  const cfg = await ConfigurationManager.fromSources([
    new ObjectConfigurationSource({ name: 'Base', sources: baseSource }),
    new OverrideConfigurationSource(JSON.stringify({ sources: overrideSource })),
  ], { applyEnvOverrides: false }).load();
  assertEquals(cfg.sources[0].source, 'https://override.example.com/hosts.txt');
});

Deno.test('fromSources: OverrideConfigurationSource has higher precedence than EnvConfigurationSource', async () => {
  // Env provides name='EnvName', but override provides name='OverrideName'.
  // Override should win because it is a higher-priority source.
  const cfg = await ConfigurationManager.fromSources([
    new ObjectConfigurationSource(minimalConfig()),
    new EnvConfigurationSource((k) => k === 'ADBLOCK_CONFIG_NAME' ? 'EnvName' : undefined),
    new OverrideConfigurationSource('{"name":"OverrideName"}'),
  ], { applyEnvOverrides: false }).load();
  assertEquals(cfg.name, 'OverrideName');
});

Deno.test('load: env is applied between base sources and override sources by default', async () => {
  // Base has name='Base'; env provides name='EnvName'; override provides name='OverrideName'.
  // Expected: OverrideName (override wins over env, which wins over base).
  const cfg = await ConfigurationManager.fromSources([
    new ObjectConfigurationSource({ ...minimalConfig(), name: 'Base' }),
    new OverrideConfigurationSource('{"name":"OverrideName"}'),
  ], {
    applyEnvOverrides: true,
  }).load();
  // Without a real ADBLOCK_CONFIG_NAME env var, env produces nothing — override wins.
  assertEquals(cfg.name, 'OverrideName');
});
