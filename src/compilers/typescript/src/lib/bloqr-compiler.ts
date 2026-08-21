/**
 * BloqrCompiler - Main service class for programmatic library usage
 *
 * Provides a clean, high-level API for compiling AdGuard filter rules.
 * Use BloqrCompilerBuilder for fluent configuration.
 *
 * @example
 * ```typescript
 * // Simple usage
 * const compiler = BloqrCompiler.create();
 * const result = await compiler.compile({ configPath: 'config.yaml' });
 *
 * // With builder
 * const compiler = BloqrCompiler.builder()
 *   .withTimeout(60000)
 *   .withLogger(customLogger)
 *   .build();
 *
 * // Validate configuration
 * const validation = await compiler.validate('config.yaml');
 * if (validation.valid) {
 *   const result = await compiler.compile({ configPath: 'config.yaml' });
 * }
 * ```
 */

import type { IConfiguration } from '../index.ts';
import type {
  CompilerResult,
  ConfigurationFormat,
  ExtendedConfiguration,
  Logger,
  VersionInfo,
} from '../orchestration/types.ts';
import { findDefaultConfig, readConfiguration, toJson } from '../orchestration/config-reader.ts';
import { compileFilters, computeHash, countRules, runCompiler } from '../orchestration/compiler.ts';
import {
  DEFAULT_RESOURCE_LIMITS,
  type ResourceLimits,
  validateConfiguration,
  type ValidationResult,
} from '../orchestration/validation.ts';
import { createLogger } from '../orchestration/logger.ts';
import { getVersionInfo } from '../orchestration/cli.ts';

/**
 * Options for the BloqrCompiler service
 */
export interface BloqrCompilerServiceOptions {
  /** Default compilation timeout in milliseconds */
  timeoutMs?: number;
  /** Maximum output file size in bytes */
  maxOutputSize?: number;
  /** Resource limits configuration */
  resourceLimits?: Partial<ResourceLimits>;
  /** Logger instance */
  logger?: Logger;
  /** Enable debug logging */
  debug?: boolean;
}

/**
 * Options for a single compilation run
 */
export interface CompileRunOptions {
  /** Path to configuration file */
  configPath: string;
  /** Force configuration format */
  format?: ConfigurationFormat;
  /** Path to output file */
  outputPath?: string;
  /** Copy output to rules directory */
  copyToRules?: boolean;
  /** Custom rules directory path */
  rulesDirectory?: string;
  /** Progress callback */
  onProgress?: (event: CompileProgressEvent) => void;
}

/**
 * Progress event during compilation
 */
export interface CompileProgressEvent {
  /** Progress phase */
  phase: 'loading' | 'validating' | 'compiling' | 'writing' | 'copying' | 'complete';
  /** Progress message */
  message: string;
  /** Percentage complete (0-100) */
  percent?: number;
}

/**
 * Main BloqrCompiler service class
 *
 * This is the recommended entry point for programmatic usage.
 * Use the static `create()` or `builder()` methods to instantiate.
 */
export class BloqrCompiler {
  private readonly options: Required<BloqrCompilerServiceOptions>;
  private readonly logger: Logger;

  /**
   * Create a BloqrCompiler with options
   * @param options Service options
   */
  constructor(options: BloqrCompilerServiceOptions = {}) {
    this.options = {
      timeoutMs: options.timeoutMs ?? DEFAULT_RESOURCE_LIMITS.compilationTimeoutMs,
      maxOutputSize: options.maxOutputSize ?? DEFAULT_RESOURCE_LIMITS.maxOutputFileSize,
      resourceLimits: { ...DEFAULT_RESOURCE_LIMITS, ...options.resourceLimits },
      logger: options.logger ?? createLogger(options.debug ?? false),
      debug: options.debug ?? false,
    };
    this.logger = this.options.logger;
  }

  /**
   * Create a BloqrCompiler with default options
   */
  static create(): BloqrCompiler {
    return new BloqrCompiler();
  }

  /**
   * Create a BloqrCompilerBuilder for fluent configuration
   */
  static builder(): BloqrCompilerBuilder {
    return new BloqrCompilerBuilder();
  }

  /**
   * Compile filter rules from a configuration file
   * @param options Compilation options
   * @returns Compilation result
   */
  async compile(options: CompileRunOptions): Promise<CompilerResult> {
    const { onProgress } = options;

    onProgress?.({ phase: 'loading', message: 'Loading configuration...', percent: 0 });

    const result = await runCompiler({
      configPath: options.configPath,
      format: options.format,
      outputPath: options.outputPath,
      copyToRules: options.copyToRules,
      rulesDirectory: options.rulesDirectory,
      logger: this.logger,
      timeoutMs: this.options.timeoutMs,
      maxOutputSize: this.options.maxOutputSize,
    });

    onProgress?.({ phase: 'complete', message: 'Compilation complete', percent: 100 });

    return result;
  }

  /**
   * Compile filter rules from an in-memory configuration
   * @param config Configuration object
   * @returns Array of compiled rule strings
   */
  compileFromConfig(config: IConfiguration): Promise<string[]> {
    return compileFilters(config, this.logger, {
      timeoutMs: this.options.timeoutMs,
      maxOutputSize: this.options.maxOutputSize,
    });
  }

