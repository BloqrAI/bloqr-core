/**
 * Tests for chunking functionality
 * Validates chunk splitting and merging logic
 */

import { assertEquals } from '@std/assert';
import type { IConfiguration } from '../index.ts';
import {
  DEFAULT_CHUNKING_CONFIG,
  mergeChunks,
  shouldEnableChunking,
  splitIntoChunks,
} from './chunking.ts';
import { createLogger } from './logger.ts';

const logger = createLogger(false);

Deno.test('DEFAULT_CHUNKING_CONFIG - has expected defaults', () => {
  assertEquals(DEFAULT_CHUNKING_CONFIG.enabled, false);
  assertEquals(DEFAULT_CHUNKING_CONFIG.chunkSize, 100000);
  assertEquals(DEFAULT_CHUNKING_CONFIG.strategy, 'source');
});

Deno.test('shouldEnableChunking - returns false when explicitly disabled', () => {
  const config: IConfiguration = {
    name: 'test',
    sources: [
      { source: 'https://example.com/list1.txt' },
      { source: 'https://example.com/list2.txt' },
    ],
  };

  const result = shouldEnableChunking(config, { enabled: false }, logger);
  assertEquals(result, false);
});

Deno.test('shouldEnableChunking - returns false when no sources', () => {
  const config: IConfiguration = {
    name: 'test',
    sources: [],
  };

  const result = shouldEnableChunking(config, { enabled: true }, logger);
  assertEquals(result, false);
});

Deno.test('shouldEnableChunking - returns true when enabled with multiple sources', () => {
  const config: IConfiguration = {
    name: 'test',
    sources: [
      { source: 'https://example.com/list1.txt' },
      { source: 'https://example.com/list2.txt' },
      { source: 'https://example.com/list3.txt' },
    ],
  };

  const result = shouldEnableChunking(config, { enabled: true, strategy: 'source' }, logger);
  assertEquals(result, true);
});

Deno.test('shouldEnableChunking - returns true for source strategy with multiple sources', () => {
  const config: IConfiguration = {
    name: 'test',
    sources: [
      { source: 'https://example.com/list1.txt' },
      { source: 'https://example.com/list2.txt' },
    ],
  };

  const result = shouldEnableChunking(config, { strategy: 'source' }, logger);
  assertEquals(result, true);
});

Deno.test('splitIntoChunks - creates single chunk for single source', () => {
  const config: IConfiguration = {
    name: 'test',
    sources: [
      { source: 'https://example.com/list1.txt' },
    ],
  };

  const chunks = splitIntoChunks(config, { maxParallel: 4 }, logger);
  assertEquals(chunks.length, 1);
  assertEquals(chunks[0].sources?.length, 1);
});

Deno.test('splitIntoChunks - creates multiple chunks for multiple sources', () => {
  const config: IConfiguration = {
    name: 'test',
    sources: [
      { source: 'https://example.com/list1.txt' },
      { source: 'https://example.com/list2.txt' },
      { source: 'https://example.com/list3.txt' },
      { source: 'https://example.com/list4.txt' },
      { source: 'https://example.com/list5.txt' },
      { source: 'https://example.com/list6.txt' },
    ],
  };

  const chunks = splitIntoChunks(config, { maxParallel: 3 }, logger);
  assertEquals(chunks.length, 3);

  // Each chunk should have about 2 sources
  assertEquals(chunks[0].sources?.length, 2);
  assertEquals(chunks[1].sources?.length, 2);
  assertEquals(chunks[2].sources?.length, 2);
});

Deno.test('splitIntoChunks - respects maxParallel', () => {
  const config: IConfiguration = {
    name: 'test',
    sources: Array.from({ length: 20 }, (_, i) => ({
      source: `https://example.com/list${i}.txt`,
    })),
  };

  const chunks = splitIntoChunks(config, { maxParallel: 4 }, logger);
  assertEquals(chunks.length, 4);

  // Should have 5 sources per chunk
  assertEquals(chunks[0].sources?.length, 5);
  assertEquals(chunks[1].sources?.length, 5);
  assertEquals(chunks[2].sources?.length, 5);
  assertEquals(chunks[3].sources?.length, 5);
});

Deno.test('splitIntoChunks - adds chunk metadata', () => {
  const config: IConfiguration = {
    name: 'test',
    sources: [
      { source: 'https://example.com/list1.txt' },
      { source: 'https://example.com/list2.txt' },
    ],
  };

  const chunks = splitIntoChunks(config, { maxParallel: 2 }, logger);
  assertEquals(chunks.length, 2);

  assertEquals(chunks[0]._chunkMetadata?.index, 0);
  assertEquals(chunks[0]._chunkMetadata?.total, 2);

  assertEquals(chunks[1]._chunkMetadata?.index, 1);
  assertEquals(chunks[1]._chunkMetadata?.total, 2);
});

Deno.test('mergeChunks - merges multiple chunks correctly', () => {
  const chunk1 = ['rule1', 'rule2', 'rule3'];
  const chunk2 = ['rule4', 'rule5', 'rule6'];
  const chunk3 = ['rule7', 'rule8', 'rule9'];

  const merged = mergeChunks([chunk1, chunk2, chunk3], logger);
  assertEquals(merged.length, 9);
  assertEquals(merged, [
    'rule1',
    'rule2',
    'rule3',
    'rule4',
    'rule5',
    'rule6',
    'rule7',
    'rule8',
    'rule9',
  ]);
});

Deno.test('mergeChunks - removes duplicate rules', () => {
  const chunk1 = ['rule1', 'rule2', 'rule3'];
  const chunk2 = ['rule2', 'rule4', 'rule5']; // rule2 is duplicate
  const chunk3 = ['rule5', 'rule6', 'rule1']; // rule5 and rule1 are duplicates

  const merged = mergeChunks([chunk1, chunk2, chunk3], logger);
  assertEquals(merged.length, 6);
  assertEquals(merged, ['rule1', 'rule2', 'rule3', 'rule4', 'rule5', 'rule6']);
});

Deno.test('mergeChunks - preserves comments and empty lines', () => {
  const chunk1 = ['! Comment', 'rule1', ''];
  const chunk2 = ['# Another comment', 'rule2', ''];
  const chunk3 = ['rule3', '! Final comment'];

  const merged = mergeChunks([chunk1, chunk2, chunk3], logger);
  assertEquals(merged.length, 8);
  // Comments and empty lines should be preserved
  assertEquals(merged[0], '! Comment');
  assertEquals(merged[1], 'rule1');
  assertEquals(merged[2], '');
});

Deno.test('mergeChunks - handles single chunk', () => {
  const chunk1 = ['rule1', 'rule2', 'rule3'];

  const merged = mergeChunks([chunk1], logger);
  assertEquals(merged.length, 3);
  assertEquals(merged, ['rule1', 'rule2', 'rule3']);
});

Deno.test('mergeChunks - handles empty chunks', () => {
  const chunk1: string[] = [];
  const chunk2 = ['rule1', 'rule2'];
  const chunk3: string[] = [];

  const merged = mergeChunks([chunk1, chunk2, chunk3], logger);
  assertEquals(merged.length, 2);
  assertEquals(merged, ['rule1', 'rule2']);
});
