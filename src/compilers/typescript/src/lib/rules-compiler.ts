/**
 * RulesCompiler - Main service class for programmatic library usage
 *
 * Provides a clean, high-level API for compiling AdGuard filter rules.
 * Use RulesCompilerBuilder for fluent configuration.
 *
 * @example
 * ```typescript
 * // Simple usage
 * const compiler = RulesCompiler.create();
 * const result = await compiler.compile({ configPath: 'config.yaml' });
 *
 * // With builder
 * const compiler = RulesCompiler.builder()
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
 * Options for the RulesCompiler service
 */
export interface RulesCompilerServiceOptions {
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
 * Main RulesCompiler service class
 *
 * This is the recommended entry point for programmatic usage.
 * Use the static `create()` or `builder()` methods to instantiate.
 */
export class RulesCompiler {
  private readonly options: Required<RulesCompilerServiceOptions>;
  private readonly logger: Logger;

  /**
   * Create a RulesCompiler with options
   * @param options Service options
   */
  constructor(options: RulesCompilerServiceOptions = {}) {
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
   * Create a RulesCompiler with default options
   */
  static create(): RulesCompiler {
    return new RulesCompiler();
  }

  /**
   * Create a RulesCompilerBuilder for fluent configuration
   */
  static builder(): RulesCompilerBuilder {
    return new RulesCompilerBuilder();
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
  get serviceOptions(): Readonly<Required<RulesCompilerServiceOptions>> {
    return this.options;
  }
}

/**
 * Builder for RulesCompiler with fluent configuration
 *
 * @example
 * ```typescript
 * const compiler = RulesCompiler.builder()
 *   .withTimeout(60000)
 *   .withMaxOutputSize(50 * 1024 * 1024)
 *   .withLogger(customLogger)
 *   .withDebug(true)
 *   .build();
 * ```
 */
export class RulesCompilerBuilder {
  private options: RulesCompilerServiceOptions = {};

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
   * Build the RulesCompiler instance
   */
  build(): RulesCompiler {
    return new RulesCompiler(this.options);
  }
}

/**
 * Convenience function to create a RulesCompiler
 */
export function createRulesCompiler(options?: RulesCompilerServiceOptions): RulesCompiler {
  return new RulesCompiler(options);
}