  /**
   * Read and parse a configuration file
   * @param configPath Path to configuration file
   * @param format Optional format override
   * @returns Parsed configuration
   */
  readConfig(configPath: string, format?: ConfigurationFormat): ExtendedConfiguration {
    return readConfiguration(configPath, format, this.logger);
  }

  /**
   * Find the default configuration file in the current directory
   * @returns Path to default config file, or undefined if not found
   */
  findDefaultConfig(): string | undefined {
    return findDefaultConfig();
  }

  /**
   * Validate a configuration file
   * @param configPath Path to configuration file
   * @param format Optional format override
   * @returns Validation result
   */
  validate(configPath: string, format?: ConfigurationFormat): ValidationResult {
    const config = this.readConfig(configPath, format);
    return validateConfiguration(config);
  }

  /**
   * Validate a configuration object
   * @param config Configuration object
   * @returns Validation result
   */
  validateConfig(config: unknown): ValidationResult {
    return validateConfiguration(config);
  }

  /**
   * Get version information for the compiler and runtime
   * @returns Version information
   */
  getVersionInfo(): VersionInfo {
    return getVersionInfo();
  }

  /**
   * Count rules in a compiled output file
   * @param filePath Path to output file
   * @returns Number of rules
   */
  countRules(filePath: string): number {
    return countRules(filePath);
  }

  /**
   * Compute SHA-384 hash of a file
   * @param filePath Path to file
   * @returns Hex-encoded hash
   */
  computeHash(filePath: string): string {
    return computeHash(filePath);
  }

  /**
   * Convert a configuration to JSON string
   * @param config Configuration object
   * @returns JSON string
   */
  toJson(config: ExtendedConfiguration): string {
    return toJson(config);
  }

  /**
   * Get the configured logger
   */
  get log(): Logger {
    return this.logger;
  }

  /**
   * Get the service options
   */
  get serviceOptions(): Readonly<Required<BloqrCompilerServiceOptions>> {
    return this.options;
  }
}

/**
 * Builder for BloqrCompiler with fluent configuration
 *
 * @example
 * ```typescript
 * const compiler = BloqrCompiler.builder()
 *   .withTimeout(60000)
 *   .withMaxOutputSize(50 * 1024 * 1024)
 *   .withLogger(customLogger)
 *   .withDebug(true)
 *   .build();
 * ```
 */
export class BloqrCompilerBuilder {
  private options: BloqrCompilerServiceOptions = {};

  /**
   * Set compilation timeout
   * @param ms Timeout in milliseconds
   */
  withTimeout(ms: number): this {
    this.options.timeoutMs = ms;
    return this;
  }

  /**
   * Set maximum output file size
   * @param bytes Maximum size in bytes
   */
  withMaxOutputSize(bytes: number): this {
    this.options.maxOutputSize = bytes;
    return this;
  }

  /**
   * Set resource limits
   * @param limits Resource limits configuration
   */
  withResourceLimits(limits: Partial<ResourceLimits>): this {
    this.options.resourceLimits = { ...this.options.resourceLimits, ...limits };
    return this;
  }

  /**
   * Set a custom logger
   * @param logger Logger instance
   */
  withLogger(logger: Logger): this {
    this.options.logger = logger;
    return this;
  }

  /**
   * Enable or disable debug logging
   * @param enabled Enable debug mode
   */
  withDebug(enabled = true): this {
    this.options.debug = enabled;
    return this;
  }

  /**
   * Build the BloqrCompiler instance
   */
  build(): BloqrCompiler {
    return new BloqrCompiler(this.options);
  }
}

/**
 * Convenience function to create a BloqrCompiler
 */
export function createBloqrCompiler(options?: BloqrCompilerServiceOptions): BloqrCompiler {
  return new BloqrCompiler(options);
}

// --- Deprecated aliases (pre-#331/#372 "Rules"-branded names) -------------
// Kept for one release cycle so existing @bloqr/compiler-core consumers don't
// break on upgrade; not part of the internal source going forward. Remove in
// the next major version per docs/architecture/versioning-strategy.md.

/**
 * @deprecated Use {@link BloqrCompiler} instead. Will be removed in the next major version.
 */
export const RulesCompiler = BloqrCompiler;

/**
 * @deprecated Use {@link BloqrCompiler} instead. Will be removed in the next major version.
 */
export type RulesCompiler = BloqrCompiler;

/**
 * @deprecated Use {@link BloqrCompilerBuilder} instead. Will be removed in the next major version.
 */
export const RulesCompilerBuilder = BloqrCompilerBuilder;

/**
 * @deprecated Use {@link BloqrCompilerBuilder} instead. Will be removed in the next major version.
 */
export type RulesCompilerBuilder = BloqrCompilerBuilder;

/**
 * @deprecated Use {@link BloqrCompilerServiceOptions} instead. Will be removed in the next major version.
 */
export type RulesCompilerServiceOptions = BloqrCompilerServiceOptions;

/**
 * @deprecated Use {@link createBloqrCompiler} instead. Will be removed in the next major version.
 */
export function createRulesCompiler(options?: BloqrCompilerServiceOptions): BloqrCompiler {
  return createBloqrCompiler(options);
}
