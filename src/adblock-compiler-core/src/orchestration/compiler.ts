/**
 * Core compiler service for filter rules compilation
 * Production-ready with timeouts, error handling, and resource limits
 */

import compile, { type IConfiguration } from '../index.ts';
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  statSync,
  writeFileSync,
} from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import type {
  CompileOptions,
  CompilerResult,
  HashComputedEvent,
  HashMismatchEvent,
  HashVerificationCallbacks,
  HashVerifiedEvent,
  Logger,
  ValidationCallbacks,
  ValidationEvent,
  ValidationFinding,
} from './types.ts';
import { readConfiguration } from './config-reader.ts';
import { logger as defaultLogger } from './logger.ts';
import { CompilationError, ErrorCode, isCompilerError } from './errors.ts';
import { withTimeout } from './timeout.ts';
import { checkFileSize, DEFAULT_RESOURCE_LIMITS } from './validation.ts';
import { mergeChunks, shouldEnableChunking, splitIntoChunks } from './chunking.ts';
import { compileChunksInParallel } from './parallel-compiler.ts';

/**
 * Writes compiled rules to an output file
 * @param outputPath - Path to output file
 * @param rules - Array of compiled rules
 * @param logger - Logger instance
 */
export function writeOutput(
  outputPath: string,
  rules: string[],
  logger: Logger = defaultLogger,
): void {
  logger.debug(`Writing ${rules.length} rules to: ${outputPath}`);

  // Ensure output directory exists
  const outputDir = dirname(outputPath);
  if (!existsSync(outputDir)) {
    mkdirSync(outputDir, { recursive: true });
    logger.debug(`Created output directory: ${outputDir}`);
  }

  const content = rules.join('\n');
  writeFileSync(outputPath, content, 'utf8');

  logger.info(`Wrote ${rules.length} lines to ${outputPath}`);
}

/**
 * Counts non-empty, non-comment lines in a file
 * @param filePath - Path to file
 * @returns Number of rules
 */
export function countRules(filePath: string): number {
  if (!existsSync(filePath)) {
    return 0;
  }

  const content = readFileSync(filePath, 'utf8');
  const lines = content.split('\n');

  return lines.filter((line) => {
    const trimmed = line.trim();
    if (!trimmed) return false;
    if (trimmed.startsWith('!')) return false;
    if (trimmed.startsWith('#')) return false;
    return true;
  }).length;
}

/**
 * Computes SHA-384 hash of a file
 * @param filePath - Path to file
 * @returns Hex-encoded hash string
 */
export function computeHash(filePath: string): string {
  const content = readFileSync(filePath);
  return createHash('sha384').update(content).digest('hex');
}

/**
 * Computes SHA-384 hash of a file and fires callback if provided
 * @param filePath - Path to file
 * @param itemType - Type of item being hashed
 * @param callbacks - Optional hash verification callbacks
 * @returns Hex-encoded hash string
 */
export async function computeHashWithCallbacks(
  filePath: string,
  itemType: string,
  callbacks?: HashVerificationCallbacks,
): Promise<string> {
  const content = readFileSync(filePath);
  const hash = createHash('sha384').update(content).digest('hex');
  const sizeBytes = statSync(filePath).size;

  // Fire hash computed callback
  if (callbacks?.onHashComputed) {
    const event: HashComputedEvent = {
      itemIdentifier: filePath,
      itemType,
      hash,
      sizeBytes,
      isVerification: false,
      timestamp: new Date(),
    };
    await Promise.resolve(callbacks.onHashComputed(event));
  }

  return hash;
}

/**
 * Verifies hash against expected value and fires callbacks
 * @param filePath - Path to file
 * @param expectedHash - Expected SHA-384 hash
 * @param itemType - Type of item being verified
 * @param callbacks - Optional hash verification callbacks
 * @throws {Error} If hashes don't match (unless allowContinuation is set by callback)
 */
export async function verifyHashWithCallbacks(
  filePath: string,
  expectedHash: string,
  itemType: string,
  callbacks?: HashVerificationCallbacks,
): Promise<void> {
  const startTime = Date.now();
  const content = readFileSync(filePath);
  const actualHash = createHash('sha384').update(content).digest('hex');
  const sizeBytes = statSync(filePath).size;
  const computationDurationMs = Date.now() - startTime;

  if (actualHash === expectedHash) {
    // Hash matches - fire verified callback
    if (callbacks?.onHashVerified) {
      const event: HashVerifiedEvent = {
        itemIdentifier: filePath,
        itemType,
        expectedHash,
        actualHash,
        sizeBytes,
        computationDurationMs,
        timestamp: new Date(),
      };
      await Promise.resolve(callbacks.onHashVerified(event));
    }
  } else {
    // Hash mismatch - fire mismatch callback and check if continuation is allowed
    if (callbacks?.onHashMismatch) {
      const event: HashMismatchEvent = {
        itemIdentifier: filePath,
        itemType,
        expectedHash,
        actualHash,
        sizeBytes,
        abort: true,
        abortReason: `Hash mismatch for ${filePath}: expected ${
          expectedHash.slice(0, 16)
        }..., got ${actualHash.slice(0, 16)}...`,
        allowContinuation: false,
        timestamp: new Date(),
      };
      await Promise.resolve(callbacks.onHashMismatch(event));

      if (event.allowContinuation) {
        return;
      }
    }

    throw new Error(
      `Hash mismatch for ${filePath}: expected ${expectedHash}, got ${actualHash}`,
    );
  }
}

