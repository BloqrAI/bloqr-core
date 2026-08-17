#!/usr/bin/env -S deno run --allow-read --allow-write --allow-env --allow-run
/**
 * Command-line interface for the Rules Compiler TypeScript frontend.
 *
 * Production-ready with graceful shutdown and structured error handling.
 * Supports both interactive menu mode and non-interactive command-line
 * mode (argument parsing, help/version output, and the {@linkcode main}
 * entry point used by the `cli` export subpath). Deno is this package's
 * own runtime; Node.js and Bun are also supported (see {@link ./mod.bun.ts}
 * for the explicit Bun entry point) since neither `main` nor its
 * dependencies call `Deno.*` without first checking the `Deno` global is
 * present.
 *
 * @example Parse arguments and run the CLI
 * ```ts
 * import { main } from '@bloqr/compiler-core/cli';
 *
 * const exitCode = await main(['--config', 'compiler-config.yaml']);
 * ```
 *
 * @module cli
 * @packageDocumentation
 */

import { resolve } from 'node:path';
import process from 'node:process';
import type { CliOptions, ConfigurationFormat, VersionInfo } from './types.ts';
import { runCompiler } from './compiler.ts';
import { findDefaultConfig, readConfiguration, toJson } from './config-reader.ts';
import { createLogger, createProductionLogger } from './logger.ts';
import { initializeShutdownHandler } from './shutdown.ts';
import type { ShutdownHandler } from './shutdown.ts';
import { isCompilerError } from './errors.ts';
import { runInteractive } from '../console/app.ts';

/** Package version */
const VERSION = '1.0.0';

/**
 * Parses command line arguments
 * @param args - Command line arguments (Deno.args)
 * @returns Parsed CLI options
 */
export function parseArgs(args: string[]): CliOptions {
  const options: CliOptions = {
    copyToRules: false,
    version: false,
    help: false,
    debug: false,
    showConfig: false,
    interactive: false,
    compile: false,
    validate: false,
  };

  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    const nextArg = args[i + 1];

    switch (arg) {
      case '-c':
      case '--config':
        options.configPath = nextArg;
        i++;
        break;
      case '-o':
      case '--output':
        options.outputPath = nextArg;
        i++;
        break;
      case '-r':
      case '--copy-to-rules':
        options.copyToRules = true;
        break;
      case '--rules-dir':
        options.rulesDirectory = nextArg;
        i++;
        break;
      case '-f':
      case '--format':
        if (!nextArg || !['json', 'yaml', 'toml'].includes(nextArg)) {
          throw new Error(`Invalid format: ${nextArg}. Must be json, yaml, or toml.`);
        }
        options.format = nextArg as ConfigurationFormat;
        i++;
        break;
      case '-v':
      case '--version':
        options.version = true;
        break;
      case '-V':
      case '--version-info':
        options.version = true;
        break;
      case '-h':
      case '--help':
        options.help = true;
        break;
      case '-d':
      case '--debug':
        options.debug = true;
        break;
      case '--show-config':
        options.showConfig = true;
        break;
      case '-i':
      case '--interactive':
        options.interactive = true;
        break;
      case '--compile':
        options.compile = true;
        break;
      case '--validate':
        options.validate = true;
        break;
      case '--validate-config':
        options.validateConfig = true;
        break;
      case '--no-validate-config':
        options.validateConfig = false;
        break;
      case '--fail-on-warnings':
        options.failOnWarnings = true;
        break;
      case '--enable-chunking':
        options.enableChunking = true;
        break;
      case '--chunk-size':
        if (!nextArg) {
          throw new Error('Chunk size value is required');
        }
        options.chunkSize = parseInt(nextArg, 10);
        if (isNaN(options.chunkSize) || options.chunkSize <= 0) {
          throw new Error(`Invalid chunk size: ${nextArg}. Must be a positive integer.`);
        }
        i++;
        break;
      case '--max-parallel':
        if (!nextArg) {
          throw new Error('Max parallel value is required');
        }
        options.maxParallel = parseInt(nextArg, 10);
        if (isNaN(options.maxParallel) || options.maxParallel <= 0) {
          throw new Error(`Invalid max parallel: ${nextArg}. Must be a positive integer.`);
        }
        i++;
        break;
      default:
        // Allow positional config path
        if (arg && !arg.startsWith('-') && !options.configPath) {
          options.configPath = arg;
        }
    }
  }

  return options;
}

