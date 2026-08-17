/**
 * Tests for configuration reader
 * Deno-native testing implementation
 */

import { assertEquals, assertThrows } from '@std/assert';
import { detectFormat } from './config-reader.ts';

Deno.test('detectFormat - detects JSON format from .json extension', () => {
  assertEquals(detectFormat('config.json'), 'json');
  assertEquals(detectFormat('/path/to/compiler-config.json'), 'json');
});

Deno.test('detectFormat - detects YAML format from .yaml extension', () => {
  assertEquals(detectFormat('config.yaml'), 'yaml');
  assertEquals(detectFormat('/path/to/compiler-config.yaml'), 'yaml');
});

Deno.test('detectFormat - detects YAML format from .yml extension', () => {
  assertEquals(detectFormat('config.yml'), 'yaml');
  assertEquals(detectFormat('/path/to/compiler-config.yml'), 'yaml');
});

Deno.test('detectFormat - detects TOML format from .toml extension', () => {
  assertEquals(detectFormat('config.toml'), 'toml');
  assertEquals(detectFormat('/path/to/compiler-config.toml'), 'toml');
});

Deno.test('detectFormat - is case-insensitive for extensions', () => {
  assertEquals(detectFormat('config.JSON'), 'json');
  assertEquals(detectFormat('config.YAML'), 'yaml');
  assertEquals(detectFormat('config.TOML'), 'toml');
});

Deno.test('detectFormat - throws error for unknown extension .xml', () => {
  assertThrows(
    () => detectFormat('config.xml'),
    Error,
    'Unknown configuration file extension: .xml',
  );
});

Deno.test('detectFormat - throws error for unknown extension .txt', () => {
  assertThrows(
    () => detectFormat('config.txt'),
    Error,
    'Unknown configuration file extension: .txt',
  );
});