/**
 * Copies compiled output to rules directory
 * @param sourcePath - Path to source file
 * @param destPath - Path to destination file
 * @param logger - Logger instance
 */
export function copyToRulesDirectory(
  sourcePath: string,
  destPath: string,
  logger: Logger = defaultLogger,
): void {
  logger.debug(`Copying ${sourcePath} to ${destPath}`);

  const destDir = dirname(destPath);
  if (!existsSync(destDir)) {
    mkdirSync(destDir, { recursive: true });
  }

  copyFileSync(sourcePath, destPath);
  logger.info(`Copied to rules directory: ${destPath}`);
}

/**
 * Reads an environment variable across Deno and Node/Bun runtimes.
 * @param key - Environment variable name
 * @returns Value if set, otherwise `undefined`
 */
function getEnvVar(key: string): string | undefined {
  return typeof Deno !== 'undefined' && Deno.env ? Deno.env.get(key) : process.env[key];
}

/**
 * Locates the `rules-validate` CLI binary (from `src/rules-validator/`).
 *
 * Resolution order: `RULES_VALIDATE_PATH` env override, then `PATH`, then a
 * dev-convenience fallback to the Cargo workspace's `target/{release,debug}`
 * output. Returns `undefined` (rather than throwing) when not found, so
 * syntax validation degrades gracefully - mirroring the other language
 * wrappers' `find_rules_validate_binary` equivalents.
 * @returns Absolute path to the binary, or `undefined` if not found
 */
function findRulesValidateBinary(): string | undefined {
  const envOverride = getEnvVar('RULES_VALIDATE_PATH');
  if (envOverride && existsSync(envOverride)) {
    return envOverride;
  }

  const isWindows = process.platform === 'win32';
  const binaryName = isWindows ? 'rules-validate.exe' : 'rules-validate';

  const pathEnv = getEnvVar('PATH') ?? '';
  const pathSeparator = isWindows ? ';' : ':';
  for (const dir of pathEnv.split(pathSeparator)) {
    if (!dir) continue;
    const candidate = join(dir, binaryName);
    if (existsSync(candidate)) {
      return candidate;
    }
  }

  // Dev-convenience fallback: compiler.ts -> orchestration/ -> src/ -> adblock-compiler-core/ -> src/ -> repo root
  const repoRoot = fileURLToPath(new URL('../../../../', import.meta.url));
  for (const profile of ['release', 'debug']) {
    const candidate = join(repoRoot, 'target', profile, binaryName);
    if (existsSync(candidate)) {
      return candidate;
    }
  }

  return undefined;
}

/** Parsed `--json file` output from the `rules-validate` CLI */
interface RulesValidateFileResult {
  is_valid: boolean;
  valid_rules: number;
  invalid_rules: number;
  messages: string[];
}

/**
 * Shells out to the `rules-validate` CLI to syntax-check compiled output and
 * fires `callbacks.onValidation` with the result. Findings are informational
 * by default; only a handler that explicitly sets `event.abort = true` stops
 * compilation (mirroring the Rust/.NET/Python rules-validator wiring).
 *
 * A missing/unusable binary or malformed output is treated as a skip, not a
 * failure, so this check degrades gracefully wherever the CLI hasn't been
 * built or installed.
 * @param outputPath - Path to the compiled output file
 * @param callbacks - Optional validation callbacks
 * @param logger - Logger instance
 * @throws {Error} If a handler sets `event.abort = true`
 */