/**
 * Shows help message
 */
export function showHelp(): void {
  console.log(`
AdGuard Filter Rules Compiler (TypeScript Frontend)

Usage: deno task start [OPTIONS] [CONFIG_PATH]

Modes:
  -i, --interactive     Run in interactive menu mode (default when no args)
  --compile             Run in CLI mode (compile and exit)
  --validate            Validate configuration only

Options:
  -c, --config PATH     Path to configuration file
  -o, --output PATH     Path to output file (default: output/compiled-TIMESTAMP.txt)
  -r, --copy-to-rules   Copy output to rules directory
  --rules-dir PATH      Custom rules directory path
  -f, --format FORMAT   Force configuration format (json, yaml, toml)
  -v, --version         Show version information
  -h, --help            Show this help message
  -d, --debug           Enable debug output
  --show-config         Show parsed configuration (don't compile)
  --validate-config     Enable configuration validation before compilation (default: true)
  --no-validate-config  Disable configuration validation before compilation
  --fail-on-warnings    Fail compilation if configuration has validation warnings

Chunking Options (for large rule lists):
  --enable-chunking     Enable chunked parallel compilation
  --chunk-size N        Number of sources per chunk (applies when using source-based chunking)
  --max-parallel N      Maximum number of chunks to compile in parallel (default: CPU count)

Production Options:
  --json-logs           Use JSON format for log output (structured logging)
  --timeout MS          Compilation timeout in milliseconds (default: 300000)

Environment Variables:
  DEBUG                 Enable debug logging
  LOG_FORMAT=json       Enable JSON log format
  LOG_LEVEL             Set log level (DEBUG, INFO, WARN, ERROR, SILENT)

Supported Configuration Formats:
  - JSON  (.json)
  - YAML  (.yaml, .yml)
  - TOML  (.toml)

Examples:
  deno task start                           # Interactive menu mode
  deno task interactive                     # Explicit interactive mode
  deno task compile                         # CLI compile mode
  deno task start -c config.yaml            # CLI mode with specific config
  deno task start -c config.json -r         # Compile and copy to rules
  deno task start --validate -c config.yaml # Validate only
  deno task start --show-config -c config.yaml
  deno task start --json-logs -c config.yaml  # Production mode
  deno task start --enable-chunking --max-parallel 8  # Parallel chunked compilation
  deno task start --enable-chunking --chunk-size 50000 --max-parallel 4  # Custom chunk settings
`);
}

/**
 * Gets version information
 * @returns Version info object
 */
export function getVersionInfo(): VersionInfo {
  if (typeof Deno !== 'undefined') {
    return {
      moduleVersion: VERSION,
      nodeVersion: `Deno ${Deno.version.deno}`,
      platform: {
        os: Deno.build.os,
        arch: Deno.build.arch,
      },
    };
  }

  // Node.js and Bun both land here. Index through `globalThis` (rather than
  // referencing a bare `Bun` identifier) so this still type-checks under
  // `deno check`, which has no ambient `Bun` type.
  const bunVersion = (globalThis as { Bun?: { version?: string } }).Bun?.version;

  return {
    moduleVersion: VERSION,
    nodeVersion: bunVersion ? `Bun ${bunVersion}` : `Node.js ${process.version}`,
    platform: {
      os: process.platform,
      arch: process.arch,
    },
  };
}

/**
 * Shows version information
 */
export function showVersion(): void {
  const info = getVersionInfo();

  console.log('AdGuard Filter Rules Compiler (TypeScript Frontend)');
  console.log(`Version: ${info.moduleVersion}`);
  console.log('');
  console.log('Platform Information:');
  console.log(`  OS: ${info.platform.os}`);
  console.log(`  Architecture: ${info.platform.arch}`);
  console.log(`  Runtime: ${info.nodeVersion}`);

  // TypeScript/V8 versions are only meaningful (and only available without a
  // ReferenceError) when actually running under Deno.
  if (typeof Deno !== 'undefined') {
    console.log(`  TypeScript: ${Deno.version.typescript}`);
    console.log(`  V8: ${Deno.version.v8}`);
  }
}

