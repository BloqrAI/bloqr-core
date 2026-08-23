import { assertEquals } from '@std/assert';
import { SourceType } from '../types/index.ts';
import {
  classifyLine,
  detectEngineFromLines,
  detectSourceEngine,
  groupSourcesByEngine,
} from './EngineDetector.ts';

Deno.test('classifyLine - cosmetic rules are browser signals', () => {
  assertEquals(classifyLine('example.com##.ad-banner'), 'browser');
  assertEquals(classifyLine('example.com#@#.ad-banner'), 'browser');
  assertEquals(classifyLine('example.com#?#.ad[data-src]'), 'browser');
});

Deno.test('classifyLine - hosts syntax is a dns signal', () => {
  assertEquals(classifyLine('0.0.0.0 example.com'), 'dns');
  assertEquals(classifyLine('127.0.0.1 tracker.example.com'), 'dns');
});

Deno.test('classifyLine - bare domain and ||domain^ are dns signals', () => {
  assertEquals(classifyLine('example.com'), 'dns');
  assertEquals(classifyLine('||example.com^'), 'dns');
  assertEquals(classifyLine('@@||example.com^'), 'dns');
});

Deno.test('classifyLine - network rule with browser-only modifiers is a browser signal', () => {
  assertEquals(classifyLine('||example.com^$script,third-party'), 'browser');
  assertEquals(classifyLine('||example.com^$csp=default-src *.example.com'), 'browser');
  assertEquals(classifyLine('||example.com^$redirect=noopjs'), 'browser');
});

Deno.test('classifyLine - network rule with only dns-relevant modifiers is a dns signal', () => {
  assertEquals(classifyLine('||example.com^$important'), 'dns');
});

Deno.test('classifyLine - blank line has no signal', () => {
  assertEquals(classifyLine(''), null);
  assertEquals(classifyLine('   '), null);
});

Deno.test('detectEngineFromLines - majority vote picks browser', () => {
  const lines = [
    '! a comment',
    'example.com##.ad',
    'tracker.com#@#.banner',
    'example.com',
  ];
  assertEquals(detectEngineFromLines(lines), 'browser');
});

Deno.test('detectEngineFromLines - majority vote picks dns', () => {
  const lines = [
    '0.0.0.0 example.com',
    '0.0.0.0 tracker.com',
    'ads.example.com##.banner',
  ];
  assertEquals(detectEngineFromLines(lines), 'dns');
});

Deno.test('detectEngineFromLines - empty/ambiguous sample falls back to dns by default', () => {
  assertEquals(detectEngineFromLines([]), 'dns');
  assertEquals(detectEngineFromLines(['! only comments', '']), 'dns');
});

Deno.test('detectEngineFromLines - tied votes fall back to dns', () => {
  const lines = [
    'example.com##.ad',
    '0.0.0.0 tracker.com',
  ];
  assertEquals(detectEngineFromLines(lines), 'dns');
});

Deno.test('detectEngineFromLines - custom fallback is respected', () => {
  assertEquals(detectEngineFromLines([], { fallback: 'browser' }), 'browser');
});

Deno.test('detectSourceEngine - explicit source.engine always wins', () => {
  const engine = detectSourceEngine(
    { engine: 'browser', type: SourceType.Hosts },
    ['0.0.0.0 example.com'],
  );
  assertEquals(engine, 'browser');
});

Deno.test('detectSourceEngine - hosts type forces dns even with browser-looking lines', () => {
  const engine = detectSourceEngine(
    { type: SourceType.Hosts },
    ['example.com##.ad-banner'],
  );
  assertEquals(engine, 'dns');
});

Deno.test('detectSourceEngine - sniffs from content when no declarative signal is present', () => {
  const engine = detectSourceEngine({}, [
    'example.com##.ad',
    'tracker.com#@#.banner',
  ]);
  assertEquals(engine, 'browser');
});

Deno.test('detectSourceEngine - falls back to configDefaultEngine when sample is ambiguous', () => {
  const engine = detectSourceEngine({}, [], 'browser');
  assertEquals(engine, 'browser');
});

Deno.test('detectSourceEngine - defaults to dns with no signals at all', () => {
  assertEquals(detectSourceEngine({}), 'dns');
});

Deno.test('groupSourcesByEngine - buckets by declarative signals, preserves order', () => {
  const grouped = groupSourcesByEngine([
    { source: 'a', engine: 'browser' },
    { source: 'b' },
    { source: 'c', type: SourceType.Hosts },
    { source: 'd', engine: 'browser' },
  ]);
  assertEquals(grouped.browser.map((s) => s.source), ['a', 'd']);
  assertEquals(grouped.dns.map((s) => s.source), ['b', 'c']);
});

Deno.test('groupSourcesByEngine - uses configDefaultEngine for undeclared sources', () => {
  const grouped = groupSourcesByEngine(
    [{ source: 'a' }, { source: 'b', type: SourceType.Hosts }],
    'browser',
  );
  assertEquals(grouped.browser.map((s) => s.source), ['a']);
  assertEquals(grouped.dns.map((s) => s.source), ['b']);
});
