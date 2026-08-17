/**
 * Tests for CLI argument parsing
 * Deno-native testing implementation
 */

import { assertEquals, assertThrows } from '@std/assert';
import { parseArgs } from './cli.ts';

Deno.test('parseArgs - returns default options with no arguments', () => {
  const options = parseArgs([]);
  assertEquals(options.copyToRules, false);
  assertEquals(options.version, false);
  assertEquals(options.help, false);
  assertEquals(options.debug, false);
  assertEquals(options.configPath, undefined);
  assertEquals(options.outputPath, undefined);
});

Deno.test('parseArgs - parses -c flag for config path', () => {
  const options = parseArgs(['-c', 'config.yaml']);
  assertEquals(options.configPath, 'config.yaml');
});

Deno.test('parseArgs - parses --config flag for config path', () => {
  const options = parseArgs(['--config', 'config.json']);
  assertEquals(options.configPath, 'config.json');
});

Deno.test('parseArgs - parses -o flag for output path', () => {
  const options = parseArgs(['-o', 'output.txt']);
  assertEquals(options.outputPath, 'output.txt');
});

Deno.test('parseArgs - parses --output flag for output path', () => {
  const options = parseArgs(['--output', 'rules.txt']);
  assertEquals(options.outputPath, 'rules.txt');
});

Deno.test('parseArgs - parses -r flag for copy to rules', () => {
  const options = parseArgs(['-r']);
  assertEquals(options.copyToRules, true);
});

Deno.test('parseArgs - parses --copy-to-rules flag', () => {
  const options = parseArgs(['--copy-to-rules']);
  assertEquals(options.copyToRules, true);
});

Deno.test('parseArgs - parses -f flag for format', () => {
  const options = parseArgs(['-f', 'yaml']);
  assertEquals(options.format, 'yaml');
});

Deno.test('parseArgs - parses --format flag', () => {
  const options = parseArgs(['--format', 'toml']);
  assertEquals(options.format, 'toml');
});

Deno.test('parseArgs - throws error for invalid format', () => {
  assertThrows(
    () => parseArgs(['-f', 'xml']),
    Error,
    'Invalid format: xml',
  );
});

Deno.test('parseArgs - parses -v flag for version', () => {
  const options = parseArgs(['-v']);
  assertEquals(options.version, true);
});

Deno.test('parseArgs - parses --version flag', () => {
  const options = parseArgs(['--version']);
  assertEquals(options.version, true);
});

Deno.test('parseArgs - parses -h flag for help', () => {
  const options = parseArgs(['-h']);
  assertEquals(options.help, true);
});

Deno.test('parseArgs - parses --help flag', () => {
  const options = parseArgs(['--help']);
  assertEquals(options.help, true);
});

Deno.test('parseArgs - parses -d flag for debug', () => {
  const options = parseArgs(['-d']);
  assertEquals(options.debug, true);
});

Deno.test('parseArgs - parses --debug flag', () => {
  const options = parseArgs(['--debug']);
  assertEquals(options.debug, true);
});

Deno.test('parseArgs - parses --show-config flag', () => {
  const options = parseArgs(['--show-config']);
  assertEquals(options.showConfig, true);
});

Deno.test('parseArgs - parses positional config path', () => {
  const options = parseArgs(['config.yaml']);
  assertEquals(options.configPath, 'config.yaml');
});

Deno.test('parseArgs - parses multiple flags together', () => {
  const options = parseArgs(['-c', 'config.yaml', '-o', 'output.txt', '-r', '-d']);
  assertEquals(options.configPath, 'config.yaml');
  assertEquals(options.outputPath, 'output.txt');
  assertEquals(options.copyToRules, true);
  assertEquals(options.debug, true);
});

Deno.test('parseArgs - parses --rules-dir flag', () => {
  const options = parseArgs(['--rules-dir', '/custom/rules']);
  assertEquals(options.rulesDirectory, '/custom/rules');
});
