import { assertEquals, assertExists, assertRejects } from '@std/assert';
import { fromFileUrl } from '@std/path';
import { BrowserSyntaxCompiler } from './BrowserSyntaxCompiler.ts';
import type { IConfiguration } from '../../types/index.ts';
import { TransformationType } from '../../types/index.ts';
import { silentLogger } from '../../utils/index.ts';

const testResourcesDir = fromFileUrl(new URL('../../resources/', import.meta.url));
const browserRulesFilePath = `${testResourcesDir}browser-rules.txt`;

function createTestConfig(overrides: Partial<IConfiguration> = {}): IConfiguration {
  return {
    name: 'Test Browser Filter List',
    sources: [
      {
        source: browserRulesFilePath,
        engine: 'browser',
      },
    ],
    ...overrides,
  };
}

Deno.test('BrowserSyntaxCompiler - should create instance with default logger', () => {
  const compiler = new BrowserSyntaxCompiler();
  assertExists(compiler);
});

Deno.test('BrowserSyntaxCompiler - should compile a minimal browser-syntax configuration', async () => {
  const compiler = new BrowserSyntaxCompiler({ logger: silentLogger });
  const config = createTestConfig();

  const rules = await compiler.compile(config);
  assertExists(rules);
  assertEquals(Array.isArray(rules), true);
  assertEquals(rules.some((line) => line.includes('Title: Test Browser Filter List')), true);
});

Deno.test('BrowserSyntaxCompiler - preserves cosmetic and network browser rules', async () => {
  const compiler = new BrowserSyntaxCompiler({ logger: silentLogger });
  const config = createTestConfig();

  const rules = await compiler.compile(config);
  assertEquals(rules.some((line) => line === '||example.com^$script,third-party'), true);
  assertEquals(rules.some((line) => line === 'example.com##.ad-banner'), true);
  assertEquals(rules.some((line) => line === 'tracker.com#@#.allowed-banner'), true);
});

Deno.test('BrowserSyntaxCompiler - default pipeline deduplicates rules', async () => {
  const compiler = new BrowserSyntaxCompiler({ logger: silentLogger });
  const config = createTestConfig();

  const rules = await compiler.compile(config);
  const occurrences = rules.filter((line) => line === '||example.com^$script,third-party').length;
  assertEquals(occurrences, 1);
});

Deno.test('BrowserSyntaxCompiler - default pipeline strips comment lines', async () => {
  const compiler = new BrowserSyntaxCompiler({ logger: silentLogger });
  const config = createTestConfig();

  const rules = await compiler.compile(config);
  assertEquals(rules.some((line) => line === '! Test browser-syntax rules'), false);
});

Deno.test('BrowserSyntaxCompiler - rejects DNS-only transformations (global)', async () => {
  const compiler = new BrowserSyntaxCompiler({ logger: silentLogger });
  const config = createTestConfig({
    transformations: [TransformationType.Compress],
  });

  await assertRejects(() => compiler.compile(config), Error, 'Compress');
});

Deno.test('BrowserSyntaxCompiler - rejects DNS-only transformations (per-source)', async () => {
  const compiler = new BrowserSyntaxCompiler({ logger: silentLogger });
  const config = createTestConfig();
  config.sources[0].transformations = [TransformationType.Validate];

  await assertRejects(() => compiler.compile(config), Error, 'Validate');
});

Deno.test('BrowserSyntaxCompiler - compileWithMetrics returns metrics when benchmarking', async () => {
  const compiler = new BrowserSyntaxCompiler({ logger: silentLogger });
  const config = createTestConfig();

  const result = await compiler.compileWithMetrics(config, true);
  assertExists(result.metrics);
  assertExists(result.rules);
});
