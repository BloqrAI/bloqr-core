/**
 * Logger utility for consistent console output
 * Supports both human-readable and structured (JSON) output formats
 */

import process from 'node:process';
import type { Logger } from './types.ts';

/**
 * Log level enumeration
 */
export enum LogLevel {
  DEBUG = 0,
  INFO = 1,
  WARN = 2,
  ERROR = 3,
  SILENT = 4,
}

/**
 * Log level names for output
 */
const LOG_LEVEL_NAMES: Record<LogLevel, string> = {
  [LogLevel.DEBUG]: 'DEBUG',
  [LogLevel.INFO]: 'INFO',
  [LogLevel.WARN]: 'WARN',
  [LogLevel.ERROR]: 'ERROR',
  [LogLevel.SILENT]: 'SILENT',
};

/**
 * Logger configuration options
 */
export interface LoggerConfig {
  /** Enable debug logging */
  debugEnabled: boolean;
  /** Use JSON format for structured logging */
  jsonFormat: boolean;
  /** Minimum log level to output */
  minLevel: LogLevel;
  /** Include timestamps in output */
  timestamps: boolean;
  /** Application name for structured logs */
  appName?: string;
  /** Additional context to include in all logs */
  context?: Record<string, unknown>;
}

/**
 * Default logger configuration
 */
const DEFAULT_CONFIG: LoggerConfig = {
  debugEnabled: false,
  jsonFormat: false,
  minLevel: LogLevel.INFO,
  timestamps: true,
  appName: 'rules-compiler',
};

/**
 * Structured log entry for JSON output
 */
interface LogEntry {
  timestamp: string;
  level: string;
  message: string;
  app?: string;
  context?: Record<string, unknown>;
  args?: unknown[];
}

/**
 * Formats timestamp for log messages
 */
function timestamp(): string {
  return new Date().toISOString();
}

/**
 * Extended logger interface with configuration
 */
export interface ExtendedLogger extends Logger {
  /** Current configuration */
  readonly config: Readonly<LoggerConfig>;
  /** Create a child logger with additional context */
  child(context: Record<string, unknown>): ExtendedLogger;
  /** Check if a log level is enabled */
  isLevelEnabled(level: LogLevel): boolean;
}

/**
 * Creates a logger instance with configurable options
 * @param config - Logger configuration
 * @returns Logger instance
 */
