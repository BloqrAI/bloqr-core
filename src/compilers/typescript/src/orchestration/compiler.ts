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
import { readConfiguration, stripInternalMetadata } from './config-reader.ts';
import { logger as defaultLogger } from './logger.ts';
import { CompilationError, ErrorCode, isCompilerError } from './errors.ts';
import { withTimeout } from './timeout.ts';
import { checkFileSize, DEFAULT_RESOURCE_LIMITS } from './validation.ts';
import { mergeChunks, shouldEnableChunking, splitIntoChunks } from './chunking.ts';
import { compileChunksInParallel } from './parallel-compiler.ts';
import { detectSourceEngine, MultiEngineCompiler } from '../engines/index.ts';
import type { EngineKind } from '../engines/index.ts';

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
 * Locates the `bloqr-validate` CLI binary (from `src/validation/`).
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
  const binaryName = isWindows ? 'bloqr-validate.exe' : 'bloqr-validate';

  const pathEnv = getEnvVar('PATH') ?? '';
  const pathSeparator = isWindows ? ';' : ':';
  for (const dir of pathEnv.split(pathSeparator)) {
    if (!dir) continue;
    const candidate = join(dir, binaryName);
    if (existsSync(candidate)) {
      return candidate;
    }
  }

  // Dev-convenience fallback: compiler.ts -> orchestration/ -> src/ -> typescript/ -> compilers/ -> src/ -> repo root
  const repoRoot = fileURLToPath(new URL('../../../../../', import.meta.url));
  for (const profile of ['release', 'debug']) {
    const candidate = join(repoRoot, 'target', profile, binaryName);
    if (existsSync(candidate)) {
      return candidate;
    }
  }

  return undefined;
}

/** Parsed `--json file` output from the `bloqr-validate` CLI */
interface RulesValidateFileResult {
  is_valid: boolean;
  valid_rules: number;
  invalid_rules: number;
  messages: string[];
}

/**
 * Shells out to the `bloqr-validate` CLI to syntax-check compiled output and
 * fires `callbacks.onValidation` with the result.
 *
 * Fail-closed by default: any Error/Critical finding (`!event.passed`) aborts
 * compilation, and so does a missing binary, a failed invocation, or
 * unparseable output - a validator we couldn't run tells us nothing about the
 * output's safety, so it can't be treated as "no findings". `failOnWarnings`
 * additionally escalates Warning findings to abort. A handler may still set
 * `event.abort`/`event.abortReason` explicitly for custom logic, but no
 * handler is required for the default checks to hold.
 *
 * Pass `allowUnvalidated: true` to revert to the legacy, opt-in-only behavior
 * (silently skip on a run failure; only an explicit handler-set `abort`
 * counts) - use only for deliberate debugging of unvalidated output.
 * @param outputPath - Path to the compiled output file
 * @param callbacks - Optional validation callbacks
 * @param logger - Logger instance
 * @param allowUnvalidated - Explicit opt-out of the fail-closed default
 * @param failOnWarnings - Also abort on Warning-severity findings
 * @param engine - Which grammar `bloqr-validate` validates against: `'dns'` (default -
 * rejects cosmetic/browser-only syntax) or `'browser'` (accepts it natively). Pass
 * `'browser'` for the browser-syntax artifact so it validates fail-closed without needing
 * `allowUnvalidatedOutput` - see docs/adr/0005-browser-syntax-validation-engine.md.
 * @throws {Error} If validation could not run, reported invalid output, or a
 * handler set `event.abort = true` - unless `allowUnvalidated` is set
 */
