/**
 * Tests for the bloqr-validator wiring in `runRulesValidator` (#361).
 *
 * `runRulesValidator` shells out to the `bloqr-validate` CLI rather than
 * binding against a native library directly. To keep these tests
 * deterministic and independent of whether the Rust workspace has been
 * built locally, most cases point `RULES_VALIDATE_PATH` at a small fake
 * shell script that emits canned `--json file` output. A separate,
 * best-effort end-to-end case exercises the real `bloqr-validate` binary
 * when one has already been built into `target/{release,debug}` (e.g. in
 * the Docker dev environment or after running the Rust test suite
 * locally) and is skipped otherwise.
 */

import { assertEquals, assertRejects, assertStringIncludes } from '@std/assert';
import { chmodSync, existsSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { compileFilters, runRulesValidator, writeOutput } from './compiler.ts';
import { readConfiguration } from './config-reader.ts';
import { createLogger } from './logger.ts';
import type { ValidationEvent } from './types.ts';

const logger = createLogger(false);

function makeFakeValidator(dir: string, stdout: string, exitCode = 0): string {
  const scriptPath = join(dir, 'bloqr-validate');
  writeFileSync(scriptPath, `#!/bin/sh\ncat <<'EOF'\n${stdout}\nEOF\nexit ${exitCode}\n`);
  chmodSync(scriptPath, 0o755);
  return scriptPath;
}

async function withTempDir(fn: (dir: string) => Promise<void>): Promise<void> {
  const dir = mkdtempSync(join(tmpdir(), 'bloqr-validator-ts-test-'));
  try {
    await fn(dir);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
}

Deno.test('runRulesValidator - allowUnvalidated skips a missing binary without throwing', async () => {
  await withTempDir(async (dir) => {
    const outputPath = join(dir, 'output.txt');
    writeOutput(outputPath, ['||example.com^'], logger);

    const originalOverride = Deno.env.get('RULES_VALIDATE_PATH');
    Deno.env.set('RULES_VALIDATE_PATH', join(dir, 'does-not-exist'));
    try {
      // Explicit opt-out reverts to legacy behavior: a missing validator
      // never throws (PATH/dev-fallback lookups may still legitimately find
      // nothing, or, in a dev environment with a built binary, something).
      await runRulesValidator(outputPath, undefined, logger, true);
    } finally {
      if (originalOverride === undefined) {
        Deno.env.delete('RULES_VALIDATE_PATH');
      } else {
        Deno.env.set('RULES_VALIDATE_PATH', originalOverride);
      }
    }
  });
});

Deno.test('runRulesValidator - valid syntax fires passing event and does not throw', async () => {
  await withTempDir(async (dir) => {
    const binary = makeFakeValidator(
      dir,
      '{"is_valid":true,"format":"adblock","valid_rules":1,"invalid_rules":0,"messages":[]}',
    );
    const outputPath = join(dir, 'output.txt');
    writeOutput(outputPath, ['||example.com^'], logger);

    const events: ValidationEvent[] = [];
    const originalOverride = Deno.env.get('RULES_VALIDATE_PATH');
    Deno.env.set('RULES_VALIDATE_PATH', binary);
    try {
      await runRulesValidator(
        outputPath,
        { onValidation: (event) => void events.push(event) },
        logger,
      );
    } finally {
      if (originalOverride === undefined) {
        Deno.env.delete('RULES_VALIDATE_PATH');
      } else {
        Deno.env.set('RULES_VALIDATE_PATH', originalOverride);
      }
    }

    assertEquals(events.length, 1);
    assertEquals(events[0].stageName, 'bloqr-validator');
    assertEquals(events[0].passed, true);
    assertEquals(events[0].itemsValidated, 1);
    assertEquals(events[0].findings.length, 0);
  });
});

Deno.test('runRulesValidator - invalid syntax with no messages emits RV001 finding and throws by default', async () => {
  await withTempDir(async (dir) => {
    const binary = makeFakeValidator(
      dir,
      '{"is_valid":false,"format":"unknown","valid_rules":0,"invalid_rules":1,"messages":[]}',
      1,
    );
    const outputPath = join(dir, 'output.txt');
    writeOutput(outputPath, ['bad rule'], logger);

    const events: ValidationEvent[] = [];
    const originalOverride = Deno.env.get('RULES_VALIDATE_PATH');
    Deno.env.set('RULES_VALIDATE_PATH', binary);
    try {
      // Fail-closed by default: an Error/Critical finding aborts even though
      // no handler was registered to explicitly set event.abort - this is
      // the gap the fail-closed rewrite closes.
      await assertRejects(
        () =>
          runRulesValidator(
            outputPath,
            { onValidation: (event) => void events.push(event) },
            logger,
          ),
        Error,
      );
    } finally {
      if (originalOverride === undefined) {
        Deno.env.delete('RULES_VALIDATE_PATH');
      } else {
        Deno.env.set('RULES_VALIDATE_PATH', originalOverride);
      }
    }

    assertEquals(events.length, 1);
    assertEquals(events[0].passed, false);
    assertEquals(events[0].findings.length, 1);
    assertEquals(events[0].findings[0].severity, 'error');
    assertStringIncludes(events[0].findings[0].message, 'RV001');
  });
});

Deno.test('runRulesValidator - invalid syntax does not throw when allowUnvalidated is set', async () => {
  await withTempDir(async (dir) => {
    const binary = makeFakeValidator(
      dir,
      '{"is_valid":false,"format":"unknown","valid_rules":0,"invalid_rules":1,"messages":[]}',
      1,
    );
    const outputPath = join(dir, 'output.txt');
    writeOutput(outputPath, ['bad rule'], logger);

    const events: ValidationEvent[] = [];
    const originalOverride = Deno.env.get('RULES_VALIDATE_PATH');
    Deno.env.set('RULES_VALIDATE_PATH', binary);
    try {
      // Explicit opt-out reverts to legacy behavior: only a handler-set
      // abort counts.
      await runRulesValidator(
        outputPath,
        { onValidation: (event) => void events.push(event) },
        logger,
        true,
      );
    } finally {
      if (originalOverride === undefined) {
        Deno.env.delete('RULES_VALIDATE_PATH');
      } else {
        Deno.env.set('RULES_VALIDATE_PATH', originalOverride);
      }
    }

    assertEquals(events.length, 1);
    assertEquals(events[0].passed, false);
  });
});

Deno.test('runRulesValidator - handler can abort compilation on findings', async () => {
  await withTempDir(async (dir) => {
    const binary = makeFakeValidator(
      dir,
      '{"is_valid":false,"format":"adblock","valid_rules":0,"invalid_rules":1,"messages":["line 1: bad rule"]}',
      1,
    );
    const outputPath = join(dir, 'output.txt');
    writeOutput(outputPath, ['bad rule'], logger);

    const originalOverride = Deno.env.get('RULES_VALIDATE_PATH');
    Deno.env.set('RULES_VALIDATE_PATH', binary);
    try {
      await assertRejects(
        () =>
          runRulesValidator(outputPath, {
            onValidation: (event) => {
              event.abort = true;
              event.abortReason = 'aborted by test handler';
            },
          }, logger),
        Error,
        'aborted by test handler',
      );
    } finally {
      if (originalOverride === undefined) {
        Deno.env.delete('RULES_VALIDATE_PATH');
      } else {
        Deno.env.set('RULES_VALIDATE_PATH', originalOverride);
      }
    }
  });
});

Deno.test('runRulesValidator - malformed JSON output throws by default', async () => {
  await withTempDir(async (dir) => {
    const binary = makeFakeValidator(dir, 'not json', 1);
    const outputPath = join(dir, 'output.txt');
    writeOutput(outputPath, ['||example.com^'], logger);

    const originalOverride = Deno.env.get('RULES_VALIDATE_PATH');
    Deno.env.set('RULES_VALIDATE_PATH', binary);
    try {
      // A validator we couldn't parse output from tells us nothing about the
      // output's safety, so it can't be treated as "no findings".
      await assertRejects(() => runRulesValidator(outputPath, undefined, logger), Error);
    } finally {
      if (originalOverride === undefined) {
        Deno.env.delete('RULES_VALIDATE_PATH');
      } else {
        Deno.env.set('RULES_VALIDATE_PATH', originalOverride);
      }
    }
  });
});

Deno.test('runRulesValidator - malformed JSON output is swallowed when allowUnvalidated is set', async () => {
  await withTempDir(async (dir) => {
    const binary = makeFakeValidator(dir, 'not json', 1);
    const outputPath = join(dir, 'output.txt');
    writeOutput(outputPath, ['||example.com^'], logger);

    const originalOverride = Deno.env.get('RULES_VALIDATE_PATH');
    Deno.env.set('RULES_VALIDATE_PATH', binary);
    try {
      await runRulesValidator(outputPath, undefined, logger, true);
    } finally {
      if (originalOverride === undefined) {
        Deno.env.delete('RULES_VALIDATE_PATH');
      } else {
        Deno.env.set('RULES_VALIDATE_PATH', originalOverride);
      }
    }
  });
});

// Best-effort end-to-end check against the real `bloqr-validate` binary, if
// one has already been built into the Cargo workspace's target directory
// (e.g. via `cargo build -p bloqr-validator-core-cli` or the Docker dev image).
// Skipped otherwise so this suite doesn't require a Rust toolchain in CI.
const realBinaryPath = fileURLToPath(
  new URL('../../../../../target/release/bloqr-validate', import.meta.url),
);

Deno.test({
  name: 'runRulesValidator - real bloqr-validate binary end-to-end (if built)',
  ignore: !existsSync(realBinaryPath),
  fn: async () => {
    await withTempDir(async (dir) => {
      const outputPath = join(dir, 'output.txt');
      writeOutput(outputPath, ['||example.com^'], logger);

      const events: ValidationEvent[] = [];
      const originalOverride = Deno.env.get('RULES_VALIDATE_PATH');
      Deno.env.set('RULES_VALIDATE_PATH', realBinaryPath);
      try {
        await runRulesValidator(
          outputPath,
          { onValidation: (event) => void events.push(event) },
          logger,
        );
      } finally {
        if (originalOverride === undefined) {
          Deno.env.delete('RULES_VALIDATE_PATH');
        } else {
          Deno.env.set('RULES_VALIDATE_PATH', originalOverride);
        }
      }

      assertEquals(events.length, 1);
      assertEquals(events[0].passed, true);
      assertEquals(events[0].itemsValidated, 1);
    });
  },
});

// Regression coverage for #427: readConfiguration() tags its returned config object with
// `_sourceFormat`/`_sourcePath` orchestration-layer metadata. compileFilters() must strip
// that before handing the config to the core engine's compile() (which validates against a
// strict schema that rejects unrecognized properties) - a real file-read-then-compile,
// exercising the exact interaction that was broken, not just each layer in isolation.
Deno.test('compileFilters - accepts a readConfiguration()-produced config (readConfiguration + compile integration)', async () => {
  await withTempDir(async (dir) => {
    const sourcePath = join(dir, 'source.txt');
    writeFileSync(sourcePath, '||example.com^\n||duplicate.com^\n||duplicate.com^\n');

    const configPath = join(dir, 'config.json');
    writeFileSync(
      configPath,
      JSON.stringify({
        name: 'Regression Test',
        sources: [{ name: 'source-1', source: sourcePath, type: 'adblock' }],
        transformations: ['Deduplicate'],
      }),
    );

    const config = readConfiguration(configPath, undefined, logger);
    // readConfiguration() must have tagged this object - otherwise the regression this test
    // guards against can't be exercised at all.
    assertEquals(Object.hasOwn(config, '_sourceFormat'), true);
    assertEquals(Object.hasOwn(config, '_sourcePath'), true);

    const rules = await compileFilters(config, logger);

    assertEquals(rules.includes('||example.com^'), true);
    assertEquals(rules.includes('||duplicate.com^'), true);
    assertEquals(rules.filter((r) => r === '||duplicate.com^').length, 1);
  });
});