/**
 * Formats transformations value for display
 * @param value - Transformations value from config
 * @returns Formatted string
 */
function formatTransformations(value: unknown): string {
  if (Array.isArray(value)) {
    return value.join(', ');
  }
  if (typeof value === 'string') {
    return value;
  }
  return 'none';
}

/**
 * Shows configuration details
 * @param configPath - Path to configuration file
 * @param format - Optional format override
 */
function showConfig(configPath: string, format?: ConfigurationFormat): void {
  const logger = createLogger(false);
  const config = readConfiguration(configPath, format, logger);

  console.log(`Configuration: ${configPath}`);
  console.log('');
  console.log(`  Name: ${config.name}`);

  const configRecord = config as unknown as Record<string, unknown>;
  const versionValue = configRecord['version'];
  const version = typeof versionValue === 'string' || typeof versionValue === 'number'
    ? String(versionValue)
    : 'N/A';
  console.log(`  Version: ${version}`);

  const licenseValue = configRecord['license'];
  const license = typeof licenseValue === 'string' ? licenseValue : 'N/A';
  console.log(`  License: ${license}`);

  console.log(`  Sources: ${config.sources?.length || 0}`);
  console.log(`  Transformations: ${formatTransformations(configRecord['transformations'])}`);

  console.log('');
  console.log('JSON representation:');
  console.log(toJson(config));
}

/**
 * Extended CLI options with production features
 */
interface ExtendedCliOptions extends CliOptions {
  /** Use JSON format for logging */
  jsonLogs: boolean;
  /** Compilation timeout in milliseconds */
  timeout?: number;
}

/**
 * Parses extended command line arguments
 */
function parseExtendedArgs(args: string[]): ExtendedCliOptions {
  const baseOptions = parseArgs(args);
  const extendedOptions: ExtendedCliOptions = {
    ...baseOptions,
    jsonLogs: false,
  };

  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    const nextArg = args[i + 1];

    switch (arg) {
      case '--json-logs':
        extendedOptions.jsonLogs = true;
        break;
      case '--timeout':
        if (!nextArg) {
          throw new Error('Timeout value is required');
        }
        extendedOptions.timeout = parseInt(nextArg, 10);
        if (isNaN(extendedOptions.timeout)) {
          throw new Error(`Invalid timeout value: ${nextArg}`);
        }
        i++;
        break;
    }
  }

  return extendedOptions;
}

/**
 * Determines if CLI mode should be used
 */
function shouldUseCLIMode(options: ExtendedCliOptions): boolean {
  // Explicit flags
  if (options.interactive) return false;
  if (options.compile) return true;
  if (options.validate) return true;

  // If config path provided, use CLI mode
  if (options.configPath) return true;

  // If any action flags are set, use CLI mode
  if (options.showConfig) return true;
  if (options.copyToRules) return true;
  if (options.outputPath) return true;

  // Production flags indicate CLI mode
  if (options.jsonLogs) return true;
  if (options.timeout) return true;

  // Default to interactive mode
  return false;
}

/**
 * Run validation mode
 */
async function runValidationMode(
  configPath: string,
  format: ConfigurationFormat | undefined,
  logger: ReturnType<typeof createLogger>,
): Promise<number> {
  try {
    const config = readConfiguration(configPath, format, logger);
    const { validateConfiguration } = await import('./validation.ts');
    const result = validateConfiguration(config);

    if (result.valid) {
      console.log('Configuration is valid!');
      if (result.warnings.length > 0) {
        console.log('\nWarnings:');
        for (const warning of result.warnings) {
          console.log(`  - ${warning}`);
        }
      }
      return 0;
    } else {
      console.error('Configuration has errors:');
      for (const error of result.errors) {
        console.error(`  - ${error}`);
      }
      return 1;
    }
  } catch (error) {
    console.error(
      `Error validating configuration: ${error instanceof Error ? error.message : String(error)}`,
    );
    return 1;
  }
}

