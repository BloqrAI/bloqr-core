/**
 * Orchestrates compilation of a single configuration whose sources may mix DNS and
 * browser-syntax engines. Routes each source's grammar bucket to the compiler that
 * understands it, and never merges their outputs — DNS and browser-syntax rules are
 * always returned (and must always be written) as separate artifacts, because they're
 * consumed by different engines with different parsers.
 *
 * @module
 */

import type { IConfiguration, ISource, TransformationType } from '../types/index.ts';
import { FilterCompiler } from '../compiler/FilterCompiler.ts';
import type { CompilationResult, FilterCompilerOptions } from '../compiler/FilterCompiler.ts';
import {
  BROWSER_SAFE_TRANSFORMATIONS,
  BrowserSyntaxCompiler,
} from './browser/BrowserSyntaxCompiler.ts';
import type { BrowserSyntaxCompilerOptions } from './browser/BrowserSyntaxCompiler.ts';
import { detectSourceEngine } from './EngineDetector.ts';
import type { EngineKind } from './types.ts';
import { CANONICAL_TRANSFORMATION_ORDER } from '../transformations/TransformationRegistry.ts';

/**
 * Transformations that are registered/known but specifically DNS-only - the set this
 * module intentionally drops from a shared top-level `transformations` list before it
 * reaches the browser bucket (see {@link filterToBrowserSafe}). Derived as the
 * complement of {@link BROWSER_SAFE_TRANSFORMATIONS} within
 * `CANONICAL_TRANSFORMATION_ORDER` rather than hand-listed, so it can't drift out of
 * sync with either.
 *
 * Deliberately narrower than "everything not in `BROWSER_SAFE_TRANSFORMATIONS`":
 * an unrecognized name (a typo, or a real-but-unimplemented type like
 * `ConflictDetection`/`RuleOptimizer`) is *not* DNS-only, so it must NOT be silently
 * dropped here - it needs to survive into the browser bucket's `transformations` so
 * `ConfigurationValidator` can reject it, per issue #502.
 */
const DNS_ONLY_TRANSFORMATIONS: ReadonlySet<TransformationType> = new Set(
  CANONICAL_TRANSFORMATION_ORDER.filter((t) => !BROWSER_SAFE_TRANSFORMATIONS.has(t)),
);

/**
 * Result of a multi-engine compilation. Each key is present only when the
 * configuration had at least one source resolving to that engine.
 */
export interface MultiEngineCompilationResult {
  /** Result of compiling the DNS/hosts-style bucket, when present. */
  dns?: CompilationResult;
  /** Result of compiling the browser-syntax bucket, when present. */
  browser?: CompilationResult;
}

/** Options for {@link MultiEngineCompiler}. Forwarded to the underlying per-engine compilers. */
export interface MultiEngineCompilerOptions {
  /** Options forwarded to the underlying {@link FilterCompiler} (DNS engine). */
  filterCompilerOptions?: FilterCompilerOptions;
  /** Options forwarded to the underlying {@link BrowserSyntaxCompiler}. */
  browserSyntaxCompilerOptions?: BrowserSyntaxCompilerOptions;
  /**
   * Force every source through a single engine, bypassing detection entirely.
   * Equivalent to the CLI's `--engine dns|browser` (as opposed to the default
   * `auto`). Omit for auto-detection per source.
   */
  forceEngine?: EngineKind;
}

/**
 * Splits a configuration's sources into per-engine buckets and returns two
 * configurations (sharing the same top-level metadata) — one per engine, each
 * containing only that engine's sources. A bucket is omitted when it has no sources.
 */
function partitionConfiguration(
  configuration: IConfiguration,
  forceEngine?: EngineKind,
): { dns?: IConfiguration; browser?: IConfiguration } {
  const dnsSources: ISource[] = [];
  const browserSources: ISource[] = [];

  for (const source of configuration.sources) {
    const engine = forceEngine ?? detectSourceEngine(source, [], configuration.defaultEngine);
    (engine === 'browser' ? browserSources : dnsSources).push(source);
  }

  const result: { dns?: IConfiguration; browser?: IConfiguration } = {};
  if (dnsSources.length > 0) {
    result.dns = { ...configuration, sources: dnsSources };
  }
  if (browserSources.length > 0) {
    result.browser = {
      ...configuration,
      sources: browserSources,
      transformations: filterToBrowserSafe(
        configuration.transformations as TransformationType[] | undefined,
      ),
    };
  }
  return result;
}

