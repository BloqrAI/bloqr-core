import {
  type IConfiguration,
  type ILogger,
  type ISource,
  TransformationType,
} from '../types/index.ts';
import {
  type CompilerEventEmitter,
  createEventEmitter,
  logger as defaultLogger,
  type Wildcard,
} from '../utils/index.ts';
import type { Transformation } from './base/Transformation.ts';
import { RemoveCommentsTransformation } from './RemoveCommentsTransformation.ts';
import { TrimLinesTransformation } from './TrimLinesTransformation.ts';
import { RemoveEmptyLinesTransformation } from './RemoveEmptyLinesTransformation.ts';
import { InsertFinalNewLineTransformation } from './InsertFinalNewLineTransformation.ts';
import { ConvertToAsciiTransformation } from './ConvertToAsciiTransformation.ts';
import { InvertAllowTransformation } from './InvertAllowTransformation.ts';
import { RemoveModifiersTransformation } from './RemoveModifiersTransformation.ts';
import { DeduplicateTransformation } from './DeduplicateTransformation.ts';
import { ValidateAllowIpTransformation, ValidateTransformation } from './ValidateTransformation.ts';
import { CompressTransformation } from './CompressTransformation.ts';
import { FilterService } from '../services/FilterService.ts';
import {
  createEventBridgeHook,
  NoOpHookManager,
  TransformationHookManager,
} from './TransformationHooks.ts';

/**
 * Registry for transformation classes.
 * Implements the Registry pattern for managing transformation instances.
 */
export class TransformationRegistry {
  private readonly transformations: Map<TransformationType, Transformation>;
  private readonly logger: ILogger;

  /**
   * Creates a new TransformationRegistry
   * @param logger - Logger instance for output
   */
  constructor(logger?: ILogger) {
    this.logger = logger || defaultLogger;
    this.transformations = new Map();
    this.registerDefaultTransformations();
  }

  /**
   * Registers the default transformations.
   */
  private registerDefaultTransformations(): void {
    this.register(TransformationType.RemoveComments, new RemoveCommentsTransformation(this.logger));
    this.register(TransformationType.TrimLines, new TrimLinesTransformation(this.logger));
    this.register(
      TransformationType.RemoveEmptyLines,
      new RemoveEmptyLinesTransformation(this.logger),
    );
    this.register(
      TransformationType.InsertFinalNewLine,
      new InsertFinalNewLineTransformation(this.logger),
    );
    this.register(TransformationType.ConvertToAscii, new ConvertToAsciiTransformation(this.logger));
    this.register(TransformationType.InvertAllow, new InvertAllowTransformation(this.logger));
    this.register(
      TransformationType.RemoveModifiers,
      new RemoveModifiersTransformation(this.logger),
    );
    this.register(TransformationType.Deduplicate, new DeduplicateTransformation(this.logger));
    this.register(TransformationType.Validate, new ValidateTransformation(false, this.logger));
    this.register(
      TransformationType.ValidateAllowIp,
      new ValidateAllowIpTransformation(this.logger),
    );
    this.register(TransformationType.Compress, new CompressTransformation(this.logger));
  }

  /**
   * Registers a transformation.
   */
  public register(type: TransformationType, transformation: Transformation): void {
    this.transformations.set(type, transformation);
  }

  /**
   * Gets a transformation by type.
   */
  public get(type: TransformationType): Transformation | undefined {
    return this.transformations.get(type);
  }

  /**
   * Unregisters a transformation.
   *
   * This is the counterpart to {@link register} and is what
   * `SubsystemBridge.unregisterTransformation` should be wired to, so
   * plugin-provided transformations are removed from the pipeline when
   * their plugin is unregistered or rolled back.
   *
   * @returns `true` if a transformation was registered under `type` and removed
   */
  public unregister(type: TransformationType): boolean {
    return this.transformations.delete(type);
  }

  /**
   * Checks if a transformation is registered.
   */
  public has(type: TransformationType): boolean {
    return this.transformations.has(type);
  }

  /**
   * Gets all registered transformation types.
   */
  public getRegisteredTypes(): TransformationType[] {
    return Array.from(this.transformations.keys());
  }
}

/**
 * Canonical execution order for the built-in transformations.
 *
 * Requested built-in transformations are always executed in this order,
 * regardless of the order they appear in the configuration. The constant is
 * exported so plugin authors and tooling can inspect where built-ins run
 * relative to each other; custom (plugin-registered) transformation types are
 * not part of this list and always run after the built-ins, in request order.
 */
