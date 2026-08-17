/**
 * `ConfigurationManager` – unified entry-point for loading, merging, and
 * validating compiler configurations.
 *
 * ## Layered precedence (lowest → highest)
 * 1. Caller-supplied sources in construction order
 * 2. `EnvConfigurationSource`  (ADBLOCK_CONFIG_* env vars, opt-out via options)
 * 3. Any extra sources appended after construction
 *
 * ## Usage
 * ```ts
 * // From a JSON file (CLI)
 * const cfg = await ConfigurationManager.fromFile('./blocklist.json').load();
 *
 * // From a plain object (programmatic / tests)
 * const cfg = await ConfigurationManager.fromObject({ name: 'Test', sources: [] }).load();
 *
 * // From CLI --input flags
 * const cfg = await ConfigurationManager.fromCliArgs(['https://...'], 'hosts').load();
 *
 * // Multi-layer: file then inline override
 * const mgr = ConfigurationManager.fromSources([
 *   new FileConfigurationSource('./base.json'),
 *   new OverrideConfigurationSource('{"name":"CI Build"}'),
 * ], { applyEnvOverrides: false });
 * const cfg = await mgr.load();
 * ```
 *
 * @module
 */

import type { IConfiguration, IFileSystem } from '../types/index.ts';
import { ConfigurationSchema } from './schemas.ts';
import { VALIDATION_DEFAULTS } from '../config/defaults.ts';
import type { ZodError } from 'zod';
import {
  CliConfigurationSource,
  EnvConfigurationSource,
  FileConfigurationSource,
  type IConfigurationSource,
  ObjectConfigurationSource,
  OverrideConfigurationSource,
} from './sources/index.ts';

// ── Exported errors ────────────────────────────────────────────────────────────

/**
 * Thrown by `ConfigurationManager.load()` when the merged configuration
 * fails Zod schema validation.
 */
export class ConfigurationValidationError extends Error {
  constructor(
    message: string,
    /** The underlying Zod error with per-field issue details. */
    public readonly zodError: ZodError,
  ) {
    super(message);
    this.name = 'ConfigurationValidationError';
  }
}

// ── Options ────────────────────────────────────────────────────────────────────

/** Options controlling `ConfigurationManager` behaviour. */
export interface ConfigurationManagerOptions {
  /**
   * When `false`, `ADBLOCK_CONFIG_*` environment variables are NOT applied.
   * Defaults to `true`.
   */
  applyEnvOverrides?: boolean;
  /**
   * When `false`, the `MAX_SOURCES` truncation is skipped.
   * Defaults to `true`.
   */
  enforceSourceLimit?: boolean;
  /**
   * When `false`, the `MAX_EXCLUSIONS` truncation is skipped.
   * Defaults to `true`.
   */
  enforceExclusionLimit?: boolean;
}

// ── ConfigurationManager ───────────────────────────────────────────────────────

/**
 * Orchestrates: load → deep-merge → enforce limits → Zod-validate.
 *
 * The manager is immutable after construction.  Call `load()` to produce a
 * fully-resolved `IConfiguration`.  `load()` may be called multiple times
 * (e.g. to reload on file change); each call is independent.
 */
export class ConfigurationManager {
  private resolvedConfig: IConfiguration | null = null;
  private lastError: ZodError | null = null;

  constructor(
    private readonly sources: IConfigurationSource[],
    private readonly options: ConfigurationManagerOptions = {},
  ) {}

  // ── Core load ──────────────────────────────────────────────────────────────

  /**
   * Loads all sources, deep-merges them, enforces system limits, then
   * validates through the Zod `ConfigurationSchema`.
   *
   * @throws {ConfigurationValidationError} when the merged result is invalid.
   * @returns The resolved `IConfiguration`.
   */
  async load(): Promise<IConfiguration> {
    const applyEnv = this.options.applyEnvOverrides !== false;

    let pipeline: IConfigurationSource[];
    if (applyEnv) {
      // Env overrides sit between base sources and any explicit override sources so that
      // OverrideConfigurationSource / CliConfigurationSource take the highest precedence.
      // Both source types validate their input at construction time (OverrideConfigurationSource
      // requires a non-null JSON object; CliConfigurationSource is built from validated CLI args).
      const baseSources: IConfigurationSource[] = [];
      const overrideSources: IConfigurationSource[] = [];
      for (const source of this.sources) {
        if (
          source instanceof OverrideConfigurationSource || source instanceof CliConfigurationSource
        ) {
          overrideSources.push(source);
        } else {
          baseSources.push(source);
        }
      }
      pipeline = [...baseSources, new EnvConfigurationSource(), ...overrideSources];
    } else {
      pipeline = [...this.sources];
    }

    // Collect partials from all sources in pipeline order
    const partials: Partial<IConfiguration>[] = [];
    for (const source of pipeline) {
      partials.push(await source.load());
    }

    // Deep-merge: scalars last-wins; arrays last fully replaces
    const merged = ConfigurationManager.deepMerge(partials);

    // Enforce limits
    if (this.options.enforceSourceLimit !== false && Array.isArray(merged.sources)) {
      if (merged.sources.length > VALIDATION_DEFAULTS.MAX_SOURCES) {
        merged.sources = merged.sources.slice(0, VALIDATION_DEFAULTS.MAX_SOURCES);
      }
    }
    if (this.options.enforceExclusionLimit !== false && Array.isArray(merged.exclusions)) {
      if (merged.exclusions.length > VALIDATION_DEFAULTS.MAX_EXCLUSIONS) {
        merged.exclusions = merged.exclusions.slice(0, VALIDATION_DEFAULTS.MAX_EXCLUSIONS);
      }
    }

    // Zod validation
    const result = ConfigurationSchema.safeParse(merged);
    if (!result.success) {
      this.lastError = result.error;
      const issues = result.error.issues
        .map((i) => `  ${i.path.join('.')}: ${i.message}`)
        .join('\n');
      throw new ConfigurationValidationError(`Invalid configuration:\n${issues}`, result.error);
    }

    this.lastError = null;
    this.resolvedConfig = result.data as IConfiguration;
    return this.resolvedConfig;
  }