export async function runRulesValidator(
  outputPath: string,
  callbacks?: ValidationCallbacks,
  logger: Logger = defaultLogger,
): Promise<void> {
  const binary = findRulesValidateBinary();
  if (!binary) {
    logger.debug('rules-validate binary not found; skipping syntax validation');
    return;
  }

  const hashDbPath = join(dirname(outputPath), '.hashes.json');

  let stdout: string;
  try {
    const spawnResult = spawnSync(
      binary,
      ['--json', 'file', outputPath, '--hash-db', hashDbPath],
      { encoding: 'utf8' },
    );
    if (spawnResult.error) {
      throw spawnResult.error;
    }
    stdout = spawnResult.stdout ?? '';
  } catch (error) {
    logger.debug(
      `rules-validate invocation failed, skipping syntax validation: ${
        error instanceof Error ? error.message : String(error)
      }`,
    );
    return;
  }

  let parsed: RulesValidateFileResult;
  try {
    parsed = JSON.parse(stdout);
  } catch {
    logger.debug('rules-validate produced non-JSON output; skipping syntax validation');
    return;
  }

  const findings: ValidationFinding[] = [];
  if (parsed.messages.length === 0 && !parsed.is_valid) {
    findings.push({
      severity: 'error',
      message:
        `RV001: syntax validation failed (${parsed.valid_rules} valid, ${parsed.invalid_rules} invalid rules)`,
    });
  } else {
    for (const message of parsed.messages) {
      findings.push({ severity: parsed.is_valid ? 'warning' : 'error', message });
    }
  }

  const event: ValidationEvent = {
    stageName: 'rules-validator',
    itemsValidated: parsed.valid_rules + parsed.invalid_rules,
    passed: parsed.is_valid,
    findings,
    abort: false,
    timestamp: new Date(),
  };

  if (callbacks?.onValidation) {
    await Promise.resolve(callbacks.onValidation(event));
  }

  if (event.abort) {
    throw new Error(event.abortReason ?? 'Rules validation aborted by handler');
  }
}

/**
 * Compiler options with resource limits
 */
export interface CompilerOptions {
  /** Compilation timeout in milliseconds */
  timeoutMs?: number;
  /** Maximum output file size in bytes */
  maxOutputSize?: number;
}

/**
 * Default compiler options
 */
const DEFAULT_COMPILER_OPTIONS: CompilerOptions = {
  timeoutMs: DEFAULT_RESOURCE_LIMITS.compilationTimeoutMs,
  maxOutputSize: DEFAULT_RESOURCE_LIMITS.maxOutputFileSize,
};

/**
 * Compiles filter rules using the hostlist-compiler
 * @param config - Compiler configuration
 * @param logger - Logger instance
 * @param options - Compiler options
 * @returns Array of compiled rules
 */
export async function compileFilters(
  config: IConfiguration,
  logger: Logger = defaultLogger,
  options: CompilerOptions = {},
): Promise<string[]> {
  const resolvedOptions = { ...DEFAULT_COMPILER_OPTIONS, ...options };
  logger.info('Starting filter compilation...');

  try {
    // Wrap compilation with timeout
    const result = await withTimeout(
      compile(config),
      resolvedOptions.timeoutMs ?? DEFAULT_RESOURCE_LIMITS.compilationTimeoutMs,
      { configName: config.name },
    );

    logger.info(`Compilation complete. Generated ${result.length} rules.`);
    return result;
  } catch (error) {
    // Re-throw if already a CompilerError
    if (isCompilerError(error)) {
      logger.error(`Compilation failed: ${error.toLogString()}`);
      throw error;
    }

    const message = error instanceof Error ? error.message : 'Unknown error';
    logger.error(`Compilation failed: ${message}`);
    throw new CompilationError(
      `Filter compilation failed: ${message}`,
      ErrorCode.COMPILATION_FAILED,
      { configName: config.name },
      error instanceof Error ? error : undefined,
    );
  }
}

/**
 * Generates a timestamped output filename
 * @returns Filename with timestamp
 */
function generateOutputFilename(): string {
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
  return `compiled-${timestamp}.txt`;
}

/**
 * Extended compile options with resource limits
 */
export interface ExtendedCompileOptions extends CompileOptions {
  /** Compilation timeout in milliseconds */
  timeoutMs?: number;
  /** Maximum output file size in bytes */
  maxOutputSize?: number;
  /** Hash verification callbacks for monitoring integrity */
  hashCallbacks?: HashVerificationCallbacks;
  /** Rules-validator syntax-validation callbacks */
  validationCallbacks?: ValidationCallbacks;
}

/**
 * Runs the full compilation pipeline
 * @param options - Compilation options
 * @returns Compilation result
 */
