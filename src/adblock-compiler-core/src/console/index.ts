/**
 * Interactive console UI components for the Rules Compiler.
 *
 * Provides the terminal-styling primitives (colored output, tables,
 * spinners, banners) and the {@linkcode ConsoleApplication} menu-driven
 * interface used by the orchestration layer's interactive mode. Import
 * this module directly (via the package's `console` export subpath) to
 * build custom terminal UIs, or use `runInteractive` for the ready-made
 * menu experience.
 *
 * @example
 * ```ts
 * import { runInteractive } from '@bloqr/compiler-core/console';
 *
 * const exitCode = await runInteractive({ configPath: 'compiler-config.yaml' });
 * ```
 *
 * @module console
 * @packageDocumentation
 */

// Utilities
export {
  bold,
  chalk,
  colorStatus,
  createKeyValueTable,
  createSpinner,
  createTable,
  dim,
  displayTable,
  formatBytes,
  formatElapsed,
  formatValue,
  generateBanner,
  showError,
  showHeader,
  showInfo,
  showNoItems,
  showPanel,
  showRule,
  showSuccess,
  showWarning,
  showWelcomeBanner,
  truncate,
  withSpinner,
} from './utils.ts';

// Application
export { ConsoleApplication, runInteractive } from './app.ts';
export type { ConsoleAppOptions } from './app.ts';
