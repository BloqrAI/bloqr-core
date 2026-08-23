/**
 * Shared types for the multi-engine (DNS vs browser-syntax) compilation surface.
 */

import type { EngineKind, ISource } from '../types/index.ts';

export type { EngineKind };

/**
 * Result of sniffing a single line for engine signals.
 * `null` means the line carries no engine-specific signal (e.g. blank/comment).
 */
export type LineEngineSignal = EngineKind | null;

/**
 * Options controlling {@link EngineDetector} sniffing behavior.
 */
export interface EngineDetectorOptions {
  /** Max number of non-empty, non-comment lines to sample when sniffing. Default 200. */
  sampleSize?: number;
  /** Engine to fall back to when detection is ambiguous or the sample is empty. Default 'dns'. */
  fallback?: EngineKind;
}

/**
 * A group of sources bucketed by resolved engine, in original order within each bucket.
 */
export interface GroupedSources {
  /** Sources resolved to the DNS/hosts-style engine. */
  dns: ISource[];
  /** Sources resolved to the browser-syntax engine. */
  browser: ISource[];
}