export function createLogger(config: Partial<LoggerConfig> | boolean = false): ExtendedLogger {
  // Handle legacy boolean parameter for backwards compatibility
  const resolvedConfig: LoggerConfig = typeof config === 'boolean'
    ? { ...DEFAULT_CONFIG, debugEnabled: config }
    : { ...DEFAULT_CONFIG, ...config };

  // Helper to get environment variables. Prefers Deno's API when available,
  // falling back to `node:process` (Node.js and Bun) otherwise.
  const getEnv = (key: string): string | undefined => {
    if (typeof Deno !== 'undefined' && Deno.env) {
      return Deno.env.get(key);
    }
    return process.env[key];
  };

  // Check environment variables
  if (getEnv('DEBUG')) {
    resolvedConfig.debugEnabled = true;
  }
  if (getEnv('LOG_FORMAT') === 'json') {
    resolvedConfig.jsonFormat = true;
  }
  const logLevel = getEnv('LOG_LEVEL');
  if (logLevel) {
    const envLevel = logLevel.toUpperCase();
    const levelMap: Record<string, LogLevel> = {
      DEBUG: LogLevel.DEBUG,
      INFO: LogLevel.INFO,
      WARN: LogLevel.WARN,
      ERROR: LogLevel.ERROR,
      SILENT: LogLevel.SILENT,
    };
    if (envLevel in levelMap) {
      const level = levelMap[envLevel];
      if (level !== undefined) {
        resolvedConfig.minLevel = level;
      }
    }
  }

  /**
   * Checks if a log level should be output
   */
  function isLevelEnabled(level: LogLevel): boolean {
    if (level === LogLevel.DEBUG && !resolvedConfig.debugEnabled) {
      return false;
    }
    return level >= resolvedConfig.minLevel;
  }

  /**
   * Formats and outputs a log entry
   */
  function log(level: LogLevel, message: string, args: unknown[]): void {
    if (!isLevelEnabled(level)) {
      return;
    }

    const ts = timestamp();
    const levelName = LOG_LEVEL_NAMES[level];

    if (resolvedConfig.jsonFormat) {
      const entry: LogEntry = {
        timestamp: ts,
        level: levelName,
        message,
        app: resolvedConfig.appName,
        context: resolvedConfig.context,
      };

      if (args.length > 0) {
        entry.args = args;
      }

      const output = JSON.stringify(entry);

      switch (level) {
        case LogLevel.ERROR:
          console.error(output);
          break;
        case LogLevel.WARN:
          console.warn(output);
          break;
        default:
          console.log(output);
      }
    } else {
      const prefix = resolvedConfig.timestamps ? `[${levelName}] ${ts} - ` : `[${levelName}] `;
      const formattedMessage = `${prefix}${message}`;

      switch (level) {
        case LogLevel.ERROR:
          console.error(formattedMessage, ...args);
          break;
        case LogLevel.WARN:
          console.warn(formattedMessage, ...args);
          break;
        case LogLevel.DEBUG:
          console.debug(formattedMessage, ...args);
          break;
        default:
          console.log(formattedMessage, ...args);
      }
    }
  }

  const logger: ExtendedLogger = {
    config: Object.freeze({ ...resolvedConfig }),

    info(message: string, ...args: unknown[]): void {
      log(LogLevel.INFO, message, args);
    },

    warn(message: string, ...args: unknown[]): void {
      log(LogLevel.WARN, message, args);
    },

    error(message: string, ...args: unknown[]): void {
      log(LogLevel.ERROR, message, args);
    },

    debug(message: string, ...args: unknown[]): void {
      log(LogLevel.DEBUG, message, args);
    },

    isLevelEnabled,

    child(context: Record<string, unknown>): ExtendedLogger {
      return createLogger({
        ...resolvedConfig,
        context: { ...resolvedConfig.context, ...context },
      });
    },
  };

  return logger;
}

/**
 * Helper to get environment variable. Prefers Deno's API when available,
 * falling back to `node:process` (Node.js and Bun) otherwise.
 */
function getEnvVar(key: string): string | undefined {
  if (typeof Deno !== 'undefined' && Deno.env) {
    return Deno.env.get(key);
  }
  return process.env[key];
}

/**
 * Default logger instance (debug disabled unless DEBUG env var is set)
 */
export const logger: ExtendedLogger = createLogger(!!getEnvVar('DEBUG'));

/**
 * Creates a JSON-formatted logger for production
 */
export function createProductionLogger(appName?: string): ExtendedLogger {
  return createLogger({
    debugEnabled: false,
    jsonFormat: true,
    minLevel: LogLevel.INFO,
    timestamps: true,
    appName: appName ?? 'rules-compiler',
  });
}

/**
 * Creates a development logger with debug enabled
 */
export function createDevelopmentLogger(): ExtendedLogger {
  return createLogger({
    debugEnabled: true,
    jsonFormat: false,
    minLevel: LogLevel.DEBUG,
    timestamps: true,
  });
}

/**
 * Parses log level from string
 */
export function parseLogLevel(level: string): LogLevel {
  const normalized = level.toUpperCase();
  const levelMap: Record<string, LogLevel> = {
    DEBUG: LogLevel.DEBUG,
    INFO: LogLevel.INFO,
    WARN: LogLevel.WARN,
    WARNING: LogLevel.WARN,
    ERROR: LogLevel.ERROR,
    SILENT: LogLevel.SILENT,
    NONE: LogLevel.SILENT,
  };
  return levelMap[normalized] ?? LogLevel.INFO;
}
