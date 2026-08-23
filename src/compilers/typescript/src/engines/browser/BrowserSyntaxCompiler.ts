/**
 * Compiles browser-syntax (AdGuard cosmetic/network/scriptlet) sources.
 *
 * Reuses the same grammar-agnostic building blocks as {@link FilterCompiler}
 * (`SourceCompiler`, `TransformationPipeline`, `HeaderGenerator`) — those already
 * operate on opaque `string[]` and don't assume DNS-style grammar. What differs for
 * browser-syntax lists is the *default* transformation set: several of the DNS
 * compiler's default/available transformations (`Compress`, `Validate`,
 * `ValidateAllowIp`) are written specifically against DNS/hosts-style rules and would
 * silently corrupt or strip valid cosmetic/network browser rules, so they are
 * rejected here rather than reused.
 *
 * @module
 */

import type { ICompilerEvents, IConfiguration, ILogger } from '../../types/index.ts';
import { TransformationType } from '../../types/index.ts';
import { ConfigurationValidator } from '../../configuration/index.ts';
import { TransformationPipeline } from '../../transformations/index.ts';
import {
  createEventBridgeHook,
  NoOpHookManager,
  TransformationHookManager,
} from '../../transformations/TransformationHooks.ts';
import { SourceCompiler } from '../../compiler/SourceCompiler.ts';
import { HeaderGenerator } from '../../compiler/HeaderGenerator.ts';
import type { CompilationResult } from '../../compiler/FilterCompiler.ts';
import {
  addChecksumToHeader,
  BenchmarkCollector,
  CompilationError,
  ConfigurationError,
  createEventEmitter,
  ErrorUtils,
  logger as defaultLogger,
} from '../../utils/index.ts';
import type { DownloaderOptions } from '../../downloader/index.ts';

/**
 * Transformations that are safe to run against browser-syntax rules.
 *
 * Excluded, and why:
 * - `Compress` — converts hosts-format entries to `||domain^`; meaningless (and
 *   destructive) for cosmetic/scriptlet rules.
 * - `Validate` / `ValidateAllowIp` — enforce the DNS-blocker-supported modifier
 *   allowlist (`important`, `dnstype`, …) and domain-only patterns; would reject
 *   valid browser rules wholesale (e.g. `$script`, `##.selector`).
 * - `InvertAllow` — DNS allow/block semantics don't map 1:1 onto AdGuard exception
 *   rule (`@@`) semantics for every rule category; deferred rather than guessed at.
 */
export const BROWSER_SAFE_TRANSFORMATIONS: ReadonlySet<TransformationType> = new Set([
  TransformationType.RemoveComments,
  TransformationType.Deduplicate,
  TransformationType.RemoveEmptyLines,
  TransformationType.TrimLines,
  TransformationType.InsertFinalNewLine,
  TransformationType.ConvertToAscii,
  TransformationType.RemoveModifiers,
  TransformationType.ConflictDetection,
  TransformationType.RuleOptimizer,
]);

/** Default pipeline applied when a browser-syntax source specifies none. */
const DEFAULT_BROWSER_TRANSFORMATIONS: TransformationType[] = [
  TransformationType.RemoveComments,
  TransformationType.Deduplicate,
  TransformationType.RemoveEmptyLines,
  TransformationType.TrimLines,
  TransformationType.InsertFinalNewLine,
];

/**
 * Injectable dependencies for {@link BrowserSyntaxCompiler}, mirroring
 * `FilterCompilerDependencies`.
 */
export interface BrowserSyntaxCompilerDependencies {
  /** Configuration validator. */
  validator?: ConfigurationValidator;
  /** Transformation pipeline. */
  pipeline?: TransformationPipeline;
  /** Source compiler. */
  sourceCompiler?: SourceCompiler;
  /** Header generator. */
  headerGenerator?: HeaderGenerator;
}

/** Options for {@link BrowserSyntaxCompiler}, mirroring `FilterCompilerOptions`. */
export interface BrowserSyntaxCompilerOptions {
  /** Logger for compilation messages — defaults to the module default logger. */
  logger?: ILogger;
  /** Compiler-level event handlers for observability. */
  events?: ICompilerEvents;
  /** Injectable dependencies (for testing/customization). */
  dependencies?: BrowserSyntaxCompilerDependencies;
  /** Options for the HTTP downloader (timeout, retries, user-agent). */
  downloaderOptions?: DownloaderOptions;
}

/**
 * Validates that every transformation requested (globally or per-source) for a
 * browser-syntax configuration is on the browser-safe allowlist.
 * @param configuration - The configuration to check.
 * @throws ConfigurationError listing the offending transformation names.
 */
function assertBrowserSafeTransformations(configuration: IConfiguration): void {
  const offending = new Set<string>();

  const check = (transformations?: readonly TransformationType[]) => {
    for (const t of transformations ?? []) {
      if (!BROWSER_SAFE_TRANSFORMATIONS.has(t)) offending.add(t);
    }
  };

  check(configuration.transformations as TransformationType[] | undefined);
  for (const source of configuration.sources) {
    check(source.transformations as TransformationType[] | undefined);
  }

  if (offending.size > 0) {
    throw new ConfigurationError(
      `The following transformations are not safe for the browser-syntax engine and were rejected: ${
        [...offending].join(', ')
      }. They assume DNS/hosts-style grammar.`,
      configuration.name,
    );
  }
}

