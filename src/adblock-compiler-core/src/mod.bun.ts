#!/usr/bin/env bun
/**
 * adblock-compiler-core — Bun Entry Point
 *
 * Bun implements Node's `fs`/`process` APIs natively, and `orchestration/cli.ts`'s
 * `main()` already detects the runtime itself, so no Bun-specific wiring is
 * needed here — this file exists purely as the documented, explicit entry
 * point for Bun users, mirroring `./mod.ts` (the Deno entry point). Bun is a
 * supported *runtime target* for consumers of this package; Deno remains this
 * project's own runtime.
 *
 * Run with `bun run src/mod.bun.ts [args]` from a local clone, after
 * installing this package's npm/JSR dependencies for Bun (see README.md).
 *
 * @module
 */

// Re-export the core compilation engine
export * from './index.ts';

import process from 'node:process';
import { main } from './orchestration/cli.ts';

// Run if executed directly
if (import.meta.main) {
  const exitCode = await main(process.argv.slice(2));
  process.exit(exitCode);
}
