// deno-lint-ignore-file no-console
/**
 * Deno-native logger implementation
 * Replaces consola for Deno compatibility
 */

import type { ILogger } from '../types/index.ts';

/**
 * Log levels for filtering output
 */
export enum LogLevel {
  Trace = -1,
  Debug = 0,
  Info = 1,
  Warn = 2,
  Error = 3,
  Silent = 4,
}

/**
 * ANSI color codes for terminal output
 */
const Colors = {
  reset: '\x1b[0m',
  dim: '\x1b[2m',
  blue: '\x1b[34m',
  yellow: '\x1b[33m',
  red: '\x1b[31m',
  green: '\x1b[32m',
  cyan: '\x1b[36m',
} as const;

/**
 * Module-specific log level overrides
 */
export interface ModuleOverrides {
  /** Maps a module name to the {@linkcode LogLevel} that should apply to loggers created for it. */
  [moduleName: string]: LogLevel;
}

/**
 * Logger configuration options
 */
export interface LoggerOptions {
  /** Minimum level to emit; messages below this level are suppressed. Defaults to `LogLevel.Info`. */
  level?: LogLevel;
  /** String prepended to every log line, before the timestamp/level tags. */
  prefix?: string;
  /** Whether to prefix each log line with an ISO-8601 timestamp. Defaults to `false`. */
  timestamps?: boolean;
  /** Whether to colorize output with ANSI escape codes. Defaults to `true`. */
  colors?: boolean;
  /** Enable structured JSON output for production observability */
  structured?: boolean;
  /** Correlation ID for grouping related logs */
  correlationId?: string;
  /** Trace ID for distributed tracing */
  traceId?: string;
  /** Module name for this logger instance */
  module?: string;
  /** Module-specific log level overrides */
  moduleOverrides?: ModuleOverrides;
}

/**
 * Creates a formatted timestamp string
 */
function getTimestamp(): string {
  return new Date().toISOString();
}

/**
 * Console-based logger implementation for Deno
 */
export class Logger implements ILogger {
  /** The effective minimum level for this logger instance, after resolving module overrides. */
  protected level: LogLevel;
  /** String prepended to every log line emitted by this instance. */
  protected prefix: string;
  /** Whether ISO-8601 timestamps are prefixed to log lines. */
  protected timestamps: boolean;
  /** Whether ANSI color codes are applied to log output. */
  protected colors: boolean;
  /** Module name this logger instance was created for, used to resolve {@linkcode moduleOverrides}. */
  protected module: string;
  /** Per-module {@linkcode LogLevel} overrides applied when resolving this logger's effective level. */
  protected moduleOverrides: ModuleOverrides;

  /**
   * Creates a new `Logger`.
   * @param options Logger configuration; unset fields fall back to sensible defaults
   * (`LogLevel.Info`, no prefix, no timestamps, colors enabled).
   */
  constructor(options: LoggerOptions = {}) {
    this.module = options.module ?? '';
    this.moduleOverrides = options.moduleOverrides ?? {};
    // Resolve effective log level based on module overrides
    this.level = this.resolveLogLevel(options.level ?? LogLevel.Info);
    this.prefix = options.prefix ?? '';
    this.timestamps = options.timestamps ?? false;
    this.colors = options.colors ?? true;
  }

  /**
   * Resolves the effective log level for this logger based on module overrides
   */
  private resolveLogLevel(defaultLevel: LogLevel): LogLevel {
    // If this logger has a module name and there's an override for it, use the override
    if (this.module && this.module.length > 0 && this.moduleOverrides[this.module] !== undefined) {
      return this.moduleOverrides[this.module];
    }
    return defaultLevel;
  }

