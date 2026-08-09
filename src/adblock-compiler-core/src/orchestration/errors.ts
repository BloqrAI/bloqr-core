/**
 * Custom error classes for production-ready error handling
 * Provides error codes, classification, and proper error hierarchy
 */

/**
 * Error codes for the rules compiler
 */
export enum ErrorCode {
  // Configuration errors (1xxx)
  /** No configuration file was found at the given or default path. */
  CONFIG_NOT_FOUND = 'E1001',
  /** Configuration format (JSON/YAML/TOML) could not be determined or is unsupported. */
  CONFIG_INVALID_FORMAT = 'E1002',
  /** Configuration file content failed to parse as its detected format. */
  CONFIG_PARSE_ERROR = 'E1003',
  /** Parsed configuration failed schema/semantic validation. */
  CONFIG_VALIDATION_ERROR = 'E1004',
  /** A required configuration field is missing. */
  CONFIG_MISSING_REQUIRED = 'E1005',

  // Compilation errors (2xxx)
  /** Filter compilation failed. */
  COMPILATION_FAILED = 'E2001',
  /** Filter compilation exceeded its allotted time. */
  COMPILATION_TIMEOUT = 'E2002',
  /** A configured source could not be fetched. */
  SOURCE_FETCH_FAILED = 'E2003',

  // File system errors (3xxx)
  /** Failed to read a file from disk. */
  FILE_READ_ERROR = 'E3001',
  /** Failed to write a file to disk. */
  FILE_WRITE_ERROR = 'E3002',
  /** Failed to create a required directory. */
  DIRECTORY_CREATE_ERROR = 'E3003',
  /** A resolved path escaped its expected base directory. */
  PATH_TRAVERSAL_ERROR = 'E3004',
  /** A file exceeded the configured maximum size. */
  FILE_TOO_LARGE = 'E3005',

  // Input validation errors (4xxx)
  /** A provided filesystem path is invalid. */
  INVALID_PATH = 'E4001',
  /** A provided URL is invalid. */
  INVALID_URL = 'E4002',
  /** A provided CLI or API argument is invalid. */
  INVALID_ARGUMENT = 'E4003',

  // System errors (5xxx)
  /** Graceful shutdown was requested. */
  SHUTDOWN_REQUESTED = 'E5001',
  /** An operation exceeded its configured timeout. */
  TIMEOUT = 'E5002',
  /** A configured resource limit (e.g. memory, concurrency) was exceeded. */
  RESOURCE_LIMIT_EXCEEDED = 'E5003',
}

/**
 * Error severity levels
 */
export enum ErrorSeverity {
  /** Recoverable error, operation can continue */
  WARNING = 'warning',
  /** Non-recoverable error, operation failed */
  ERROR = 'error',
  /** Critical error, application should terminate */
  CRITICAL = 'critical',
}

/**
 * Error context for debugging and logging
 */
export interface ErrorContext {
  /** File path involved in the error */
  filePath?: string;
  /** URL involved in the error */
  url?: string;
  /** Configuration name */
  configName?: string;
  /** Additional metadata */
  metadata?: Record<string, unknown>;
}

/**
 * Base error class for all compiler errors
 */
export class CompilerError extends Error {
  /** Machine-readable code identifying the specific failure (see {@linkcode ErrorCode}). */
  public readonly code: ErrorCode;
  /** How serious the error is — whether the operation can continue, failed, or must abort. */
  public readonly severity: ErrorSeverity;
  /** Structured metadata about the failure (file path, URL, config name, etc.). */
  public readonly context: ErrorContext;
  /** When this error was constructed. */
  public readonly timestamp: Date;
  /** The underlying error that triggered this one, if any. */
  public override readonly cause?: Error;
  /** The error's class name, used for logging and `instanceof`-free discrimination. */
  public override name: string;

  /**
   * Creates a new `CompilerError`.
   * @param message Human-readable description of the failure.
   * @param code Machine-readable {@linkcode ErrorCode} for the failure.
   * @param severity How serious the error is. Defaults to `ErrorSeverity.ERROR`.
   * @param context Structured metadata to attach for debugging/logging.
   * @param cause The underlying error that triggered this one, if any.
   */
  constructor(
    message: string,
    code: ErrorCode,
    severity: ErrorSeverity = ErrorSeverity.ERROR,
    context: ErrorContext = {},
    cause?: Error,
  ) {
    super(message);
    this.name = 'CompilerError';
    this.code = code;
    this.severity = severity;
    this.context = context;
    this.timestamp = new Date();
    this.cause = cause;

    // Maintains proper stack trace for where error was thrown
    if (Error.captureStackTrace) {
      Error.captureStackTrace(this, CompilerError);
    }
  }