/**
 * Main CLI entry point
 * @param args - Command line arguments
 * @returns Exit code
 */
export async function main(
  args: string[] = typeof Deno !== 'undefined' ? Deno.args : process.argv.slice(2),
): Promise<number> {
  let shutdownHandler: ShutdownHandler | undefined;

  try {
    const options = parseExtendedArgs(args);

    // Handle help
    if (options.help) {
      showHelp();
      return 0;
    }

    // Handle version
    if (options.version) {
      showVersion();
      return 0;
    }

    // Determine mode
    if (!shouldUseCLIMode(options)) {
      // Interactive mode
      return await runInteractive({
        configPath: options.configPath,
        format: options.format,
        debug: options.debug,
      });
    }

    // CLI mode
    // Create logger (JSON format for production, human-readable for development)
    const logger = options.jsonLogs ? createProductionLogger() : createLogger(options.debug);

    // Initialize graceful shutdown handler
    shutdownHandler = initializeShutdownHandler({ logger });

    // Determine config path
    let configPath: string;
    if (options.configPath) {
      configPath = resolve(options.configPath);
    } else {
      const defaultConfig = findDefaultConfig();
      if (!defaultConfig) {
        logger.error('Configuration file not found.');
        console.error('Searched:');
        console.error('  - compiler-config.json');
        console.error('  - compiler-config.yaml');
        console.error('  - compiler-config.yml');
        console.error('  - compiler-config.toml');
        console.error('');
        console.error('Specify config path with -c/--config');
        return 1;
      }
      configPath = defaultConfig;
    }

    // Validate only mode
    if (options.validate) {
      return await runValidationMode(configPath, options.format, logger);
    }

    // Show config only
    if (options.showConfig) {
      showConfig(configPath, options.format);
      return 0;
    }

    // Check for shutdown before starting
    shutdownHandler.assertNotShuttingDown();

    logger.info('AdGuard Filter Rules Compiler starting...');
    logger.info(`Configuration: ${configPath}`);

    // Run compilation
    const result = await runCompiler({
      configPath,
      outputPath: options.outputPath ? resolve(options.outputPath) : undefined,
      copyToRules: options.copyToRules,
      rulesDirectory: options.rulesDirectory,
      format: options.format,
      logger,
      timeoutMs: options.timeout,
      validateConfig: options.validateConfig,
      failOnWarnings: options.failOnWarnings,
      enableChunking: options.enableChunking,
      chunkSize: options.chunkSize,
      maxParallel: options.maxParallel,
    });

    if (result.success) {
      console.log('');
      console.log('Results:');
      console.log(`  Config Name:  ${result.configName}`);
      console.log(`  Config Ver:   ${result.configVersion}`);
      console.log(`  Rule Count:   ${result.ruleCount.toLocaleString()}`);
      console.log(`  Output Path:  ${result.outputPath}`);
      console.log(`  Hash:         ${result.outputHash.slice(0, 32)}...`);
      console.log(`  Elapsed:      ${result.elapsedMs}ms`);

      if (result.copiedToRules) {
        console.log(`  Copied To:    ${result.rulesDestination}`);
      }

      console.log('');
      logger.info('Done!');
      return 0;
    } else {
      if (result.errorCode) {
        logger.error(`[${result.errorCode}] ${result.errorMessage}`);
      } else {
        logger.error(`Compilation failed: ${result.errorMessage}`);
      }
      return 1;
    }
  } catch (error) {
    if (isCompilerError(error)) {
      console.error(`[ERROR] ${error.toLogString()}`);
    } else {
      const message = error instanceof Error ? error.message : 'Unknown error';
      console.error(`[ERROR] ${message}`);
    }
    return 1;
  } finally {
    // Clean up shutdown handler
    if (shutdownHandler) {
      shutdownHandler.unlisten();
    }
  }
}

// Run if executed directly
// `import.meta.main` is supported by both Deno and Bun (Node.js does not set
// it), so this direct-execution path needs the same runtime-detected exit.
if (import.meta.main) {
  const code = await main();
  if (typeof Deno !== 'undefined') {
    Deno.exit(code);
  } else {
    process.exit(code);
  }
}