export const CANONICAL_TRANSFORMATION_ORDER: readonly TransformationType[] = [
  TransformationType.ConvertToAscii,
  TransformationType.TrimLines,
  TransformationType.RemoveComments,
  TransformationType.Compress,
  TransformationType.RemoveModifiers,
  TransformationType.InvertAllow,
  TransformationType.Validate,
  TransformationType.ValidateAllowIp,
  TransformationType.Deduplicate,
  TransformationType.RemoveEmptyLines,
  TransformationType.InsertFinalNewLine,
];

const CANONICAL_ORDER_SET: ReadonlySet<TransformationType> = new Set(
  CANONICAL_TRANSFORMATION_ORDER,
);

/**
 * Executes an ordered sequence of named transformations over a rule array,
 * with optional lifecycle hooks, exclusion/inclusion pattern filtering, and
 * event emission.
 *
 * ## Execution order
 *
 * Requested built-in transformation types are always sorted into a fixed
 * canonical order (see `getOrderedTransformations`) regardless of the order
 * in which they appear in the configuration. This guarantees consistent,
 * reproducible output. Requested types outside the canonical set — such as
 * custom transformations registered through the plugin system — run after
 * the built-ins, in the order they were requested.
 *
 * ## Hook integration
 *
 * The pipeline holds an optional {@link TransformationHookManager}. On each
 * iteration the following sequence occurs:
 *
 * ```
 * emitProgress(…)
 * hookManager.executeBeforeHooks(beforeContext)   ← before transformation
 * try {
 *   rules = await transformation.execute(rules, ctx)
 * } catch (e) {
 *   await hookManager.executeErrorHooks(errorContext)
 *   throw e
 * }
 * hookManager.executeAfterHooks(afterContext)     ← after transformation
 * ```
 *
 * When no hooks are registered (`hookManager.hasHooks() === false`) the three
 * execute calls are skipped entirely. The default is a {@link NoOpHookManager}
 * so hook overhead is zero unless you pass a real hook manager.
 *
 * ## Event emission
 *
 * `ICompilerEvents.onTransformationStart` / `onTransformationComplete` events
 * are now emitted **through the hook system** rather than by direct calls
 * inside the loop. `FilterCompiler` and `WorkerCompiler` achieve this by
 * automatically registering a {@link createEventBridgeHook} on the hook
 * manager when `ICompilerEvents` listeners are present.
 */
export class TransformationPipeline {
  private readonly registry: TransformationRegistry;
  private readonly logger: ILogger;
  private readonly filterService: FilterService;
  private readonly eventEmitter: CompilerEventEmitter;
  private readonly hookManager: TransformationHookManager;

  /**
   * Creates a new `TransformationPipeline`.
   *
   * All parameters are optional; sane defaults are used when omitted:
   * - `registry` defaults to a new `TransformationRegistry` with all
   *   built-in transformations pre-registered.
   * - `logger` defaults to the module-level default logger.
   * - `eventEmitter` defaults to a `NoOpEventEmitter` (no-overhead singleton).
   * - `hookManager` defaults to a `NoOpHookManager` (no-overhead, skips all
   *   hook code paths).
   *
   * In practice you will rarely construct this directly — `FilterCompiler`
   * and `WorkerCompiler` build and configure the pipeline for you.
   *
   * @param registry - Transformation registry to look up transformations from
   * @param logger - Logger passed through to transformations during execution
   * @param eventEmitter - Emitter used for progress events (not transformation
   *   start/complete — those go through the hook manager)
   * @param hookManager - Lifecycle hook manager; defaults to `NoOpHookManager`
   */
  constructor(
    registry?: TransformationRegistry,
    logger?: ILogger,
    eventEmitter?: CompilerEventEmitter,
    hookManager?: TransformationHookManager,
  ) {
    this.logger = logger || defaultLogger;
    this.registry = registry || new TransformationRegistry(this.logger);
    this.filterService = new FilterService();
    this.eventEmitter = eventEmitter || createEventEmitter();

    // Auto-wire the event bridge when no explicit hookManager is provided
    // but the event emitter has onTransformationStart/Complete listeners.
    // This covers call sites like SourceCompiler that construct the pipeline
    // without knowing about the hook system (they only pass an eventEmitter).
    // We check specifically for transformation listeners — not the broader
    // hasListeners() — to avoid hook overhead for unrelated event types.
    this.hookManager = hookManager ?? (
      this.eventEmitter.hasTransformationListeners()
        ? new TransformationHookManager(createEventBridgeHook(this.eventEmitter))
        : new NoOpHookManager()
    );
  }

