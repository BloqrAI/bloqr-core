/**
 * @module adblock-compiler-core
 * Main entry point for the `@bloqr/compiler-core` library.
 *
 * This is a minimal, dependency-free filter-list compilation engine —
 * extracted from `bloqr-compiler`'s core, with `@adguard/agtree` and all
 * commercial/Cloudflare-specific features removed. See
 * `docs/compiler-architecture.md` for the full story of how this relates
 * to the commercial `@bloqr/compiler` product.
 *
 * @example Install and compile a list
 * ```ts
 * import { compile } from '@bloqr/compiler-core';
 *
 * const result = await compile({
 *   sources: [{ url: 'https://example.com/list.txt' }],
 *   transformations: ['RemoveComments', 'Deduplicate'],
 * });
 * console.log(result.rules.join('\n'));
 * ```
 *
 * @packageDocumentation
 */
// Version information
export { PACKAGE_INFO, PACKAGE_NAME, USER_AGENT, VERSION } from './version.ts';

// Configuration constants
export {
  COMPILATION_DEFAULTS,
  DEFAULTS,
  HealthStatus,
  NETWORK_DEFAULTS,
  OutputFormat,
  PREPROCESSOR_DEFAULTS,
  RuleType,
  VALIDATION_DEFAULTS,
  WORKER_DEFAULTS,
} from './config/index.ts';

// Types
export * from './types/index.ts';

// Utils
export { RuleUtils, StringUtils, TldUtils, Wildcard } from './utils/index.ts';
export type { BenchmarkResult, CompilationMetrics, ParsedHost } from './utils/index.ts';

// Logging
export {
  createLogger,
  createLoggerFromEnv,
  Logger,
  logger,
  LogLevel,
  parseModuleOverrides,
  silentLogger,
  StructuredLogger,
} from './utils/index.ts';
export type { LoggerOptions, ModuleOverrides } from './utils/index.ts';

// Error utilities
export {
  BaseError,
  CompilationError,
  ErrorUtils,
  NetworkError,
  SourceError,
  ValidationError,
} from './utils/index.ts';

// Circuit breaker for resilience
export { CircuitBreaker, CircuitBreakerOpenError, CircuitBreakerState } from './utils/index.ts';
export type { CircuitBreakerOptions } from './utils/index.ts';

// Boolean expression parser
export { evaluateBooleanExpression, getKnownPlatforms, isKnownPlatform } from './utils/index.ts';

// Event system for observability
export { CompilerEventEmitter, createEventEmitter, NoOpEventEmitter } from './utils/index.ts';

// Minimal diagnostics/tracing seam (no-op by default; see diagnostics/index.ts)
export {
  createChildContext,
  createNoOpContext,
  createTracingContext,
  DiagnosticsCollector,
  NoOpDiagnosticsCollector,
  traceAsync,
  TraceCategory,
  TraceSeverity,
  traceSync,
} from './diagnostics/index.ts';
export type {
  AnyDiagnosticEvent,
  CacheEvent,
  DiagnosticEvent,
  IDiagnosticsCollector,
  NetworkEvent,
  OperationCompleteEvent,
  OperationErrorEvent,
  OperationStartEvent,
  PerformanceMetricEvent,
  TracingContext,
  TracingContextOptions,
} from './diagnostics/index.ts';

// Downloader
export { FilterDownloader } from './downloader/index.ts';
export type { CustomDownloader, DownloaderOptions } from './downloader/index.ts';

// Configuration and Validation Schemas
export {
  AdblockRuleSchema,
  BatchRequestAsyncSchema,
  BatchRequestSchema,
  BatchRequestSyncSchema,
  BenchmarkMetricsSchema,
  CliArgumentsSchema,
  CompilationResultSchema,
  CompileRequestSchema,
  ConfigurationSchema,
  ConfigurationValidator,
  EnvironmentSchema,
  EtcHostsRuleSchema,
  HttpFetcherOptionsSchema,
  PlatformCompilerOptionsSchema,
  PrioritySchema,
  SourceSchema,
  ValidationErrorSchema,
  ValidationErrorTypeSchema,
  ValidationReportSchema,
  ValidationResultSchema,
  ValidationSeveritySchema,
  WorkerCompilationResultSchema,
} from './configuration/index.ts';
export type {
  AdblockRule,
  BenchmarkMetrics,
  CliArguments,
  CompilationResultOutput,
  Environment,
  EtcHostsRule,
  Priority,
  WorkerCompilationResultOutput,
} from './configuration/index.ts';

// Transformations
export {
  AsyncTransformation,
  CANONICAL_TRANSFORMATION_ORDER,
  CompressTransformation,
  ConvertToAsciiTransformation,
  DeduplicateTransformation,
  ExcludeTransformation,
  IncludeTransformation,
  InsertFinalNewLineTransformation,
  InvertAllowTransformation,
  RemoveCommentsTransformation,
  RemoveEmptyLinesTransformation,
  RemoveModifiersTransformation,
  SyncTransformation,
  Transformation,
  TransformationPipeline,
  TransformationRegistry,
  TrimLinesTransformation,
  ValidateAllowIpTransformation,
  ValidateTransformation,
} from './transformations/index.ts';

// Transformation hooks
export {
  createEventBridgeHook,
  createLoggingHook,
  createMetricsHook,
  NoOpHookManager,
  TransformationHookManager,
} from './transformations/index.ts';
export type {
  AfterTransformHook,
  BeforeTransformHook,
  TransformationHookConfig,
  TransformationHookContext,
  TransformErrorHook,
} from './transformations/index.ts';

// Services
export { FilterService } from './services/index.ts';

// Compiler
export { compile, FilterCompiler, SourceCompiler } from './compiler/index.ts';
export type { CompilationResult, FilterCompilerOptions } from './compiler/index.ts';

// Platform abstraction layer (Deno-native fetchers)
export {
  CompositeFetcher,
  HttpFetcher,
  PlatformDownloader,
  PreFetchedContentFetcher,
} from './platform/index.ts';
export type {
  IContentFetcher,
  IHttpFetcherOptions,
  IPlatformCompilerOptions,
  PlatformDownloaderOptions,
  PreFetchedContent,
} from './platform/index.ts';

// Incremental compilation
export { IncrementalCompiler, MemoryCacheStorage } from './compiler/IncrementalCompiler.ts';
export type {
  ICacheStorage,
  IncrementalCompilationResult,
  IncrementalCompilerOptions,
  SourceCacheEntry,
} from './compiler/IncrementalCompiler.ts';

// Header generation
export { HeaderGenerator } from './compiler/HeaderGenerator.ts';
export type { HeaderOptions } from './compiler/HeaderGenerator.ts';

// Output formatters
export {
  AdblockFormatter,
  BaseFormatter,
  createFormatter,
  DnsmasqFormatter,
  DoHFormatter,
  formatOutput,
  HostnameListFormatter,
  HostsFormatter,
  JsonFormatter,
  PiHoleFormatter,
  UnboundFormatter,
} from './formatters/index.ts';
export type {
  FormatterConstructor,
  FormatterOptions,
  FormatterResult,
} from './formatters/index.ts';

// Default export for backward compatibility
import { compile as compileFunc } from './compiler/index.ts';
export default compileFunc;