/**
 * Filters a top-level transformation list by removing known DNS-only entries.
 *
 * The shared `configuration.transformations` field is written with one engine in
 * mind — often the DNS engine's own defaults (e.g. injected by the CLI when the user
 * didn't specify any) — so silently dropping known DNS-only entries (`Compress`,
 * `Validate`, `ValidateAllowIp`, `InvertAllow`, i.e. {@link DNS_ONLY_TRANSFORMATIONS})
 * here, rather than throwing, lets a single mixed-engine configuration carry one
 * `transformations` list without every browser-syntax source having to opt out
 * explicitly. `undefined` in means `undefined` out, so {@link BrowserSyntaxCompiler}
 * falls back to its own browser-safe defaults.
 *
 * Deliberately filters OUT the known-DNS-only set rather than filtering IN
 * {@link BROWSER_SAFE_TRANSFORMATIONS}: an unrecognized transformation name (a typo,
 * or a real-but-unimplemented type such as `ConflictDetection`/`RuleOptimizer`) must
 * survive this step so it reaches `BrowserSyntaxCompiler`'s `ConfigurationValidator`
 * and gets rejected there — filtering IN the browser-safe set would instead have
 * silently discarded it before validation ever saw it (issue #502).
 *
 * Per-source `source.transformations` are deliberately NOT filtered here — a source
 * explicitly tagged `engine: 'browser'` that also explicitly requests an unsafe
 * transformation is a real authoring mistake, and {@link BrowserSyntaxCompiler}
 * throws on it rather than silently dropping it.
 * @param transformations - The shared top-level transformation list, if any.
 * @returns The list with known DNS-only entries removed, or `undefined` when the
 *   input was `undefined` or nothing survived filtering.
 */
export function filterToBrowserSafe(
  transformations?: readonly TransformationType[],
): TransformationType[] | undefined {
  if (!transformations) return undefined;
  const filtered = transformations.filter((t) => !DNS_ONLY_TRANSFORMATIONS.has(t));
  return filtered.length > 0 ? filtered : undefined;
}

/**
 * Compiles a configuration whose sources may span both the DNS and browser-syntax
 * engines, producing one {@link CompilationResult} per engine actually present.
 *
 * Detection here is declarative-only (explicit `source.engine`, `SourceType.Hosts`,
 * or `configuration.defaultEngine`) — it does not download sources first to sniff
 * content, matching `groupSourcesByEngine`'s documented behavior. A source that needs
 * content-based sniffing should set `engine` explicitly.
 *
 * When every source resolves to the same engine (the common case today — 100% of
 * existing single-engine configs), only that engine's compiler runs; the other is
 * never invoked, and the DNS-only path produces byte-identical output to before this
 * class existed.
 */
export class MultiEngineCompiler {
  private readonly filterCompiler: FilterCompiler;
  private readonly browserSyntaxCompiler: BrowserSyntaxCompiler;
  private readonly forceEngine?: EngineKind;

  /**
   * Creates a new MultiEngineCompiler.
   * @param options - Compiler configuration options.
   */
  constructor(options?: MultiEngineCompilerOptions) {
    this.filterCompiler = new FilterCompiler(options?.filterCompilerOptions);
    this.browserSyntaxCompiler = new BrowserSyntaxCompiler(options?.browserSyntaxCompilerOptions);
    this.forceEngine = options?.forceEngine;
  }

  /**
   * Compiles the configuration, returning per-engine results. At least one of
   * `dns`/`browser` is always present (a configuration must have >=1 source).
   * @param configuration - Configuration whose sources may span both engines.
   * @param benchmark - Whether to collect performance metrics for each engine run.
   * @returns Per-engine compilation results.
   */
  public async compile(
    configuration: IConfiguration,
    benchmark = false,
  ): Promise<MultiEngineCompilationResult> {
    const { dns, browser } = partitionConfiguration(configuration, this.forceEngine);

    const result: MultiEngineCompilationResult = {};

    // Sequential, not parallel: keeps peak memory/network concurrency bounded to
    // one engine's worth of sources at a time, and keeps error attribution
    // ("did the DNS or browser bucket fail?") unambiguous without extra
    // bookkeeping. Revisit if multi-engine configs become large enough that
    // sequential fetch time matters.
    if (dns) {
      result.dns = await this.filterCompiler.compileWithMetrics(dns, benchmark);
    }
    if (browser) {
      result.browser = await this.browserSyntaxCompiler.compileWithMetrics(browser, benchmark);
    }

    return result;
  }
}