  /**
   * Formats a log message with optional prefix and timestamp
   */
  private format(levelName: string, color: string, message: string): string {
    const parts: string[] = [];

    if (this.timestamps) {
      parts.push(
        this.colors ? `${Colors.dim}${getTimestamp()}${Colors.reset}` : getTimestamp(),
      );
    }

    if (this.prefix) {
      parts.push(
        this.colors ? `${Colors.cyan}[${this.prefix}]${Colors.reset}` : `[${this.prefix}]`,
      );
    }

    parts.push(
      this.colors ? `${color}${levelName}${Colors.reset}` : levelName,
    );

    parts.push(message);

    return parts.join(' ');
  }

  /**
   * Logs a trace message (most verbose)
   */
  trace(message: string): void {
    if (this.level <= LogLevel.Trace) {
      console.debug(this.format('TRACE', Colors.dim, message));
    }
  }

  /**
   * Logs a debug message
   */
  debug(message: string): void {
    if (this.level <= LogLevel.Debug) {
      console.debug(this.format('DEBUG', Colors.dim, message));
    }
  }

  /**
   * Logs an info message
   */
  info(message: string): void {
    if (this.level <= LogLevel.Info) {
      console.info(this.format('INFO', Colors.blue, message));
    }
  }

  /**
   * Logs a warning message
   */
  warn(message: string): void {
    if (this.level <= LogLevel.Warn) {
      console.warn(this.format('WARN', Colors.yellow, message));
    }
  }

  /**
   * Logs an error message
   */
  error(message: string): void {
    if (this.level <= LogLevel.Error) {
      console.error(this.format('ERROR', Colors.red, message));
    }
  }

  /**
   * Logs a success message (info level)
   */
  success(message: string): void {
    if (this.level <= LogLevel.Info) {
      console.info(this.format('SUCCESS', Colors.green, message));
    }
  }

  /**
   * Creates a child logger with an additional prefix
   */
  child(prefix: string): Logger {
    const childPrefix = this.prefix ? `${this.prefix}:${prefix}` : prefix;
    return new Logger({
      level: this.level,
      prefix: childPrefix,
      timestamps: this.timestamps,
      colors: this.colors,
      module: this.module,
      moduleOverrides: this.moduleOverrides,
    });
  }

  /**
   * Sets the log level
   */
  setLevel(level: LogLevel): void {
    this.level = level;
  }

  /**
   * Gets the module name for this logger
   */
  getModule(): string {
    return this.module;
  }

  /**
   * Gets the module overrides configuration
   */
  getModuleOverrides(): ModuleOverrides {
    return { ...this.moduleOverrides };
  }
}

/**
 * Default logger instance
 */
export const logger: Logger = new Logger();

/**
 * Parses module overrides from environment variable.
 * Expected format: "module1:level1,module2:level2"
 * Example: "compiler:debug,downloader:trace"
 *
 * @param envValue - The environment variable value
 * @returns Parsed module overrides object
 */
export function parseModuleOverrides(envValue: string | undefined): ModuleOverrides {
  if (!envValue) {
    return {};
  }

  const overrides: ModuleOverrides = {};
  const pairs = envValue.split(',');

  for (const pair of pairs) {
    const trimmedPair = pair.trim();
    if (!trimmedPair) continue;

    const [moduleName, levelName] = trimmedPair.split(':');
    if (!moduleName || !levelName) continue;

    const trimmedModule = moduleName.trim();
    const trimmedLevel = levelName.trim().toLowerCase();

    // Parse log level from string
    let level: LogLevel;
    switch (trimmedLevel) {
      case 'trace':
        level = LogLevel.Trace;
        break;
      case 'debug':
        level = LogLevel.Debug;
        break;
      case 'info':
        level = LogLevel.Info;
        break;
      case 'warn':
      case 'warning':
        level = LogLevel.Warn;
        break;
      case 'error':
        level = LogLevel.Error;
        break;
      case 'silent':
        level = LogLevel.Silent;
        break;
      default:
        // Skip invalid log levels
        continue;
    }

    overrides[trimmedModule] = level;
  }

  return overrides;
}

/**
 * Creates a new logger with the given options
 * @param options - Logger configuration options
 * @returns A Logger or StructuredLogger instance based on options.structured
 */