  /**
   * Apply exclusion/inclusion pattern filters and then execute the requested
   * transformations in canonical order.
   *
   * The method follows a three-phase approach:
   * 1. **Exclusions** — rules matching any exclusion pattern are removed.
   * 2. **Inclusions** — if inclusion patterns are configured, only rules
   *    matching at least one pattern are kept.
   * 3. **Transformations** — each requested transformation is executed in
   *    the fixed canonical order, with before/after/error hooks invoked
   *    around each execution.
   *
   * The method uses a `readonly string[]` internally during the
   * transformation loop to avoid unnecessary array copies; it only
   * materialises a mutable copy at the very end.
   *
   * @param rules - Input rule strings to transform
   * @param configuration - Source or global configuration (supplies exclusion/inclusion patterns)
   * @param transformations - Transformation types to apply (subset of `TransformationType`)
   * @returns A new mutable array of transformed rules
   */
  public async transform(
    rules: string[],
    configuration: IConfiguration | ISource,
    transformations?: TransformationType[],
  ): Promise<string[]> {
    const transformationList = transformations || [];
    let transformed = rules;

    // Apply exclusions first
    transformed = await this.applyExclusions(transformed, configuration);

    // Apply inclusions
    transformed = await this.applyInclusions(transformed, configuration);

    // Apply transformations in order
    const orderedTransformations = this.getOrderedTransformations(transformationList);
    const totalTransformations = orderedTransformations.length;

    // Use readonly array during transformation pipeline to avoid unnecessary copies
    let readonlyTransformed: readonly string[] = transformed;

    // Cache once so the hot loop never calls hasHooks() more than once per invocation.
    // When NoOpHookManager is in use this is always false, giving the else-branch
    // a true zero-overhead fast path (no context objects, no await, no timestamps).
    const hooksActive = this.hookManager.hasHooks();

    for (let i = 0; i < orderedTransformations.length; i++) {
      const type = orderedTransformations[i];
      const transformation = this.registry.get(type);
      if (transformation) {
        // Emit progress event (always, regardless of hook state)
        this.eventEmitter.emitProgress({
          phase: 'transformations',
          current: i + 1,
          total: totalTransformations,
          message: `Applying transformation: ${type}`,
        });

        if (hooksActive) {
          // ── Hook path ─────────────────────────────────────────────
          // inputCount and startTime are only needed here; keeping them
          // inside the guard avoids their allocation on the fast path.
          const inputCount = readonlyTransformed.length;
          const startTime = performance.now();
          const beforeContext = {
            name: type,
            type,
            ruleCount: inputCount,
            timestamp: Date.now(),
          };

          await this.hookManager.executeBeforeHooks(beforeContext);

          try {
            // Reuse the readonly array result for the next iteration to avoid unnecessary copies
            readonlyTransformed = await transformation.execute(readonlyTransformed, {
              configuration,
              logger: this.logger,
            });
          } catch (error) {
            // Error hooks are observers only — we await them then re-throw
            // the original error so the caller's error-handling is unaffected.
            await this.hookManager.executeErrorHooks({
              ...beforeContext,
              error: error instanceof Error ? error : new Error(String(error)),
            });
            throw error;
          }

          const durationMs = performance.now() - startTime;
          await this.hookManager.executeAfterHooks({
            name: type,
            type,
            ruleCount: readonlyTransformed.length,
            timestamp: Date.now(),
            inputCount,
            outputCount: readonlyTransformed.length,
            durationMs,
          });
        } else {
          // ── Fast path (no hooks) ──────────────────────────────────
          // Reuse the readonly array result for the next iteration to avoid unnecessary copies
          readonlyTransformed = await transformation.execute(readonlyTransformed, {
            configuration,
            logger: this.logger,
          });
        }
      }
    }

    // Convert to mutable array only at the end
    return Array.from(readonlyTransformed);
  }

