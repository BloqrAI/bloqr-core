/**
 * @module engines
 * Multi-engine (DNS vs browser-syntax) compilation surface.
 *
 * - {@link EngineKind} / {@link detectSourceEngine} / {@link groupSourcesByEngine} —
 *   syntax detection and source routing.
 * - {@link BrowserSyntaxCompiler} — compiles browser-syntax (cosmetic/network/
 *   scriptlet) sources.
 * - {@link MultiEngineCompiler} — orchestrates a configuration whose sources span
 *   both engines, producing one output per engine.
 */
export {
  classifyLine,
  detectEngineFromLines,
  detectSourceEngine,
  groupSourcesByEngine,
} from './EngineDetector.ts';
export type {
  EngineDetectorOptions,
  EngineKind,
  GroupedSources,
  LineEngineSignal,
} from './types.ts';
export {
  BROWSER_SAFE_TRANSFORMATIONS,
  BrowserSyntaxCompiler,
} from './browser/BrowserSyntaxCompiler.ts';
export type {
  BrowserSyntaxCompilerDependencies,
  BrowserSyntaxCompilerOptions,
} from './browser/BrowserSyntaxCompiler.ts';
export { filterToBrowserSafe, MultiEngineCompiler } from './MultiEngineCompiler.ts';
export type {
  MultiEngineCompilationResult,
  MultiEngineCompilerOptions,
} from './MultiEngineCompiler.ts';