export function createLogger(options?: LoggerOptions): Logger {
  if (options?.structured) {
    return new StructuredLogger(options);
  }
  return new Logger(options);
}

/**
 * Creates a logger with configuration from environment variables.
 * Reads the following environment variables:
 * - LOG_LEVEL: Default log level (trace, debug, info, warn, error, silent)
 * - LOG_MODULE_OVERRIDES: Module-specific overrides in format "module1:level1,module2:level2"
 * - LOG_STRUCTURED: Enable structured JSON output (true/false)
 *
 * @param options - Additional options to merge with environment configuration
 * @returns A configured Logger instance
 *
 * @example
 * ```typescript
 * // Set via environment:
 * // LOG_LEVEL=info
 * // LOG_MODULE_OVERRIDES=compiler:debug,downloader:trace
 * const logger = createLoggerFromEnv({ prefix: 'myapp' });
 * ```
 */
export function createLoggerFromEnv(options?: LoggerOptions): Logger {
  // Parse default log level from environment
  let defaultLevel = LogLevel.Info;
  const envLevelRaw = typeof Deno !== 'undefined' && Deno.env
    ? Deno.env.get('LOG_LEVEL')
    : undefined;
  const envLevel = envLevelRaw?.toLowerCase();
  if (envLevel) {
    switch (envLevel) {
      case 'trace':
        defaultLevel = LogLevel.Trace;
        break;
      case 'debug':
        defaultLevel = LogLevel.Debug;
        break;
      case 'info':
        defaultLevel = LogLevel.Info;
        break;
      case 'warn':
      case 'warning':
        defaultLevel = LogLevel.Warn;
        break;
      case 'error':
        defaultLevel = LogLevel.Error;
        break;
      case 'silent':
        defaultLevel = LogLevel.Silent;
        break;
    }
  }

  // Parse module overrides from environment
  const envOverrides = typeof Deno !== 'undefined' && Deno.env
    ? Deno.env.get('LOG_MODULE_OVERRIDES')
    : undefined;
  const moduleOverrides = parseModuleOverrides(envOverrides);

  // Parse structured flag from environment
  const envStructured = typeof Deno !== 'undefined' && Deno.env
    ? Deno.env.get('LOG_STRUCTURED')
    : undefined;
  const structured = envStructured === 'true' || envStructured === '1';

  // Merge with provided options (provided options take precedence)
  return createLogger({
    ...options,
    level: options?.level ?? defaultLevel,
    structured: options?.structured ?? structured,
    // Merge module overrides (provided options override env)
    moduleOverrides: {
      ...moduleOverrides,
      ...(options?.moduleOverrides || {}),
    },
  });
}

/**
 * Silent logger that discards all output (useful for testing)
 */
export const silentLogger: ILogger = {
  trace: () => {},
  debug: () => {},
  info: () => {},
  warn: () => {},
  error: () => {},
};

/**
 * Map LogLevel enum to string representation for structured logs
 */
function logLevelToString(level: LogLevel): string {
  switch (level) {
    case LogLevel.Trace:
      return 'trace';
    case LogLevel.Debug:
      return 'debug';
    case LogLevel.Info:
      return 'info';
    case LogLevel.Warn:
      return 'warn';
    case LogLevel.Error:
      return 'error';
    case LogLevel.Silent:
      return 'silent';
    default:
      return 'info';
  }
}

/**
 * Structured logger implementation for production observability
 * Outputs JSON-formatted logs compatible with log aggregation systems
 * (CloudWatch, Datadog, Splunk, etc.)
 *
 * @example
 * ```typescript
 * const logger = new StructuredLogger({
 *     level: LogLevel.Info,
 *     correlationId: 'abc-123',
 *     traceId: 'trace-456'
 * });
 * logger.info('Processing started', { itemCount: 42 });
 * // Output: {"timestamp":"2024-01-01T12:00:00.000Z","level":"info","message":"Processing started","context":{"itemCount":42},"correlationId":"abc-123","traceId":"trace-456"}
 * ```
 */