  /**
   * Gets transformations in the correct execution order.
   *
   * Built-in transformation types are sorted into the fixed
   * {@link CANONICAL_TRANSFORMATION_ORDER}. Any requested type outside that list — e.g. a custom
   * type registered through the plugin system (see
   * {@link PluginTransformationWrapper}) — has no defined position in the
   * canonical order, so it is appended afterwards in the order it was
   * requested. Without this, plugin-registered transformations would be
   * silently dropped from the pipeline by the canonical-order filter.
   */
  private getOrderedTransformations(requested: TransformationType[]): TransformationType[] {
    const requestedSet = new Set(requested);
    const builtins = CANONICAL_TRANSFORMATION_ORDER.filter((type) => requestedSet.has(type));
    const extras = Array.from(new Set(requested.filter((type) => !CANONICAL_ORDER_SET.has(type))));
    return [...builtins, ...extras];
  }

  /**
   * Applies exclusion patterns.
   * Optimized to partition patterns by type for faster matching.
   */
  private async applyExclusions(
    rules: string[],
    configuration: IConfiguration | ISource,
  ): Promise<string[]> {
    return this.applyPatternFilter(
      rules,
      configuration.exclusions,
      configuration.exclusions_sources,
      'exclude',
    );
  }

  /**
   * Applies inclusion patterns.
   * Optimized to partition patterns by type for faster matching.
   */
  private async applyInclusions(
    rules: string[],
    configuration: IConfiguration | ISource,
  ): Promise<string[]> {
    return this.applyPatternFilter(
      rules,
      configuration.inclusions,
      configuration.inclusions_sources,
      'include',
    );
  }

  /**
   * Common pattern matching logic for exclusions and inclusions.
   * Partitions patterns by type for optimized matching.
   * Uses combined regex for better performance with many patterns.
   * @param rules - Rules to filter
   * @param patterns - Pattern strings
   * @param patternSources - URLs/paths to pattern files
   * @param mode - 'exclude' removes matching rules, 'include' keeps only matching rules
   */
  private async applyPatternFilter(
    rules: string[],
    patterns?: string[],
    patternSources?: string[],
    mode: 'exclude' | 'include' = 'exclude',
  ): Promise<string[]> {
    // Check if we have any patterns to apply
    if (
      (!patterns || patterns.length === 0) &&
      (!patternSources || patternSources.length === 0)
    ) {
      return rules;
    }

    const wildcards = await this.filterService.prepareWildcards(patterns, patternSources);

    if (wildcards.length === 0) {
      return rules;
    }

    const modeLabel = mode === 'exclude' ? 'exclusion' : 'inclusion';
    this.logger.info(`Filtering the list of rules using ${wildcards.length} ${modeLabel} rules`);

    // Partition patterns by type for optimized matching
    const plainPatterns: string[] = [];
    const regexWildcards: Wildcard[] = [];

    for (const w of wildcards) {
      if (w.isPlain) {
        plainPatterns.push(w.pattern);
      } else {
        regexWildcards.push(w);
      }
    }

    // Build combined regex for plain patterns (more efficient for many patterns)
    // Escape special regex characters and join with OR
    let combinedPlainRegex: RegExp | null = null;
    if (plainPatterns.length > 0) {
      const escaped = plainPatterns.map((p) => p.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));
      // Use non-capturing group for performance
      combinedPlainRegex = new RegExp(`(?:${escaped.join('|')})`);
    }

    // Create the appropriate filter predicate based on mode
    const matchesPattern = (rule: string): boolean => {
      // Fast path: check combined regex for plain patterns
      if (combinedPlainRegex && combinedPlainRegex.test(rule)) {
        return true;
      }

      // Slow path: individual regex/wildcard patterns
      for (const w of regexWildcards) {
        if (w.test(rule)) {
          return true;
        }
      }

      return false;
    };

    const filtered = mode === 'exclude'
      ? rules.filter((rule) => {
        const matches = matchesPattern(rule);
        if (matches) {
          this.logger.debug(`${rule} excluded by pattern`);
        }
        return !matches;
      })
      : rules.filter((rule) => {
        const matches = matchesPattern(rule);
        if (!matches) {
          this.logger.debug(`${rule} does not match inclusions list`);
        }
        return matches;
      });

    if (mode === 'exclude') {
      this.logger.info(
        `Excluded ${rules.length - filtered.length} rules. ${filtered.length} rules left.`,
      );
    } else {
      this.logger.info(`Included ${filtered.length} rules`);
    }

    return filtered;
  }
}
