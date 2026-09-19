import { assertEquals, assertExists, assertFalse, assertRejects } from '@std/assert';
import { fromFileUrl } from '@std/path';
import { BROWSER_SAFE_TRANSFORMATIONS, BrowserSyntaxCompiler } from './BrowserSyntaxCompiler.ts';
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

Deno.test('BrowserSyntaxCompiler - rejects commercial-only transformations that silently no-op (issue #502)', async () => {
  // ConflictDetection/RuleOptimizer used to be listed in BROWSER_SAFE_TRANSFORMATIONS
  // even though TransformationRegistry never registers them in this OSS package -
  // meaning they'd pass this "safe" check and then silently no-op in
  // TransformationPipeline.transform(). Both are now rejected before compilation -
  // by ConfigurationValidator (which BrowserSyntaxCompiler.compile() runs first), so
  // it never reaches assertBrowserSafeTransformations at all in practice, but that
  // function's own allowlist must stay correct as defense in depth.
  const compiler = new BrowserSyntaxCompiler({ logger: silentLogger });

  const globalConfig = createTestConfig({
    transformations: [TransformationType.ConflictDetection],
  });
  await assertRejects(() => compiler.compile(globalConfig), Error);

  const sourceConfig = createTestConfig();
  sourceConfig.sources[0].transformations = [TransformationType.RuleOptimizer];
  await assertRejects(() => compiler.compile(sourceConfig), Error);
});

Deno.test('BROWSER_SAFE_TRANSFORMATIONS - excludes unregistered commercial-only transformations (issue #502)', () => {
  assertFalse(BROWSER_SAFE_TRANSFORMATIONS.has(TransformationType.ConflictDetection));
  assertFalse(BROWSER_SAFE_TRANSFORMATIONS.has(TransformationType.RuleOptimizer));
});

Deno.test('BrowserSyntaxCompiler - compileWithMetrics returns metrics when benchmarking', async () => {
  const compiler = new BrowserSyntaxCompiler({ logger: silentLogger });
  const config = createTestConfig();

  const result = await compiler.compileWithMetrics(config, true);
  assertExists(result.metrics);
  assertExists(result.rules);
});