export class StructuredLogger extends Logger {
  private correlationId?: string;
  private traceId?: string;

  /**
   * Creates a new `StructuredLogger`.
   * @param options Logger configuration. `correlationId` and `traceId` are attached to every
   * emitted JSON record; all other options behave as in {@linkcode Logger}.
   */
  constructor(options: LoggerOptions = {}) {
    // Pass non-structured options to parent
    super({
      level: options.level,
      prefix: options.prefix,
      timestamps: false, // Don't use parent's timestamp formatting
      colors: false, // JSON doesn't need colors
      module: options.module,
      moduleOverrides: options.moduleOverrides,
    });
    this.correlationId = options.correlationId;
    this.traceId = options.traceId;
  }

  /**
   * Creates a structured log entry
   */
  private createLogEntry(
    level: LogLevel,
    message: string,
    context?: Record<string, unknown>,
  ): string {
    const entry: {
      timestamp: string;
      level: string;
      message: string;
      prefix?: string;
      context?: Record<string, unknown>;
      correlationId?: string;
      traceId?: string;
    } = {
      timestamp: new Date().toISOString(),
      level: logLevelToString(level),
      message,
    };

    // Only include prefix if set
    if (this.prefix) {
      entry.prefix = this.prefix;
    }

    // Only include context if provided
    if (context && Object.keys(context).length > 0) {
      entry.context = context;
    }

    // Only include correlationId if set
    if (this.correlationId) {
      entry.correlationId = this.correlationId;
    }

    // Only include traceId if set
    if (this.traceId) {
      entry.traceId = this.traceId;
    }

    return JSON.stringify(entry);
  }

  /**
   * Logs a trace message with optional context
   */
  override trace(message: string, context?: Record<string, unknown>): void {
    if (this.level <= LogLevel.Trace) {
      console.debug(this.createLogEntry(LogLevel.Trace, message, context));
    }
  }

  /**
   * Logs a debug message with optional context
   */
  override debug(message: string, context?: Record<string, unknown>): void {
    if (this.level <= LogLevel.Debug) {
      console.debug(this.createLogEntry(LogLevel.Debug, message, context));
    }
  }

  /**
   * Logs an info message with optional context
   */
  override info(message: string, context?: Record<string, unknown>): void {
    if (this.level <= LogLevel.Info) {
      console.info(this.createLogEntry(LogLevel.Info, message, context));
    }
  }

  /**
   * Logs a warning message with optional context
   */
  override warn(message: string, context?: Record<string, unknown>): void {
    if (this.level <= LogLevel.Warn) {
      console.warn(this.createLogEntry(LogLevel.Warn, message, context));
    }
  }

  /**
   * Logs an error message with optional context
   */
  override error(message: string, context?: Record<string, unknown>): void {
    if (this.level <= LogLevel.Error) {
      console.error(this.createLogEntry(LogLevel.Error, message, context));
    }
  }

  /**
   * Logs a success message (info level) with optional context
   */
  override success(message: string, context?: Record<string, unknown>): void {
    if (this.level <= LogLevel.Info) {
      // Success is logged at info level with 'success' in the message or context
      const successContext = { ...context, type: 'success' };
      console.info(this.createLogEntry(LogLevel.Info, message, successContext));
    }
  }

  /**
   * Creates a child logger with an additional prefix
   */
  override child(prefix: string): StructuredLogger {
    const childPrefix = this.prefix ? `${this.prefix}:${prefix}` : prefix;
    return new StructuredLogger({
      level: this.level,
      prefix: childPrefix,
      correlationId: this.correlationId,
      traceId: this.traceId,
      structured: true,
      module: this.module,
      moduleOverrides: this.moduleOverrides,
    });
  }

  /**
   * Sets the correlation ID for grouping related logs
   */
  setCorrelationId(correlationId: string): void {
    this.correlationId = correlationId;
  }

  /**
   * Sets the trace ID for distributed tracing
   */
  setTraceId(traceId: string): void {
    this.traceId = traceId;
  }
}
