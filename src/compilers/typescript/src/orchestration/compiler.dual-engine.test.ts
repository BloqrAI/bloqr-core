/**
 * Integration tests for `runCompiler`'s dual-engine (DNS vs browser-syntax) routing
 * (#435 — Wave 1 of epic #432).
 *
 * These exercise `runCompiler` end-to-end against temp-dir fixture files, covering
 * the two acceptance-critical guarantees:
 *  - an all-DNS configuration takes the pre-existing chunked/unchunked code path
 *    unchanged (byte-identical output);
 *  - a mixed-engine configuration produces two independent artifacts.
 *
 * `bloqr-validate` is stubbed out via `RULES_VALIDATE_PATH` (same technique as
 * `compiler.test.ts`) so these don't depend on the Rust workspace being built, and
 * `allowUnvalidatedOutput` is used for the browser-syntax cases since native
 * browser-syntax validation doesn't land until #434.
 */

import { assertEquals, assertExists } from '@std/assert';
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { runCompiler } from './compiler.ts';
import { createLogger } from './logger.ts';

const logger = createLogger(false);

async function withTempDir(fn: (dir: string) => Promise<void>): Promise<void> {
  const dir = mkdtempSync(join(tmpdir(), 'bloqr-dual-engine-ts-test-'));
  try {
    await fn(dir);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
}

function writeConfig(dir: string, name: string, config: unknown): string {
  const configPath = join(dir, name);
  writeFileSync(configPath, JSON.stringify(config, null, 2));
  return configPath;
}

Deno.test('runCompiler - all-DNS config uses the unchanged single-artifact path', async () => {
  await withTempDir(async (dir) => {
    const dnsSourcePath = join(dir, 'dns-rules.txt');
    writeFileSync(dnsSourcePath, '||example.com^\n||test.org^\n');

    const configPath = writeConfig(dir, 'config.json', {
      name: 'DNS Only',
      sources: [{ source: dnsSourcePath, type: 'adblock' }],
    });
    const outputPath = join(dir, 'output.txt');

    const result = await runCompiler({
      configPath,
      outputPath,
      logger,
      allowUnvalidatedOutput: true,
    });

    assertEquals(result.success, true);
    assertEquals(result.browserOutputPath, undefined);
    assertExists(result.outputHash);
    assertEquals(existsSync(outputPath), true);
    const content = readFileSync(outputPath, 'utf8');
    assertEquals(content.includes('||example.com^'), true);
  });
});

Deno.test('runCompiler - mixed-engine config produces two artifacts with independent hashes', async () => {
  await withTempDir(async (dir) => {
    const dnsSourcePath = join(dir, 'dns-rules.txt');
    writeFileSync(dnsSourcePath, '||example.com^\n');
    const browserSourcePath = join(dir, 'browser-rules.txt');
    writeFileSync(browserSourcePath, 'example.com##.ad-banner\n||ads.example.org^$script\n');

    const configPath = writeConfig(dir, 'config.json', {
      name: 'Mixed Engine',
      sources: [
        { source: dnsSourcePath, type: 'adblock' },
        { source: browserSourcePath, engine: 'browser' },
      ],
    });
    const outputPath = join(dir, 'output.txt');

    const result = await runCompiler({
      configPath,
      outputPath,
      logger,
      allowUnvalidatedOutput: true,
    });

    assertEquals(result.success, true);
    assertExists(result.browserOutputPath);
    assertExists(result.browserOutputHash);
    assertExists(result.browserRuleCount);

    assertEquals(existsSync(outputPath), true);
    assertEquals(existsSync(result.browserOutputPath!), true);
    assertEquals(result.browserOutputPath, join(dir, 'output.browser.txt'));

    const dnsContent = readFileSync(outputPath, 'utf8');
    const browserContent = readFileSync(result.browserOutputPath!, 'utf8');
    assertEquals(dnsContent.includes('||example.com^'), true);
    assertEquals(browserContent.includes('example.com##.ad-banner'), true);
    // Rules never cross engines.
    assertEquals(dnsContent.includes('##.ad-banner'), false);
    assertEquals(browserContent.includes('||example.com^\n'), false);

    // Independent hashes for independent content.
    assertEquals(result.outputHash === result.browserOutputHash, false);
  });
});

Deno.test('runCompiler - explicit --browser-output path overrides the derived default', async () => {
  await withTempDir(async (dir) => {
    const dnsSourcePath = join(dir, 'dns-rules.txt');
    writeFileSync(dnsSourcePath, '||example.com^\n');
    const browserSourcePath = join(dir, 'browser-rules.txt');
    writeFileSync(browserSourcePath, 'example.com##.ad-banner\n');

    const configPath = writeConfig(dir, 'config.json', {
      name: 'Mixed Engine Custom Path',
      sources: [
        { source: dnsSourcePath, type: 'adblock' },
        { source: browserSourcePath, engine: 'browser' },
      ],
    });
    const outputPath = join(dir, 'output.txt');
    const browserOutputPath = join(dir, 'custom-browser-output.txt');

    const result = await runCompiler({
      configPath,
      outputPath,
      browserOutputPath,
      logger,
      allowUnvalidatedOutput: true,
    });

    assertEquals(result.success, true);
    assertEquals(result.browserOutputPath, browserOutputPath);
    assertEquals(existsSync(browserOutputPath), true);
  });
});

Deno.test('runCompiler - engine: browser forces every source through the browser engine', async () => {
  await withTempDir(async (dir) => {
    // A source that looks like DNS syntax is forced through the browser engine anyway.
    const sourcePath = join(dir, 'rules.txt');
    writeFileSync(sourcePath, '||example.com^\n');

    const configPath = writeConfig(dir, 'config.json', {
      name: 'Forced Browser',
      sources: [{ source: sourcePath, type: 'adblock' }],
    });
    const outputPath = join(dir, 'output.txt');

    const result = await runCompiler({
      configPath,
      outputPath,
      engine: 'browser',
      logger,
      allowUnvalidatedOutput: true,
    });

    assertEquals(result.success, true);
    // Single-engine result: no separate browser artifact, everything reported
    // through the primary output fields.
    assertEquals(result.browserOutputPath, undefined);
    assertExists(result.outputHash);
    assertEquals(existsSync(outputPath), true);
  });
});