export async function runRulesValidator(
  outputPath: string,
  callbacks?: ValidationCallbacks,
  logger: Logger = defaultLogger,
  allowUnvalidated = false,
  failOnWarnings = false,
  engine: 'dns' | 'browser' = 'dns',
): Promise<void> {
  const binary = findRulesValidateBinary();
  if (!binary) {
    if (allowUnvalidated) {
      logger.debug('bloqr-validate binary not found; skipping syntax validation');
      return;
    }
    throw new Error(
      'bloqr-validate binary not found; cannot validate compiled output. ' +
        'Pass allowUnvalidatedOutput to bypass this check (not recommended).',
    );
  }

  const hashDbPath = join(dirname(outputPath), '.hashes.json');

  let stdout: string;
  try {
    const spawnResult = spawnSync(
      binary,
      ['--json', 'file', outputPath, '--hash-db', hashDbPath, '--engine', engine],
      { encoding: 'utf8' },
    );
    if (spawnResult.error) {
      throw spawnResult.error;
    }
    stdout = spawnResult.stdout ?? '';
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    if (allowUnvalidated) {
      logger.debug(`bloqr-validate invocation failed, skipping syntax validation: ${message}`);
      return;
    }
    throw new Error(
      `bloqr-validate invocation failed: ${message}. ` +
        'Pass allowUnvalidatedOutput to bypass this check (not recommended).',
    );
  }

  let parsed: RulesValidateFileResult;
  try {
    parsed = JSON.parse(stdout);
  } catch {
    if (allowUnvalidated) {
      logger.debug('bloqr-validate produced non-JSON output; skipping syntax validation');
      return;
    }
    throw new Error(
      'bloqr-validate produced non-JSON output; cannot validate compiled output. ' +
        'Pass allowUnvalidatedOutput to bypass this check (not recommended).',
    );
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
    stageName: 'bloqr-validator',
    itemsValidated: parsed.valid_rules + parsed.invalid_rules,
    passed: parsed.is_valid,
    findings,
    abort: false,
    timestamp: new Date(),
  };

  if (callbacks?.onValidation) {
    await Promise.resolve(callbacks.onValidation(event));
  }

  const hasWarnings = findings.some((f) => f.severity === 'warning');
  const shouldAbort = event.abort ||
    (!allowUnvalidated && (!event.passed || (failOnWarnings && hasWarnings)));

  if (shouldAbort) {
    throw new Error(
      event.abortReason ?? 'Rules validation aborted: bloqr-validate reported invalid output',
    );
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
    // Wrap compilation with timeout. `compile()` is the core engine's strict-schema
    // boundary - it rejects unrecognized properties, so any orchestration-layer metadata
    // (`_sourceFormat`/`_sourcePath`, added by readConfiguration()) must be stripped
    // before the config object reaches it, even though `config`'s declared type here is
    // already the clean `IConfiguration` (the actual object at runtime may carry more).
    const result = await withTimeout(
      compile(stripInternalMetadata(config)),
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
 * Derives the default output path for the browser-syntax artifact from the
 * DNS/primary output path: `.txt` is replaced with `.browser.txt`; any other
 * extension (or none) has `.browser.txt` appended.
 * @param outputPath - The primary (DNS) output path.
 * @returns The derived browser-syntax output path.
 */
function deriveBrowserOutputPath(outputPath: string): string {
  return outputPath.endsWith('.txt')
    ? `${outputPath.slice(0, -'.txt'.length)}.browser.txt`
    : `${outputPath}.browser.txt`;
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
  /**
   * Explicit opt-out of the mandatory rules-validator syntax check on compiled
   * output. Security-relevant: leave this `false` (the default) in production -
   * compiled output is validated and compilation fails closed by default. Use
   * only for deliberate debugging of unvalidated output.
   */
  allowUnvalidatedOutput?: boolean;
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

    // Dual-engine routing: resolve declaratively (explicit source.engine,
    // legacy SourceType.Hosts, or configuration.defaultEngine) which engine
    // each source belongs to, without downloading anything. This is
    // deliberately the *only* new branch point in this function: an
    // all-DNS configuration - the default, and every configuration that
    // existed before this feature - takes the untouched chunked/unchunked
    // compileFilters() path below unmodified, guaranteeing byte-identical
    // output to pre-dual-engine behavior.
    const forceEngine: EngineKind | undefined = options.engine && options.engine !== 'auto'
      ? options.engine
      : undefined;
    const hasBrowserSources = config.sources.some((source) =>
      (forceEngine ?? detectSourceEngine(source, [], config.defaultEngine)) === 'browser'
    );

    if (hasBrowserSources) {
      const hasDnsSources = config.sources.some((source) =>
        (forceEngine ?? detectSourceEngine(source, [], config.defaultEngine)) === 'dns'
      );

      logger.info(
        `Using multi-engine compilation (dns=${hasDnsSources}, browser=${hasBrowserSources})`,
      );

      // Note: orchestration's `Logger` (used throughout this file) is a
      // narrower interface than the compiler-core `ILogger` the engine
      // classes expect (no `trace`), so - mirroring how `compileFilters()`
      // below already lets `compile()` fall back to compiler-core's own
      // default logger rather than threading this one through - engine
      // compilation logging uses its own default logger too.
      const multiEngineCompiler = new MultiEngineCompiler({ forceEngine });
      // Mirrors compileFilters()'s stripInternalMetadata() call: the strict-schema
      // validators inside FilterCompiler/BrowserSyntaxCompiler reject unrecognized
      // properties, so readConfiguration()'s orchestration-layer metadata
      // (_sourceFormat/_sourcePath) must not reach them.
      const multiResult = await multiEngineCompiler.compile(stripInternalMetadata(config));

      // Both buckets present: dns writes to the primary output path, browser
      // writes to its own (default-derived or explicit) path. Only one
      // bucket present: it alone writes to the primary output path - a
      // browser-only or forced-dns configuration still produces exactly one
      // artifact, matching single-engine behavior.
      if (multiResult.dns) {
        writeOutput(result.outputPath, multiResult.dns.rules, logger);
      }
      if (multiResult.browser) {
        const browserOutputPath = multiResult.dns
          ? resolve(options.browserOutputPath ?? deriveBrowserOutputPath(result.outputPath))
          : result.outputPath;
        writeOutput(browserOutputPath, multiResult.browser.rules, logger);
        if (multiResult.dns) {
          result.browserOutputPath = browserOutputPath;
        }
      }

      const maxOutputSize = options.maxOutputSize ?? DEFAULT_RESOURCE_LIMITS.maxOutputFileSize;

      if (multiResult.dns) {
        const outputStats = statSync(result.outputPath);
        checkFileSize(outputStats.size, maxOutputSize, 'output file');
        result.ruleCount = countRules(result.outputPath);
        result.outputHash = options.hashCallbacks
          ? await computeHashWithCallbacks(result.outputPath, 'output_file', options.hashCallbacks)
          : computeHash(result.outputPath);

        await runRulesValidator(
          result.outputPath,
          options.validationCallbacks,
          logger,
          options.allowUnvalidatedOutput ?? false,
          options.failOnWarnings ?? false,
        );
      }

      if (multiResult.browser) {
        const browserPath = result.browserOutputPath ?? result.outputPath;
        const outputStats = statSync(browserPath);
        checkFileSize(outputStats.size, maxOutputSize, 'browser output file');
        const browserRuleCount = countRules(browserPath);
        const browserOutputHash = options.hashCallbacks
          ? await computeHashWithCallbacks(
            browserPath,
            'browser_output_file',
            options.hashCallbacks,
          )
          : computeHash(browserPath);

        await runRulesValidator(
          browserPath,
          options.validationCallbacks,
          logger,
          options.allowUnvalidatedOutput ?? false,
          options.failOnWarnings ?? false,
          'browser',
        );

        if (multiResult.dns) {
          result.browserRuleCount = browserRuleCount;
          result.browserOutputHash = browserOutputHash;
        } else {
          // Browser-only compilation: report through the primary result
          // fields, matching single-engine (dns-only) behavior.
          result.ruleCount = browserRuleCount;
          result.outputHash = browserOutputHash;
        }
      }

      logger.debug(`Hash: ${result.outputHash}`);
    } else {
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

      // Mandatory bloqr-validator syntax check - fail-closed by default (see
      // runRulesValidator doc comment); handlers may still customize via the
      // callback, but nothing has to be registered for the default checks to hold.
      await runRulesValidator(
        result.outputPath,
        options.validationCallbacks,
        logger,
        options.allowUnvalidatedOutput ?? false,
        options.failOnWarnings ?? false,
      );
    }

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
