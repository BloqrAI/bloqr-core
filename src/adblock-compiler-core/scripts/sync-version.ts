#!/usr/bin/env -S deno run --allow-read --allow-write

/**
 * Sync version from src/version.ts to deno.json.
 *
 * This script makes src/version.ts the single source of writable truth for
 * @bloqr/compiler-core's version. Run it after updating the VERSION constant
 * in src/version.ts to propagate the version into deno.json (the field JSR's
 * publish step reads).
 *
 * Mirrors the equivalent script in bloqr-compiler (scripts/sync-version.ts) —
 * see docs/architecture/versioning-strategy.md for the org-wide pattern this
 * is part of.
 *
 * Usage:
 *   deno task version:sync
 *   deno run --allow-read --allow-write scripts/sync-version.ts
 */

import { VERSION } from '../src/version.ts';
import { isValidSemver } from '../src/utils/semver.ts';

function readVersionFromSource(): string {
  if (!isValidSemver(VERSION)) {
    throw new Error('VERSION has an invalid format in src/version.ts');
  }
  return VERSION;
}

/**
 * Update the "version" field in deno.json via a surgical regex replace,
 * rather than a JSON.parse/stringify round-trip. deno.json supports JSONC
 * (`//` comments) — e.g. the accepted-npm-exception notes next to `ora`,
 * `figlet`, and `@inquirer/prompts` in the `imports` block — and Deno's own
 * config loader tolerates that, but a plain JSON.parse/stringify round-trip
 * would both reject the comments as invalid JSON and silently drop them on
 * write. Regex-replacing just the version line preserves everything else
 * byte-for-byte, matching the same approach bloqr-compiler's sync-version.ts
 * uses for wrangler.toml's COMMENT_VERSION field.
 */
async function syncJsonFile(path: string, version: string): Promise<void> {
  const content = await Deno.readTextFile(path);
  const versionLineRegex = /^(\s*"version"\s*:\s*")([^"]*)(")/m;
  const match = content.match(versionLineRegex);
  if (!match) {
    throw new Error(`Could not find a "version" field in ${path}`);
  }
  const old = match[2];
  if (old === version) {
    console.log(`  ${path}: already at ${version}, skipping`);
    return;
  }
  const updated = content.replace(versionLineRegex, `$1${version}$3`);
  await Deno.writeTextFile(path, updated);
  console.log(`  ${path}: ${old} → ${version}`);
}

async function main(): Promise<void> {
  const version = readVersionFromSource();
  console.log(`Syncing version ${version} from src/version.ts to:`);
  await syncJsonFile('deno.json', version);
  console.log('Done.');
}

if (import.meta.main) {
  await main();
}