export async function runCompiler(options: ExtendedCompileOptions): Promise<CompilerResult> {
  const logger = options.logger ?? defaultLogger;
  const startTime = new Date();

  const result: CompilerResult = {
    success: false,
    configName: '',
    configVersion: '',
    ruleCount: 0,
    outputPath: '',
    outputHash: '',
    copiedToRules: false,
    elapsedMs: 0,
    startTime,
    endTime: new Date(),
  };

  try {
    // Read configuration
    logger.info(`Loading configuration from: ${options.configPath}`);
    const config = readConfiguration(options.configPath, options.format, logger);
    result.configName = config.name ?? 'unknown';
    const configRecord = config as unknown as Record<string, unknown>;
    const versionValue = configRecord['version'];
    result.configVersion = typeof versionValue === 'string' ? versionValue : 'unknown';

    // Validate configuration if requested (default: true)
    const shouldValidate = options.validateConfig ?? true;
    if (shouldValidate) {
      const { validateConfiguration } = await import('./validation.ts');
      const validationResult = validateConfiguration(config);

      if (!validationResult.valid) {
        logger.error('Configuration validation failed:');
        for (const error of validationResult.errors) {
          logger.error(`  ${error}`);
        }
        throw new CompilationError(
          `Configuration validation failed: ${validationResult.errors.join('; ')}`,
          ErrorCode.CONFIG_VALIDATION_ERROR,
        );
      }

      if (validationResult.warnings.length > 0) {
        for (const warning of validationResult.warnings) {
          logger.warn(`Configuration warning: ${warning}`);
        }

        if (options.failOnWarnings) {
          throw new CompilationError(
            `Configuration has warnings (failOnWarnings is enabled): ${
              validationResult.warnings.join('; ')
            }`,
            ErrorCode.CONFIG_VALIDATION_ERROR,
          );
        }
      }
    }

    // Determine output path
    const outputFilename = generateOutputFilename();
    const defaultOutputPath = join(dirname(options.configPath), 'output', outputFilename);
    const outputPath = options.outputPath ?? defaultOutputPath;
    result.outputPath = resolve(outputPath);

    // Determine if chunking should be used
    const chunkingConfig = {
      enabled: options.enableChunking ?? config.chunking?.enabled,
      chunkSize: options.chunkSize ?? config.chunking?.chunkSize,
      maxParallel: options.maxParallel ?? config.chunking?.maxParallel,
      strategy: config.chunking?.strategy,
    };

    const useChunking = shouldEnableChunking(config, chunkingConfig, logger);

    let rules: string[];

    if (useChunking) {
      logger.info('Using chunked parallel compilation');

      // Split configuration into chunks
      const chunks = splitIntoChunks(config, chunkingConfig, logger);

      if (chunks.length === 1) {
        // Only one chunk, compile directly
        logger.info('Only one chunk created, compiling directly');
        rules = await compileFilters(config, logger, {
          timeoutMs: options.timeoutMs,
          maxOutputSize: options.maxOutputSize,
        });
      } else {
        // Compile chunks in parallel
        const maxParallel = chunkingConfig.maxParallel ?? 4;
        const compiledChunks = await compileChunksInParallel(chunks, maxParallel, logger);

        // Merge chunks back together
        rules = mergeChunks(compiledChunks, logger);
      }
    } else {
      logger.info('Using standard single-threaded compilation');
      // Standard compilation (no chunking)
      rules = await compileFilters(config, logger, {
        timeoutMs: options.timeoutMs,
        maxOutputSize: options.maxOutputSize,
      });
    }

    // Write output
    writeOutput(result.outputPath, rules, logger);

    // Check output file size
    const outputStats = statSync(result.outputPath);
    const maxOutputSize = options.maxOutputSize ?? DEFAULT_RESOURCE_LIMITS.maxOutputFileSize;
    checkFileSize(outputStats.size, maxOutputSize, 'output file');

    // Calculate statistics - use callbacks if provided
    result.ruleCount = countRules(result.outputPath);
    if (options.hashCallbacks) {
      result.outputHash = await computeHashWithCallbacks(
        result.outputPath,
        'output_file',
        options.hashCallbacks,
      );
    } else {
      result.outputHash = computeHash(result.outputPath);
    }

    logger.debug(`Hash: ${result.outputHash}`);

    // Run the rules-validator syntax check; findings are informational unless a handler aborts
    await runRulesValidator(result.outputPath, options.validationCallbacks, logger);

    // Copy to rules directory if requested
    if (options.copyToRules) {
      const rulesDir = options.rulesDirectory ??
        join(dirname(options.configPath), '..', '..', 'rules');
      const destPath = join(rulesDir, 'adguard_user_filter.txt');
      copyToRulesDirectory(result.outputPath, resolve(destPath), logger);
      result.copiedToRules = true;
      result.rulesDestination = resolve(destPath);

      // Compute hash of copied file if callbacks provided
      if (options.hashCallbacks) {
        await computeHashWithCallbacks(destPath, 'copied_rules_file', options.hashCallbacks);
      }
    }

    result.success = true;
  } catch (error) {
    // Use structured error information if available
    if (isCompilerError(error)) {
      result.errorMessage = error.toLogString();
      result.errorCode = error.code;
    } else {
      const message = error instanceof Error ? error.message : 'Unknown error';
      result.errorMessage = message;
    }
    logger.error(`Compilation failed: ${result.errorMessage}`);
  }

  result.endTime = new Date();
  result.elapsedMs = result.endTime.getTime() - startTime.getTime();

  return result;
}