  /**
   * Converts error to a JSON-serializable object
   */
  toJSON(): Record<string, unknown> {
    return {
      name: this.name,
      code: this.code,
      message: this.message,
      severity: this.severity,
      context: this.context,
      timestamp: this.timestamp.toISOString(),
      cause: this.cause?.message,
      stack: this.stack,
    };
  }

  /**
   * Creates a formatted string for logging
   */
  toLogString(): string {
    const contextStr = Object.keys(this.context).length > 0
      ? ` (${JSON.stringify(this.context)})`
      : '';
    return `[${this.code}] ${this.message}${contextStr}`;
  }
}

/**
 * Configuration-related errors
 */
export class ConfigurationError extends CompilerError {
  /**
   * Creates a new `ConfigurationError`.
   * @param message Human-readable description of the failure.
   * @param code Machine-readable {@linkcode ErrorCode}. Defaults to `CONFIG_VALIDATION_ERROR`.
   * @param context Structured metadata to attach for debugging/logging.
   * @param cause The underlying error that triggered this one, if any.
   */
  constructor(
    message: string,
    code: ErrorCode = ErrorCode.CONFIG_VALIDATION_ERROR,
    context: ErrorContext = {},
    cause?: Error,
  ) {
    super(message, code, ErrorSeverity.ERROR, context, cause);
    this.name = 'ConfigurationError';
  }
}

/**
 * Configuration not found error
 */
export class ConfigNotFoundError extends ConfigurationError {
  /**
   * Creates a new `ConfigNotFoundError`.
   * @param filePath Path to the configuration file that could not be found.
   * @param cause The underlying filesystem error, if any.
   */
  constructor(filePath: string, cause?: Error) {
    super(
      `Configuration file not found: ${filePath}`,
      ErrorCode.CONFIG_NOT_FOUND,
      { filePath },
      cause,
    );
    this.name = 'ConfigNotFoundError';
  }
}

/**
 * Configuration parse error
 */
export class ConfigParseError extends ConfigurationError {
  /**
   * Creates a new `ConfigParseError`.
   * @param filePath Path to the configuration file that failed to parse.
   * @param format The configuration format being parsed (e.g. `'yaml'`, `'toml'`, `'json'`).
   * @param parseError The underlying parser's error message.
   * @param cause The underlying parse error, if any.
   */
  constructor(filePath: string, format: string, parseError: string, cause?: Error) {
    super(
      `Failed to parse ${format.toUpperCase()} configuration: ${parseError}`,
      ErrorCode.CONFIG_PARSE_ERROR,
      { filePath, metadata: { format } },
      cause,
    );
    this.name = 'ConfigParseError';
  }
}

/**
 * Compilation-related errors
 */
export class CompilationError extends CompilerError {
  /**
   * Creates a new `CompilationError`.
   * @param message Human-readable description of the failure.
   * @param code Machine-readable {@linkcode ErrorCode}. Defaults to `COMPILATION_FAILED`.
   * @param context Structured metadata to attach for debugging/logging.
   * @param cause The underlying error that triggered this one, if any.
   */
  constructor(
    message: string,
    code: ErrorCode = ErrorCode.COMPILATION_FAILED,
    context: ErrorContext = {},
    cause?: Error,
  ) {
    super(message, code, ErrorSeverity.ERROR, context, cause);
    this.name = 'CompilationError';
  }
}

/**
 * Compilation timeout error
 */
export class CompilationTimeoutError extends CompilationError {
  /** The timeout, in milliseconds, that was exceeded. */
  public readonly timeoutMs: number;

  /**
   * Creates a new `CompilationTimeoutError`.
   * @param timeoutMs The timeout, in milliseconds, that was exceeded.
   * @param context Structured metadata to attach for debugging/logging.
   */
  constructor(timeoutMs: number, context: ErrorContext = {}) {
    super(
      `Compilation timed out after ${timeoutMs}ms`,
      ErrorCode.COMPILATION_TIMEOUT,
      context,
    );
    this.name = 'CompilationTimeoutError';
    this.timeoutMs = timeoutMs;
  }
}

/**
 * File system errors
 */
