import { assertEquals, assertExists } from '@std/assert';
import { fromFileUrl } from '@std/path';
import { MultiEngineCompiler } from './MultiEngineCompiler.ts';
import type { IConfiguration } from '../types/index.ts';
import { SourceType, TransformationType } from '../types/index.ts';
import { silentLogger } from '../utils/index.ts';

const testResourcesDir = fromFileUrl(new URL('../resources/', import.meta.url));
const dnsRulesFilePath = `${testResourcesDir}rules.txt`;
const browserRulesFilePath = `${testResourcesDir}browser-rules.txt`;

function createMixedConfig(): IConfiguration {
  return {
    name: 'Mixed Engine List',
    sources: [
      { source: dnsRulesFilePath, type: SourceType.Adblock },
      { source: browserRulesFilePath, engine: 'browser' },
    ],
  };
}

function createDnsOnlyConfig(): IConfiguration {
  return {
    name: 'DNS Only List',
    sources: [{ source: dnsRulesFilePath, type: SourceType.Adblock }],
  };
}

function createBrowserOnlyConfig(): IConfiguration {
  return {
    name: 'Browser Only List',
    sources: [{ source: browserRulesFilePath, engine: 'browser' }],
  };
}

Deno.test('MultiEngineCompiler - mixed config produces both dns and browser results', async () => {
  const compiler = new MultiEngineCompiler({
    filterCompilerOptions: { logger: silentLogger },
    browserSyntaxCompilerOptions: { logger: silentLogger },
  });

  const result = await compiler.compile(createMixedConfig());
  assertExists(result.dns);
  assertExists(result.browser);
  assertEquals(result.dns!.rules.some((l) => l === '||example.com^'), true);
  assertEquals(result.browser!.rules.some((l) => l === 'example.com##.ad-banner'), true);
});

Deno.test('MultiEngineCompiler - dns-only config produces only a dns result', async () => {
  const compiler = new MultiEngineCompiler({
    filterCompilerOptions: { logger: silentLogger },
    browserSyntaxCompilerOptions: { logger: silentLogger },
  });

  const result = await compiler.compile(createDnsOnlyConfig());
  assertExists(result.dns);
  assertEquals(result.browser, undefined);
});

Deno.test('MultiEngineCompiler - browser-only config produces only a browser result', async () => {
  const compiler = new MultiEngineCompiler({
    filterCompilerOptions: { logger: silentLogger },
    browserSyntaxCompilerOptions: { logger: silentLogger },
  });

  const result = await compiler.compile(createBrowserOnlyConfig());
  assertEquals(result.dns, undefined);
  assertExists(result.browser);
});

Deno.test('MultiEngineCompiler - forceEngine routes every source through one engine', async () => {
  const compiler = new MultiEngineCompiler({
    filterCompilerOptions: { logger: silentLogger },
    browserSyntaxCompilerOptions: { logger: silentLogger },
    forceEngine: 'dns',
  });

  // Even the browser-tagged source is forced through the dns engine.
  const result = await compiler.compile(createMixedConfig());
  assertExists(result.dns);
  assertEquals(result.browser, undefined);
});

Deno.test('MultiEngineCompiler - a shared top-level transformations list with DNS-only entries does not break the browser bucket', async () => {
  // Regression: CLI callers (and DNS-authored configs in general) commonly set a
  // top-level `transformations` list containing DNS-only transformations like
  // Compress/Validate. The browser bucket must silently drop those rather than
  // failing the whole compilation.
  const compiler = new MultiEngineCompiler({
    filterCompilerOptions: { logger: silentLogger },
    browserSyntaxCompilerOptions: { logger: silentLogger },
  });

  const config = createMixedConfig();
  config.transformations = [
    TransformationType.RemoveComments,
    TransformationType.Deduplicate,
    TransformationType.Compress,
    TransformationType.Validate,
    TransformationType.TrimLines,
    TransformationType.InsertFinalNewLine,
  ];

  const result = await compiler.compile(config);
  assertExists(result.dns);
  assertExists(result.browser);
  assertEquals(result.browser!.rules.some((l) => l === 'example.com##.ad-banner'), true);
});

Deno.test('MultiEngineCompiler - non-header output lines never overlap between engines', async () => {
  const compiler = new MultiEngineCompiler({
    filterCompilerOptions: { logger: silentLogger },
    browserSyntaxCompilerOptions: { logger: silentLogger },
  });

  const result = await compiler.compile(createMixedConfig());
  const nonHeaderLines = (rules: string[]) =>
    rules.filter((r) => r.trim() !== '' && !r.startsWith('!'));

  const dnsRuleLines = new Set(nonHeaderLines(result.dns!.rules));
  const browserRuleLines = new Set(nonHeaderLines(result.browser!.rules));
  const overlap = [...dnsRuleLines].filter((r) => browserRuleLines.has(r));
  assertEquals(overlap.length, 0);
});

Deno.test('MultiEngineCompiler - all-DNS config produces byte-identical output to FilterCompiler directly', async () => {
  // The load-bearing backward-compatibility guarantee: every existing (100% DNS)
  // config must produce the exact same output via MultiEngineCompiler as it did
  // (and still does) via a bare FilterCompiler. `! Last modified:` / `! Checksum:`
  // lines are excluded from the comparison since they're timestamp-derived and
  // legitimately differ between two independently-timed compile runs of the same
  // config — not a sign of divergent compilation logic.
  const { FilterCompiler } = await import('../compiler/FilterCompiler.ts');
  const config = createDnsOnlyConfig();
  const stripNonDeterministic = (rules: string[]) =>
    rules.filter((l) => !l.startsWith('! Last modified:') && !l.startsWith('! Checksum:'));

  const directResult = await new FilterCompiler({ logger: silentLogger }).compile(config);
  const multiResult = await new MultiEngineCompiler({
    filterCompilerOptions: { logger: silentLogger },
    browserSyntaxCompilerOptions: { logger: silentLogger },
  }).compile(config);

  assertExists(multiResult.dns);
  assertEquals(stripNonDeterministic(multiResult.dns!.rules), stripNonDeterministic(directResult));
  assertEquals(multiResult.dns!.rules.length, directResult.length);
  assertEquals(multiResult.browser, undefined);
});
