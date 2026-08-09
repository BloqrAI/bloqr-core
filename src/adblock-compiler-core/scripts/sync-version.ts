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
 * Update the "version" field in deno.json.
 */
async function syncJsonFile(path: string, version: string): Promise<void> {
  const content = await Deno.readTextFile(path);
  const json = JSON.parse(content) as Record<string, unknown>;
  const old = json['version'] as string | undefined;
  if (old === version) {
    console.log(`  ${path}: already at ${version}, skipping`);
    return;
  }
  json['version'] = version;
  await Deno.writeTextFile(path, JSON.stringify(json, null, 2) + '\n');
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
