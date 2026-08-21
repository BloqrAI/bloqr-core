/**
 * Orchestration Layer — CLI/config/chunking wrapper around the core
 * compiler-core engine (see `../index.ts`).
 *
 * Provides everything the old `rules-compiler-typescript` PoC contributed
 * on top of the compilation engine: multi-format config reading, chunking,
 * parallel compilation, hashing, structured logging, graceful shutdown,
 * and the interactive console / CLI.
 *
 * Import this module (or its `./console` / `./cli` friends via the
 * package's `orchestration` export subpath) for the CLI-wrapper API.
 * Import the package root for the bare compilation engine.
 *
 * @module orchestration
 * @packageDocumentation
 */

// Types
export type {
  CliOptions,
  CompileOptions,
  CompilerResult,
  ConfigurationFormat,
  ExtendedConfiguration,
  Logger,
  PlatformInfo,
  ValidationCallbacks,
  ValidationEvent,
  ValidationFinding,
  ValidationSeverity,
  VersionInfo,
} from './types.ts';

// Configuration reader
export { detectFormat, findDefaultConfig, readConfiguration, toJson } from './config-reader.ts';
export type { ReadConfigurationOptions } from './config-reader.ts';

// Compiler orchestration (chunking, hashing, parallel compilation on top of the core engine)
export {
  compileFilters,
  computeHash,
  copyToRulesDirectory,
  countRules,
  runCompiler,
  runRulesValidator,
  writeOutput,
} from './compiler.ts';
export type { CompilerOptions, ExtendedCompileOptions } from './compiler.ts';

// CLI
export { getVersionInfo, main, parseArgs, showHelp, showVersion } from './cli.ts';

// Console (Interactive Mode)
export {
  bold,
  colorStatus,
  ConsoleApplication,
  createKeyValueTable,
  createSpinner,
  createTable,
  dim,
  displayTable,
  formatBytes,
  formatElapsed,
  formatValue,
  generateBanner,
  runInteractive,
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
} from '../console/index.ts';
export type { ConsoleAppOptions } from '../console/index.ts';

// Logger
export {
  createDevelopmentLogger,
  createLogger,
  createProductionLogger,
  logger,
  LogLevel,
  parseLogLevel,
} from './logger.ts';
export type { ExtendedLogger, LoggerConfig } from './logger.ts';

// Errors
export {
  CompilationError,
  CompilationTimeoutError,
  CompilerError,
  ConfigNotFoundError,
  ConfigParseError,
  ConfigurationError,
  ErrorCode,
  ErrorSeverity,
  FileSystemError,
  isCompilerError,
  isRecoverable,
  PathTraversalError,
  ResourceLimitError,
  ShutdownError,
  ValidationError,
  wrapError,
} from './errors.ts';
export type { ErrorContext } from './errors.ts';

// Validation (config-schema validation, not rule-level validation)
export {
  assertValidConfiguration,
  checkFileSize,
  checkSourceCount,
  containsPathTraversal,
  DEFAULT_RESOURCE_LIMITS,
  sanitizePath,
  validateConfiguration,
  validateSourcePath,
  validateUrl,
} from './validation.ts';
export type { ResourceLimits, ValidationResult } from './validation.ts';

// Shutdown
export {
  createShutdownAwareAbortController,
  getShutdownHandler,
  initializeShutdownHandler,
  ShutdownHandler,
  withShutdownCheck,
} from './shutdown.ts';
export type { CleanupFn, ShutdownConfig } from './shutdown.ts';

// Timeout utilities
export { createTimeoutController, debounce, throttle, withRetry, withTimeout } from './timeout.ts';
export type { RetryConfig, TimeoutController } from './timeout.ts';

// Library API (high-level, builder-pattern wrapper)
export {
  AVAILABLE_TRANSFORMATIONS,
  BloqrCompiler,
  BloqrCompilerBuilder,
  ConfigurationBuilder,
  createBloqrCompiler,
  createConfiguration,
  // Deprecated pre-#331/#372 aliases - see lib/bloqr-compiler.ts
  createRulesCompiler,
  RulesCompiler,
  RulesCompilerBuilder,
  TRANSFORMATION_DESCRIPTIONS,
} from '../lib/index.ts';
export type {
  BloqrCompilerServiceOptions,
  CompileProgressEvent,
  CompileRunOptions,
  // Deprecated pre-#331/#372 alias - see lib/bloqr-compiler.ts
  RulesCompilerServiceOptions,
  SourceConfig,
  SourceType,
  Transformation,
} from '../lib/index.ts';
