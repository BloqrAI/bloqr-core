/**
 * Configuration reader with multi-format support (JSON, YAML, TOML)
 * Includes validation and sanitization for production safety
 */

import { existsSync, readFileSync, statSync } from 'node:fs';
import { extname, resolve } from 'node:path';
import process from 'node:process';
import { parse as parseYaml } from '@std/yaml';
import { parse as parseToml } from '@std/toml';
import type { IConfiguration } from '../index.ts';
import type { ConfigurationFormat, ExtendedConfiguration, Logger } from './types.ts';
import { logger as defaultLogger } from './logger.ts';
import { ConfigNotFoundError, ConfigParseError, ConfigurationError, ErrorCode } from './errors.ts';
import {
  assertValidConfiguration,
  checkFileSize,
  checkSourceCount,
  DEFAULT_RESOURCE_LIMITS,
} from './validation.ts';
import { assertJsonSchemaValidConfiguration } from './schema-validation.ts';

/**
 * Detects configuration format from file extension
 * @param filePath - Path to configuration file
 * @returns Detected format
 * @throws ConfigurationError if extension is not recognized
 */
export function detectFormat(filePath: string): ConfigurationFormat {
  const ext = extname(filePath).toLowerCase();

  switch (ext) {
    case '.json':
      return 'json';
    case '.yaml':
    case '.yml':
      return 'yaml';
    case '.toml':
      return 'toml';
    default:
      throw new ConfigurationError(
        `Unknown configuration file extension: ${ext}`,
        ErrorCode.CONFIG_INVALID_FORMAT,
        { filePath },
      );
  }
}

/**
 * Parses JSON configuration
 */
function parseJson(content: string, filePath: string): IConfiguration {
  try {
    return JSON.parse(content) as IConfiguration;
  } catch (error) {
    if (error instanceof SyntaxError) {
      throw new ConfigParseError(filePath, 'json', error.message, error);
    }
    throw error;
  }
}

/**
 * Parses YAML configuration
 */
function parseYamlConfig(content: string, filePath: string): IConfiguration {
  try {
    const parsed = parseYaml(content) as unknown;
    if (!parsed || typeof parsed !== 'object') {
      throw new ConfigParseError(filePath, 'yaml', 'parsed result is not an object');
    }
    return parsed as IConfiguration;
  } catch (error) {
    if (error instanceof ConfigParseError) {
      throw error;
    }
    const message = error instanceof Error ? error.message : 'Unknown error';
    throw new ConfigParseError(
      filePath,
      'yaml',
      message,
      error instanceof Error ? error : undefined,
    );
  }
}

/**
 * Parses TOML configuration
 */
function parseTomlConfig(content: string, filePath: string): IConfiguration {
  try {
    const parsed = parseToml(content);
    return parsed as unknown as IConfiguration;
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Unknown error';
    throw new ConfigParseError(
      filePath,
      'toml',
      message,
      error instanceof Error ? error : undefined,
    );
  }
}

/**
 * Default config search paths
 */
const DEFAULT_CONFIG_PATHS = [
  'compiler-config.json',
  'compiler-config.yaml',
  'compiler-config.yml',
  'compiler-config.toml',
  '../compilers/typescript/compiler-config.json',
];

/**
 * Finds a default configuration file
 * @param basePath - Base path to search from; defaults to the current working
 *   directory (Deno-native when available, otherwise `node:process`, which
 *   Node.js and Bun both implement).
 * @returns Path to config file or undefined
 */
export function findDefaultConfig(
  basePath: string = typeof Deno !== 'undefined' ? Deno.cwd() : process.cwd(),
): string | undefined {
  for (const configPath of DEFAULT_CONFIG_PATHS) {
    const fullPath = resolve(basePath, configPath);
    if (existsSync(fullPath)) {
      return fullPath;
    }
  }
  return undefined;
}

/**
 * Configuration reader options
 */
export interface ReadConfigurationOptions {
  /** Skip schema validation (not recommended for production) */
  skipValidation?: boolean;
  /** Custom resource limits */
  resourceLimits?: Partial<typeof DEFAULT_RESOURCE_LIMITS>;
}

/**
 * Reads and parses configuration from a file
 * @param configPath - Path to configuration file
 * @param format - Optional format override
 * @param logger - Logger instance
 * @param options - Additional options
 * @returns Parsed configuration with metadata
 * @throws ConfigNotFoundError if file doesn't exist
 * @throws ConfigParseError if parsing fails
 * @throws ConfigurationError if validation fails
 */