export class FileSystemError extends CompilerError {
  /**
   * Creates a new `FileSystemError`.
   * @param message Human-readable description of the failure.
   * @param code Machine-readable {@linkcode ErrorCode}. Defaults to `FILE_READ_ERROR`.
   * @param context Structured metadata to attach for debugging/logging.
   * @param cause The underlying filesystem error, if any.
   */
  constructor(
    message: string,
    code: ErrorCode = ErrorCode.FILE_READ_ERROR,
    context: ErrorContext = {},
    cause?: Error,
  ) {
    super(message, code, ErrorSeverity.ERROR, context, cause);
    this.name = 'FileSystemError';
  }
}

/**
 * Path traversal security error
 */
export class PathTraversalError extends CompilerError {
  /**
   * Creates a new `PathTraversalError`.
   * @param path The path in which a directory-traversal attempt (e.g. `../`) was detected.
   */
  constructor(path: string) {
    super(
      `Path traversal detected in path: ${path}`,
      ErrorCode.PATH_TRAVERSAL_ERROR,
      ErrorSeverity.CRITICAL,
      { filePath: path },
    );
    this.name = 'PathTraversalError';
  }
}

/**
 * Input validation errors
 */
export class ValidationError extends CompilerError {
  /**
   * Creates a new `ValidationError`.
   * @param message Human-readable description of the failure.
   * @param code Machine-readable {@linkcode ErrorCode}. Defaults to `INVALID_ARGUMENT`.
   * @param context Structured metadata to attach for debugging/logging.
   * @param cause The underlying error that triggered this one, if any.
   */
  constructor(
    message: string,
    code: ErrorCode = ErrorCode.INVALID_ARGUMENT,
    context: ErrorContext = {},
    cause?: Error,
  ) {
    super(message, code, ErrorSeverity.ERROR, context, cause);
    this.name = 'ValidationError';
  }
}

/**
 * Shutdown requested error (for graceful shutdown)
 */
export class ShutdownError extends CompilerError {
  /** The OS signal (e.g. `'SIGTERM'`, `'SIGINT'`) that triggered the shutdown. */
  public readonly signal: string;

  /**
   * Creates a new `ShutdownError`.
   * @param signal The OS signal that triggered the shutdown.
   */
  constructor(signal: string) {
    super(
      `Shutdown requested via ${signal}`,
      ErrorCode.SHUTDOWN_REQUESTED,
      ErrorSeverity.WARNING,
      {},
    );
    this.name = 'ShutdownError';
    this.signal = signal;
  }
}

/**
 * Resource limit exceeded error
 */
export class ResourceLimitError extends CompilerError {
  /** The configured maximum allowed for `resource`. */
  public readonly limit: number;
  /** The actual value that exceeded `limit`. */
  public readonly actual: number;
  /** Name of the resource whose limit was exceeded (e.g. `'configFileSize'`, `'sourceCount'`). */
  public readonly resource: string;

  /**
   * Creates a new `ResourceLimitError`.
   * @param resource Name of the resource whose limit was exceeded.
   * @param limit The configured maximum allowed for `resource`.
   * @param actual The actual value that exceeded `limit`.
   */
  constructor(resource: string, limit: number, actual: number) {
    super(
      `Resource limit exceeded for ${resource}: ${actual} > ${limit}`,
      ErrorCode.RESOURCE_LIMIT_EXCEEDED,
      ErrorSeverity.ERROR,
      { metadata: { resource, limit, actual } },
    );
    this.name = 'ResourceLimitError';
    this.limit = limit;
    this.actual = actual;
    this.resource = resource;
  }
}

/**
 * Wraps an unknown error into a CompilerError
 */
export function wrapError(
  error: unknown,
  defaultCode: ErrorCode = ErrorCode.COMPILATION_FAILED,
): CompilerError {
  if (error instanceof CompilerError) {
    return error;
  }

  if (error instanceof Error) {
    return new CompilerError(
      error.message,
      defaultCode,
      ErrorSeverity.ERROR,
      {},
      error,
    );
  }

  return new CompilerError(
    String(error),
    defaultCode,
    ErrorSeverity.ERROR,
  );
}

/**
 * Type guard to check if an error is a CompilerError
 */
export function isCompilerError(error: unknown): error is CompilerError {
  return error instanceof CompilerError;
}

/**
 * Type guard to check if error is recoverable
 */
export function isRecoverable(error: unknown): boolean {
  if (!isCompilerError(error)) {
    return false;
  }
  return error.severity === ErrorSeverity.WARNING;
}