  // ── Accessors ──────────────────────────────────────────────────────────────

  /**
   * Returns the last successfully resolved configuration, or `null` if
   * `load()` has not succeeded yet.
   */
  getEffectiveConfig(): IConfiguration | null {
    return this.resolvedConfig;
  }

  /**
   * Returns the Zod error from the last failed `load()`, or `null` if the
   * last `load()` succeeded (or has not been called).
   */
  getValidationErrors(): ZodError | null {
    return this.lastError;
  }

  // ── Static factories ───────────────────────────────────────────────────────

  /**
   * Creates a manager that loads a single JSON config file.
   *
   * @param path        - Path to the JSON config file.
   * @param fileSystem  - Optional injected `IFileSystem` (for testing).
   * @param options     - Manager behaviour options.
   */
  static fromFile(
    path: string,
    fileSystem?: IFileSystem,
    options?: ConfigurationManagerOptions,
  ): ConfigurationManager {
    return new ConfigurationManager(
      [new FileConfigurationSource(path, fileSystem)],
      options,
    );
  }

  /**
   * Creates a manager from a plain object (programmatic / test use).
   *
   * @param config  - Partial configuration to use as the base layer.
   * @param options - Manager behaviour options.
   */
  static fromObject(
    config: Partial<IConfiguration>,
    options?: ConfigurationManagerOptions,
  ): ConfigurationManager {
    return new ConfigurationManager(
      [new ObjectConfigurationSource(config)],
      options,
    );
  }

  /**
   * Creates a manager from CLI `--input` flags.
   *
   * @param inputs    - Array of source URLs or file paths.
   * @param inputType - Source type (`'hosts'` or `'adblock'`).
   * @param options   - Manager behaviour options.
   */
  static fromCliArgs(
    inputs: string[],
    inputType?: string,
    options?: ConfigurationManagerOptions,
  ): ConfigurationManager {
    return new ConfigurationManager(
      [new CliConfigurationSource(inputs, inputType)],
      options,
    );
  }

  /**
   * Creates a manager from an explicit list of sources.
   * Sources are merged in the order they appear in the array.
   *
   * @param sources - Ordered source pipeline.
   * @param options - Manager behaviour options.
   */
  static fromSources(
    sources: IConfigurationSource[],
    options?: ConfigurationManagerOptions,
  ): ConfigurationManager {
    return new ConfigurationManager(sources, options);
  }

  // ── Merge helpers ──────────────────────────────────────────────────────────

  /**
   * Deep-merges an ordered list of `Partial<IConfiguration>` objects.
   *
   * Merge semantics:
   * - Scalar values: last-defined wins.
   * - Arrays (`sources`, `transformations`, `exclusions`, …): last-defined
   *   fully replaces (no concatenation).
   * - `undefined` values from later sources do **not** override earlier ones.
   */
  static deepMerge(partials: Partial<IConfiguration>[]): Partial<IConfiguration> {
    const result: Partial<IConfiguration> = {};
    for (const partial of partials) {
      if (
        partial === null || partial === undefined || typeof partial !== 'object' ||
        Array.isArray(partial)
      ) {
        continue;
      }
      for (const [key, value] of Object.entries(partial)) {
        if (value !== undefined) {
          (result as Record<string, unknown>)[key] = value;
        }
      }
    }
    return result;
  }

  /**
   * Validates a plain object against the `ConfigurationSchema` without
   * loading any sources.  Returns `{ valid: true, config }` on success or
   * `{ valid: false, errors }` on failure.
   *
   * Useful for API endpoints that want to validate user-supplied JSON without
   * the full load pipeline.
   */
  static validateOnly(
    input: unknown,
  ): { valid: true; config: IConfiguration } | { valid: false; errors: ZodError } {
    const result = ConfigurationSchema.safeParse(input);
    if (result.success) {
      return { valid: true, config: result.data as IConfiguration };
    }
    return { valid: false, errors: result.error };
  }

  /**
   * Resolves a configuration from a plain object, applying defaults and
   * limit enforcement, without constructing a full manager instance.
   *
   * Returns the resolved `IConfiguration` or throws `ConfigurationValidationError`.
   */
  static async resolveObject(
    input: unknown,
    override?: string,
    options?: ConfigurationManagerOptions,
  ): Promise<IConfiguration> {
    const sources: IConfigurationSource[] = [
      new ObjectConfigurationSource(input as Partial<IConfiguration>),
    ];
    if (override) {
      sources.push(new OverrideConfigurationSource(override));
    }
    return new ConfigurationManager(sources, options).load();
  }
}

// Re-export source types so consumers can import everything from one place.
export {
  CliConfigurationSource,
  EnvConfigurationSource,
  FileConfigurationSource,
  type IConfigurationSource,
  ObjectConfigurationSource,
  OverrideConfigurationSource,
} from './sources/index.ts';
export type { ConfigurationManagerOptions as ManagerOptions };