export function readConfiguration(
  configPath: string,
  format?: ConfigurationFormat,
  logger: Logger = defaultLogger,
  options: ReadConfigurationOptions = {},
): ExtendedConfiguration {
  logger.debug(`Reading configuration from: ${configPath}`);

  // Resolve and sanitize path
  const resolvedPath = resolve(configPath);

  // Check file exists
  if (!existsSync(resolvedPath)) {
    throw new ConfigNotFoundError(resolvedPath);
  }

  // Check file size
  const stats = statSync(resolvedPath);
  const maxSize = options.resourceLimits?.maxConfigFileSize ??
    DEFAULT_RESOURCE_LIMITS.maxConfigFileSize;
  checkFileSize(stats.size, maxSize, 'configuration file');

  const content = readFileSync(resolvedPath, 'utf8');
  const detectedFormat = format ?? detectFormat(resolvedPath);

  logger.debug(`Configuration format: ${detectedFormat}`);

  let config: IConfiguration;

  switch (detectedFormat) {
    case 'json':
      config = parseJson(content, resolvedPath);
      break;
    case 'yaml':
      config = parseYamlConfig(content, resolvedPath);
      break;
    case 'toml':
      config = parseTomlConfig(content, resolvedPath);
      break;
    default: {
      const exhaustiveCheck: never = detectedFormat;
      throw new ConfigurationError(
        `Unsupported format: ${String(exhaustiveCheck)}`,
        ErrorCode.CONFIG_INVALID_FORMAT,
        { filePath: resolvedPath },
      );
    }
  }

  // Validate configuration schema unless explicitly skipped. JSON Schema validation
  // runs first, against the same canonical schema every other compiler wrapper
  // validates against (see issue #518); the hand-written checks then catch anything
  // specific to this orchestration layer.
  if (!options.skipValidation) {
    assertJsonSchemaValidConfiguration(config, resolvedPath);
    assertValidConfiguration(config, resolvedPath);
  }

  // Check source count limit
  if (config.sources) {
    const maxSources = options.resourceLimits?.maxSources ?? DEFAULT_RESOURCE_LIMITS.maxSources;
    checkSourceCount(config.sources.length, maxSources);
  }

  // Add metadata
  const extendedConfig = config as ExtendedConfiguration;
  extendedConfig._sourceFormat = detectedFormat;
  extendedConfig._sourcePath = resolvedPath;

  const configRecord = config as unknown as Record<string, unknown>;
  const versionValue = configRecord['version'];
  const version = typeof versionValue === 'string' || typeof versionValue === 'number'
    ? String(versionValue)
    : 'unknown';
  logger.info(`Loaded configuration: ${config.name} v${version}`);
  return extendedConfig;
}

/**
 * Strips orchestration-layer-only metadata (`_sourceFormat`/`_sourcePath`, added by
 * {@linkcode readConfiguration}, and `_chunkMetadata`, added by
 * {@linkcode [chunking].splitIntoChunks}) from a configuration object.
 *
 * Used by {@linkcode toJson} and anywhere else that wants a human/JSON-facing view of
 * the configuration - it deliberately keeps schema-valid, user-visible sections like
 * `output`/`chunking`/`archiving`/`hashVerification` intact. To prepare a configuration
 * for the core engine's strict compile boundary instead, use
 * {@linkcode stripForCoreCompile}.
 *
 * @param config - Configuration object, possibly carrying internal metadata.
 * @returns A shallow copy of `config` with internal metadata fields removed.
 */
export function stripInternalMetadata(config: IConfiguration): IConfiguration {
  const { _sourceFormat, _sourcePath, _chunkMetadata, ...cleanConfig } = config as
    & ExtendedConfiguration
    & { _chunkMetadata?: unknown };
  void _sourceFormat;
  void _sourcePath;
  void _chunkMetadata;
  return cleanConfig;
}

/**
 * Strips every orchestration-layer-only field from a configuration object before it
 * reaches the core compilation engine: {@linkcode stripInternalMetadata}'s internal
 * metadata, plus the schema-valid top-level sections (`output`, `hashVerification`,
 * `archiving`, `chunking`, `$schema`) that this orchestration layer reads directly off
 * {@linkcode ExtendedConfiguration} but that the core engine's `IConfiguration` type
 * doesn't model at all.
 *
 * The core compilation engine's schema validator (`ConfigurationSchema`, in
 * `configuration/schemas.ts`) is `.strict()` and rejects unrecognized properties, so
 * anything that hands a configuration object to {@linkcode [index].compile} needs a
 * clean {@linkcode IConfiguration}, not the
 * {@linkcode ExtendedConfiguration}/`ChunkedConfiguration` the orchestration layer works
 * with internally. Without this, a config file using any of those sections - all valid
 * per the canonical `schemas/compiler-config.schema.json` (see issue #518) - would pass
 * `readConfiguration()`'s schema check yet still fail at the actual compile call.
 *
 * @param config - Configuration object, possibly carrying internal metadata and/or
 * orchestration-only sections.
 * @returns A shallow copy of `config` with those fields removed.
 */
export function stripForCoreCompile(config: IConfiguration): IConfiguration {
  const {
    output,
    hashVerification,
    archiving,
    chunking,
    $schema,
    ...rest
  } = stripInternalMetadata(config) as
    & ExtendedConfiguration
    & { $schema?: unknown };
  void output;
  void hashVerification;
  void archiving;
  void chunking;
  void $schema;
  return rest;
}

/**
 * Converts configuration to JSON string (removes internal metadata)
 * @param config - Configuration object
 * @returns JSON string
 */
export function toJson(config: IConfiguration): string {
  return JSON.stringify(stripInternalMetadata(config), null, 2);
}
