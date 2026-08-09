/**
 * Type definitions for the Rules Compiler TypeScript Frontend
 */

import type { IConfiguration } from '../index.ts';

/**
 * Supported configuration file formats
 */
export type ConfigurationFormat = 'json' | 'yaml' | 'toml';

/**
 * CLI options parsed from command line arguments
 */
export interface CliOptions {
  /** Path to configuration file */
  configPath?: string;
  /** Path to output file */
  outputPath?: string;
  /** Copy output to rules directory */
  copyToRules: boolean;
  /** Custom rules directory path */
  rulesDirectory?: string;
  /** Force configuration format */
  format?: ConfigurationFormat;
  /** Show version information */
  version: boolean;
  /** Show help */
  help: boolean;
  /** Enable debug output */
  debug: boolean;
  /** Show config only (don't compile) */
  showConfig: boolean;
  /** Run in interactive mode */
  interactive: boolean;
  /** Run in compile mode (non-interactive) */
  compile: boolean;
  /** Validate configuration only */
  validate: boolean;
  /** Disable archiving */
  noArchive?: boolean;
  /** Enable interactive archiving */
  archiveInteractive?: boolean;
  /** Archive retention in days */
  archiveRetention?: number;
  /** Validate configuration before compiling (default: true) */
  validateConfig?: boolean;
  /** Fail compilation on validation warnings */
  failOnWarnings?: boolean;
  /** Enable chunked parallel compilation */
  enableChunking?: boolean;
  /** Number of rules per chunk */
  chunkSize?: number;
  /** Maximum number of chunks to process in parallel */
  maxParallel?: number;
}

/**
 * Result of a compilation operation
 */
export interface CompilerResult {
  /** Whether compilation was successful */
  success: boolean;
  /** Name from configuration */
  configName: string;
  /** Version from configuration */
  configVersion: string;
  /** Number of rules in output */
  ruleCount: number;
  /** Path to output file */
  outputPath: string;
  /** SHA-384 hash of output file */
  outputHash: string;
  /** Whether output was copied to rules */
  copiedToRules: boolean;
  /** Destination path if copied */
  rulesDestination?: string;
  /** Elapsed time in milliseconds */
  elapsedMs: number;
  /** Start time */
  startTime: Date;
  /** End time */
  endTime: Date;
  /** Error message if failed */
  errorMessage?: string;
  /** Error code if failed (from CompilerError) */
  errorCode?: string;
}

/**
 * Platform information
 */
export interface PlatformInfo {
  /** Operating system name */
  os: string;
  /** CPU architecture */
  arch: string;
}

/**
 * Version information for all components
 */
export interface VersionInfo {
  /** Module version */
  moduleVersion: string;
  /** Node.js version */
  nodeVersion: string;
  /** Platform information */
  platform: PlatformInfo;
  /** hostlist-compiler version (if available) */
  hostlistCompilerVersion?: string;
}

/**
 * Hash verification configuration
 */
export interface HashVerificationConfig {
  /** Verification mode: strict, warning, or disabled */
  mode?: 'strict' | 'warning' | 'disabled';
  /** Require hashes for all remote sources */
  requireHashesForRemote?: boolean;
  /** Fail compilation on hash mismatch */
  failOnMismatch?: boolean;
  /** Path to hash database file */
  hashDatabasePath?: string;
}

/**
 * Archiving configuration
 */
export interface ArchivingConfig {
  /** Whether archiving is enabled */
  enabled?: boolean;
  /** Archiving mode: automatic, interactive, or disabled */
  mode?: 'automatic' | 'interactive' | 'disabled';
  /** Retention period in days */
  retentionDays?: number;
}

/**
 * Output configuration
 */
export interface OutputConfig {
  /** Output file path */
  path?: string;
  /** Output file name (if not using full path) */
  fileName?: string;
  /** Handle file conflicts: rename, overwrite, or error */
  conflictStrategy?: 'rename' | 'overwrite' | 'error';
}

/**
 * Chunking configuration for parallel compilation of large rule lists
 */
export interface ChunkingConfig {
  /** Enable chunking for large rule lists */
  enabled?: boolean;
  /** Number of rules per chunk (default: 100000) */
  chunkSize?: number;
  /** Maximum number of chunks to process in parallel (default: CPU count) */
  maxParallel?: number;
  /** Strategy for determining chunk boundaries: 'line-count' or 'source' */
  strategy?: 'line-count' | 'source';
}

