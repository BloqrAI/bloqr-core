import { assertEquals, assertStrictEquals } from '@std/assert';
import { ConflictDetectionTransformation } from './ConflictDetectionTransformation.ts';
import type { ILogger } from '../types/index.ts';

/** Minimal logger that records warn() calls so tests can assert detection actually fired. */
function createRecordingLogger(): { logger: ILogger; warnings: string[] } {
  const warnings: string[] = [];
  const logger: ILogger = {
    info: () => {},
    warn: (message: string) => warnings.push(message),
    error: () => {},
    debug: () => {},
    trace: () => {},
  };
  return { logger, warnings };
}

Deno.test('ConflictDetectionTransformation - detects a network allow/block conflict', async () => {
  const { logger, warnings } = createRecordingLogger();
  const transformation = new ConflictDetectionTransformation(logger);
  const rules = ['||example.com^', '@@||example.com^'];

  const result = await transformation.execute(rules);

  assertEquals(result, rules);
  assertEquals(warnings.length, 1);
});

Deno.test('ConflictDetectionTransformation - detects a cosmetic exception conflict', async () => {
  const { logger, warnings } = createRecordingLogger();
  const transformation = new ConflictDetectionTransformation(logger);
  const rules = ['example.com##.ad-banner', 'example.com#@#.ad-banner'];

  const result = await transformation.execute(rules);

  assertEquals(result, rules);
  assertEquals(warnings.length, 1);
});

Deno.test('ConflictDetectionTransformation - never mutates the input rule set', async () => {
  const { logger } = createRecordingLogger();
  const transformation = new ConflictDetectionTransformation(logger);
  const rules = [
    '! comment',
    '||example.com^',
    '@@||other.com^',
    'example.com##.ad',
  ];

  const result = await transformation.execute(rules);

  assertEquals(result, rules);
  assertStrictEquals(result, rules);
});

Deno.test('ConflictDetectionTransformation - ignores comments and empty lines', async () => {
  const { logger, warnings } = createRecordingLogger();
  const transformation = new ConflictDetectionTransformation(logger);
  const rules = ['! comment', '', '||example.com^'];

  const result = await transformation.execute(rules);

  assertEquals(result, rules);
  assertEquals(warnings.length, 0);
});

Deno.test('ConflictDetectionTransformation - does not flag an allow rule with no matching block rule', async () => {
  const { logger, warnings } = createRecordingLogger();
  const transformation = new ConflictDetectionTransformation(logger);
  const rules = ['@@||example.com^', '||other.com^'];

  const result = await transformation.execute(rules);

  assertEquals(result, rules);
  assertEquals(warnings.length, 0);
});

Deno.test('ConflictDetectionTransformation - detects a conflict even without TrimLines run first', async () => {
  // Regression: classification (isAllowRule) trims internally, but the old
  // slice(2) ran on the untrimmed string, so " @@||example.com^" was
  // classified as an allow rule yet converted to "@||example.com^" instead of
  // "||example.com^" - missing a real conflict whenever this transformation
  // runs without TrimLines requested alongside it.
  const { logger, warnings } = createRecordingLogger();
  const transformation = new ConflictDetectionTransformation(logger);
  const rules = ['||example.com^', ' @@||example.com^ '];

  const result = await transformation.execute(rules);

  assertEquals(result, rules);
  assertEquals(warnings.length, 1);
});

Deno.test('ConflictDetectionTransformation - classifies a network exception rule before checking for a cosmetic marker', async () => {
  // Regression: RuleUtils.isCosmeticRule() matches '#@#' anywhere in the rule
  // text, so a network exception rule whose regex pattern happens to contain
  // that substring (not as a cosmetic marker) was misclassified as cosmetic
  // instead of as the network exception it actually is, via toBlockingForm()
  // checking isCosmeticRule() before the explicit '@@' prefix.
  const { logger, warnings } = createRecordingLogger();
  const transformation = new ConflictDetectionTransformation(logger);
  const rules = ['/banner#@#ad/', '@@/banner#@#ad/'];

  const result = await transformation.execute(rules);

  assertEquals(result, rules);
  assertEquals(warnings.length, 1);
});