/**
 * Compiles browser-syntax filter list configurations.
 *
 * Structurally mirrors {@link FilterCompiler} (same source-fetch → transform →
 * header flow) but defaults to, and enforces, a browser-safe transformation set. Used
 * standalone, or via `MultiEngineCompiler` alongside {@link FilterCompiler} when a
 * single configuration mixes DNS and browser-syntax sources.
 */
export class BrowserSyntaxCompiler {
  private readonly logger: ILogger;
  private readonly validator: ConfigurationValidator;
  private readonly pipeline: TransformationPipeline;
  private readonly sourceCompiler: SourceCompiler;
  private readonly headerGenerator: HeaderGenerator;

  /**
   * Creates a new BrowserSyntaxCompiler.
   * @param options - Compiler configuration options.
   */
  constructor(options?: BrowserSyntaxCompilerOptions) {
    this.logger = options?.logger ?? defaultLogger;
    const eventEmitter = createEventEmitter(options?.events);

    const hasTransformListeners = !!(
      options?.events?.onTransformationStart ||
      options?.events?.onTransformationComplete
    );
    const hookManager: TransformationHookManager = hasTransformListeners
      ? new TransformationHookManager(createEventBridgeHook(eventEmitter))
      : new NoOpHookManager();

    const deps = options?.dependencies;
    this.validator = deps?.validator ?? new ConfigurationValidator();
    this.pipeline = deps?.pipeline ??
      new TransformationPipeline(undefined, this.logger, eventEmitter, hookManager);
    this.sourceCompiler = deps?.sourceCompiler ?? new SourceCompiler({
      pipeline: this.pipeline,
      logger: this.logger,
      eventEmitter,
      downloaderOptions: options?.downloaderOptions,
    });
    this.headerGenerator = deps?.headerGenerator ?? new HeaderGenerator();
  }

  /**
   * Compiles a browser-syntax configuration into a rule list.
   * @param configuration - Configuration whose `sources` are all browser-syntax.
   *   Callers (e.g. `MultiEngineCompiler`) are responsible for routing only
   *   browser-syntax sources here — this method does not itself re-detect engine.
   * @returns Array of compiled rules.
   */
  public async compile(configuration: IConfiguration): Promise<string[]> {
    const result = await this.compileWithMetrics(configuration, false);
    return result.rules;
  }

  /**
   * Compiles with optional benchmark metrics, mirroring
   * {@link FilterCompiler.compileWithMetrics}.
   * @param configuration - Configuration whose `sources` are all browser-syntax.
   * @param benchmark - Whether to collect performance metrics.
   * @returns Compilation result with rules and optional metrics.
   */
  public async compileWithMetrics(
    configuration: IConfiguration,
    benchmark = false,
  ): Promise<CompilationResult> {
    const collector = benchmark ? new BenchmarkCollector() : null;
    collector?.start();

    const configName = configuration?.name ?? 'unknown';

    try {
      const validationResult = this.validator.validate(configuration);
      if (!validationResult.valid) {
        throw new ConfigurationError(
          validationResult.errorsText ?? 'Unknown validation error',
          configName,
        );
      }

      assertBrowserSafeTransformations(configuration);

      const totalSources = configuration.sources.length;
      collector?.setSourceCount(totalSources);

      const sourceResults = await Promise.all(
        configuration.sources.map(async (source, index) => ({
          source,
          rules: await this.sourceCompiler.compile(source, index, totalSources),
        })),
      );

      const chunks: string[][] = [];
      for (const { source, rules } of sourceResults) {
        chunks.push(this.headerGenerator.generateSourceHeader(source), rules);
      }
      let finalList: string[] = chunks.flat();

      const inputRuleCount = finalList.length;
      collector?.setRuleCount(inputRuleCount);

      const transformations = (configuration.transformations as TransformationType[] | undefined) ??
        DEFAULT_BROWSER_TRANSFORMATIONS;

      finalList = await this.pipeline.transform(finalList, configuration, transformations);

      const header = this.headerGenerator.generateListHeader(configuration);
      this.logger.info(`Browser-syntax list final length is ${header.length + finalList.length}`);

      let rules = header.concat(finalList);
      rules = await addChecksumToHeader(rules);

      collector?.setOutputRuleCount(rules.length);
      const metrics = collector?.finish();
      if (metrics) this.logger.info(collector!.generateReport());

      return { rules, metrics };
    } catch (error) {
      if (error instanceof ConfigurationError || error instanceof CompilationError) throw error;
      throw new CompilationError(
        `Browser-syntax compilation failed: ${ErrorUtils.getMessage(error)}`,
        undefined,
        ErrorUtils.toError(error),
      );
    }
  }
}