/**
 * Extended configuration with source format tracking
 */
export interface ExtendedConfiguration extends IConfiguration {
  /** Original file format */
  _sourceFormat?: ConfigurationFormat;
  /** Original file path */
  _sourcePath?: string;
  /** Hash verification configuration */
  hashVerification?: HashVerificationConfig;
  /** Archiving configuration */
  archiving?: ArchivingConfig;
  /** Output configuration */
  output?: OutputConfig;
  /** Chunking configuration for parallel compilation */
  chunking?: ChunkingConfig;
}

/**
 * Logger interface for consistent logging
 */
export interface Logger {
  /** Logs an informational message. */
  info(message: string, ...args: unknown[]): void;
  /** Logs a warning that does not stop execution. */
  warn(message: string, ...args: unknown[]): void;
  /** Logs an error, typically alongside a failure or caught exception. */
  error(message: string, ...args: unknown[]): void;
  /** Logs a diagnostic message intended for troubleshooting, not shown by default. */
  debug(message: string, ...args: unknown[]): void;
}

/**
 * Options for running the compiler
 */
export interface CompileOptions {
  /** Path to configuration file */
  configPath: string;
  /** Path to output file */
  outputPath?: string;
  /** Copy output to rules directory */
  copyToRules?: boolean;
  /** Custom rules directory path */
  rulesDirectory?: string;
  /** Force configuration format */
  format?: ConfigurationFormat;
  /** Logger instance */
  logger?: Logger;
  /** Validate configuration before compiling (default: true) */
  validateConfig?: boolean;
  /** Fail compilation on validation warnings */
  failOnWarnings?: boolean;
  /** Compilation timeout in milliseconds */
  timeoutMs?: number;
  /** Enable chunking for large rule lists */
  enableChunking?: boolean;
  /** Number of rules per chunk */
  chunkSize?: number;
  /** Maximum number of chunks to process in parallel */
  maxParallel?: number;
}

/**
 * Event callback types for hash verification
 */

/** Event arguments for when a hash is computed */
export interface HashComputedEvent {
  /** Path or identifier for the item being hashed */
  itemIdentifier: string;
  /** Type of item (e.g., "input_file", "output_file", "downloaded_source") */
  itemType: string;
  /** Computed SHA-384 hash (96 hex characters) */
  hash: string;
  /** Size of the item in bytes */
  sizeBytes: number;
  /** Whether this is for verification purposes */
  isVerification: boolean;
  /** Timestamp of the event */
  timestamp: Date;
}

/** Event arguments for when a hash is verified successfully */
export interface HashVerifiedEvent {
  /** Path or identifier for the item */
  itemIdentifier: string;
  /** Type of item (e.g., "input_file", "output_file", "downloaded_source") */
  itemType: string;
  /** Expected hash */
  expectedHash: string;
  /** Actual hash */
  actualHash: string;
  /** Size of the item in bytes */
  sizeBytes: number;
  /** Duration of hash computation in milliseconds */
  computationDurationMs: number;
  /** Timestamp of the event */
  timestamp: Date;
}

/** Event arguments for when a hash verification fails */
export interface HashMismatchEvent {
  /** Path or identifier for the item */
  itemIdentifier: string;
  /** Type of item (e.g., "input_file", "output_file", "downloaded_source") */
  itemType: string;
  /** Expected hash */
  expectedHash: string;
  /** Actual hash */
  actualHash: string;
  /** Size of the item in bytes */
  sizeBytes: number;
  /** Whether to abort compilation */
  abort: boolean;
  /** Reason for aborting (if abort is true) */
  abortReason?: string;
  /** Whether the handler allowed continuation despite mismatch */
  allowContinuation: boolean;
  /** Timestamp of the event */
  timestamp: Date;
}

/**
 * Hash verification event callbacks
 */
export interface HashVerificationCallbacks {
  /** Called when a hash is computed */
  onHashComputed?: (event: HashComputedEvent) => void | Promise<void>;
  /** Called when a hash is verified successfully */
  onHashVerified?: (event: HashVerifiedEvent) => void | Promise<void>;
  /** Called when a hash verification fails */
  onHashMismatch?: (event: HashMismatchEvent) => void | Promise<void>;
}
