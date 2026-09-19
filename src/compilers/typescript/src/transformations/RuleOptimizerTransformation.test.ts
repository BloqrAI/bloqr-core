import { assertEquals } from '@std/assert';
import { RuleOptimizerTransformation } from './RuleOptimizerTransformation.ts';
import { silentLogger } from '../utils/logger.ts';

Deno.test('RuleOptimizerTransformation - merges plain hiding rules sharing a domain list', async () => {
  const transformation = new RuleOptimizerTransformation(silentLogger);
  const rules = ['example.com##.ad-banner', 'example.com##.ad-sidebar'];

  const result = await transformation.execute(rules);

  assertEquals(result, ['example.com##.ad-banner, .ad-sidebar']);
});

Deno.test('RuleOptimizerTransformation - merges three or more rules for the same domain', async () => {
  const transformation = new RuleOptimizerTransformation(silentLogger);
  const rules = [
    'example.com##.a',
    'example.com##.b',
    'example.com##.c',
  ];

  const result = await transformation.execute(rules);

  assertEquals(result, ['example.com##.a, .b, .c']);
});

Deno.test('RuleOptimizerTransformation - does not merge rules with different domain lists', async () => {
  const transformation = new RuleOptimizerTransformation(silentLogger);
  const rules = ['example.com##.ad', 'other.com##.ad'];

  const result = await transformation.execute(rules);

  assertEquals(result, rules);
});

Deno.test('RuleOptimizerTransformation - drops an exact duplicate selector for the same domain', async () => {
  const transformation = new RuleOptimizerTransformation(silentLogger);
  const rules = ['example.com##.ad', 'example.com##.ad'];

  const result = await transformation.execute(rules);

  assertEquals(result, ['example.com##.ad']);
});

Deno.test('RuleOptimizerTransformation - preserves non-adjacent order and unrelated rules', async () => {
  const transformation = new RuleOptimizerTransformation(silentLogger);
  const rules = [
    '! header comment',
    'example.com##.a',
    '||tracker.com^',
    'example.com##.b',
  ];

  const result = await transformation.execute(rules);

  assertEquals(result, [
    '! header comment',
    'example.com##.a, .b',
    '||tracker.com^',
  ]);
});

Deno.test('RuleOptimizerTransformation - never merges scriptlet injection rules', async () => {
  const transformation = new RuleOptimizerTransformation(silentLogger);
  const rules = [
    'example.com##+js(set-constant, foo, false)',
    'example.com##+js(set-constant, bar, false)',
  ];

  const result = await transformation.execute(rules);

  assertEquals(result, rules);
});

Deno.test('RuleOptimizerTransformation - never merges exception cosmetic rules', async () => {
  const transformation = new RuleOptimizerTransformation(silentLogger);
  const rules = ['example.com#@#.ad', 'example.com#@#.banner'];

  const result = await transformation.execute(rules);

  assertEquals(result, rules);
});

Deno.test('RuleOptimizerTransformation - never merges network rules', async () => {
  const transformation = new RuleOptimizerTransformation(silentLogger);
  const rules = ['||example.com^', '||example.com^$important'];

  const result = await transformation.execute(rules);

  assertEquals(result, rules);
});

Deno.test('RuleOptimizerTransformation - leaves an empty rule set unchanged', async () => {
  const transformation = new RuleOptimizerTransformation(silentLogger);

  const result = await transformation.execute([]);

  assertEquals(result, []);
});
