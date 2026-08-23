/**
 * @module engines
 * Multi-engine (DNS vs browser-syntax) compilation surface.
 *
 * This module currently exposes {@link EngineKind} / {@link detectSourceEngine} /
 * {@link groupSourcesByEngine} — syntax detection and source routing. The
 * compilation-side pieces (`BrowserSyntaxCompiler`, `MultiEngineCompiler`) land in a
 * follow-up issue.
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
