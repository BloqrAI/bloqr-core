/**
 * compiler-core — Deno Entry Point
 *
 * Re-exports the core compilation engine (the `@bloqr/compiler-core`
 * public API) and provides the CLI entry point. For the CLI/config/console
 * orchestration layer, import from `./orchestration/index.ts` (also
 * available as the package's `./orchestration` export subpath).
 *
 * @module
 */

// Re-export the core compilation engine
export * from './index.ts';

// Deno CLI entry point
import { main } from './orchestration/cli.ts';

// Run if executed directly
if (import.meta.main) {
  const exitCode = await main(Deno.args);
  Deno.exit(exitCode);
}
