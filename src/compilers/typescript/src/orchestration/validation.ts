/**
 * Configuration schema validation and input sanitization
 * Provides runtime validation for production safety
 */

import { isAbsolute, normalize, resolve } from 'node:path';
import type { IConfiguration } from '../index.ts';
import {
  ConfigurationError,
  ErrorCode,
  PathTraversalError,
  ResourceLimitError,
  ValidationError,
} from './errors.ts';

/**
 * Validation result
 */
export interface ValidationResult {
  /** `true` if the configuration passed validation (no errors, though there may be warnings). */
  valid: boolean;
  /** Fatal validation problems that must be fixed before the configuration can be used. */
  errors: string[];
  /** Non-fatal issues worth surfacing to the user but that do not block compilation. */
  warnings: string[];
}

/**
 * Resource limits configuration
 */
export interface ResourceLimits {
  /** Maximum configuration file size in bytes (default: 10MB) */
  maxConfigFileSize: number;
  /** Maximum output file size in bytes (default: 100MB) */
  maxOutputFileSize: number;
  /** Maximum number of sources (default: 1000) */
  maxSources: number;
  /** Maximum compilation timeout in milliseconds (default: 5 minutes) */
  compilationTimeoutMs: number;
  /** Maximum path length (default: 4096) */
  maxPathLength: number;
}

/**
 * Default resource limits
 */
export const DEFAULT_RESOURCE_LIMITS: ResourceLimits = {
  maxConfigFileSize: 10 * 1024 * 1024, // 10MB
  maxOutputFileSize: 100 * 1024 * 1024, // 100MB
  maxSources: 1000,
  compilationTimeoutMs: 5 * 60 * 1000, // 5 minutes
  maxPathLength: 4096,
};

/**
 * Validates a source object
 */
function validateSource(source: unknown, index: number): string[] {
  const errors: string[] = [];
  const prefix = `sources[${index}]`;

  if (!source || typeof source !== 'object') {
    errors.push(`${prefix}: must be an object`);
    return errors;
  }

  const sourceObj = source as Record<string, unknown>;

  // Required field: 'source' (path or URL)
  if (!sourceObj['source']) {
    errors.push(`${prefix}: must have 'source' field (URL or file path)`);
  } else if (typeof sourceObj['source'] !== 'string') {
    errors.push(`${prefix}.source: must be a string`);
  } else if (sourceObj['source'].trim() === '') {
    errors.push(`${prefix}.source: cannot be empty`);
  }

  // type validation if present - only 'adblock' or 'hosts' allowed per upstream schema
  if (sourceObj['type'] !== undefined) {
    if (typeof sourceObj['type'] !== 'string') {
      errors.push(`${prefix}.type: must be a string`);
    } else if (!['adblock', 'hosts'].includes(sourceObj['type'])) {
      errors.push(`${prefix}.type: must be 'adblock' or 'hosts'`);
    }
  }

  // transformations validation if present
  if (sourceObj['transformations'] !== undefined) {
    if (!Array.isArray(sourceObj['transformations'])) {
      errors.push(`${prefix}.transformations: must be an array`);
    } else {
      validateTransformations(sourceObj['transformations'], `${prefix}.transformations`, errors);
    }
  }

  return errors;
}

/**
 * Valid transformation names from @bloqr/compiler-core
 */
const VALID_TRANSFORMATIONS = [
  'RemoveComments',
  'Compress',
  'RemoveModifiers',
  'Validate',
  'ValidateAllowIp',
  'Deduplicate',
  'InvertAllow',
  'RemoveEmptyLines',
  'TrimLines',
  'InsertFinalNewLine',
  'ConvertToAscii',
];

/**
 * Validates transformations array
 */
function validateTransformations(transformations: unknown[], path: string, errors: string[]): void {
  for (let i = 0; i < transformations.length; i++) {
    const t = transformations[i];
    if (typeof t !== 'string') {
      errors.push(`${path}[${i}]: must be a string`);
    } else if (!VALID_TRANSFORMATIONS.includes(t)) {
      errors.push(
        `${path}[${i}]: invalid transformation '${t}'. Valid: ${VALID_TRANSFORMATIONS.join(', ')}`,
      );
    }
  }
}

/**
 * Validates string array fields (inclusions, exclusions)
 */
function validateStringArray(value: unknown, fieldName: string): string[] {
  const errors: string[] = [];

  if (value === undefined) {
    return errors;
  }

  if (!Array.isArray(value)) {
    errors.push(`${fieldName}: must be an array`);
    return errors;
  }

  for (let i = 0; i < value.length; i++) {
    if (typeof value[i] !== 'string') {
      errors.push(`${fieldName}[${i}]: must be a string`);
    }
  }

  return errors;
}

/**
 * Validates configuration schema at runtime
 */
export function validateConfiguration(config: unknown): ValidationResult {
  const errors: string[] = [];
  const warnings: string[] = [];

  // Root must be an object
  if (!config || typeof config !== 'object') {
    errors.push('Configuration must be an object');
    return { valid: false, errors, warnings };
  }

  const configObj = config as Record<string, unknown>;

  // Required field: name
  if (!configObj['name']) {
    errors.push('name: is required');
  } else if (typeof configObj['name'] !== 'string') {
    errors.push('name: must be a string');
  } else if (configObj['name'].trim() === '') {
    errors.push('name: cannot be empty');
  }

  // Required field: sources
  if (!configObj['sources']) {
    errors.push('sources: is required');
  } else if (!Array.isArray(configObj['sources'])) {
    errors.push('sources: must be an array');
  } else if ((configObj['sources'] as unknown[]).length === 0) {
    errors.push('sources: must have at least one source');
  } else {
    const sources = configObj['sources'] as unknown[];
    for (let i = 0; i < sources.length; i++) {
      errors.push(...validateSource(sources[i], i));
    }
  }

  // Optional field: version
  if (configObj['version'] !== undefined) {
    if (typeof configObj['version'] !== 'string' && typeof configObj['version'] !== 'number') {
      errors.push('version: must be a string or number');
    }
  }

  // Optional field: description
  if (configObj['description'] !== undefined && typeof configObj['description'] !== 'string') {
    errors.push('description: must be a string');
  }

  // Optional field: homepage
  if (configObj['homepage'] !== undefined) {
    if (typeof configObj['homepage'] !== 'string') {
      errors.push('homepage: must be a string');
    } else {
      try {
        new URL(configObj['homepage']);
      } catch {
        warnings.push('homepage: does not appear to be a valid URL');
      }
    }
  }

  // Optional field: license
  if (configObj['license'] !== undefined && typeof configObj['license'] !== 'string') {
    errors.push('license: must be a string');
  }

  // Optional field: transformations
  if (configObj['transformations'] !== undefined) {
    if (!Array.isArray(configObj['transformations'])) {
      errors.push('transformations: must be an array');
    } else {
      validateTransformations(configObj['transformations'], 'transformations', errors);
    }
  }

  // Optional fields: inclusions, exclusions
  errors.push(...validateStringArray(configObj['inclusions'], 'inclusions'));
  errors.push(...validateStringArray(configObj['exclusions'], 'exclusions'));

  return {
    valid: errors.length === 0,
    errors,
    warnings,
  };
}

/**
 * Validates and throws if invalid
 */
export function assertValidConfiguration(
  config: unknown,
  filePath?: string,
): asserts config is IConfiguration {
  const result = validateConfiguration(config);

  if (!result.valid) {
    const errorMessage = `Configuration validation failed:\n  - ${result.errors.join('\n  - ')}`;
    throw new ConfigurationError(
      errorMessage,
      ErrorCode.CONFIG_VALIDATION_ERROR,
      { filePath },
    );
  }
}

/**
 * Checks for path traversal attempts
 */
export function containsPathTraversal(inputPath: string): boolean {
  // Normalize the path for comparison
  const normalizedPath = normalize(inputPath);

  // Check for .. sequences in original or normalized path
  if (inputPath.includes('..') || normalizedPath.includes('..')) {
    return true;
  }

  // Check for null bytes
  if (inputPath.includes('\0')) {
    return true;
  }

  // Check if normalized path differs significantly (indicates traversal attempt)
  const resolved = resolve(inputPath);
  const expectedBase = resolve('.');

  // If resolved path goes above the current directory, it's a traversal
  if (!resolved.startsWith(expectedBase) && !isAbsolute(inputPath)) {
    return true;
  }

  return false;
}

/**
 * Sanitizes and validates a file path
 */
export function sanitizePath(inputPath: string, basePath?: string): string {
  if (!inputPath || typeof inputPath !== 'string') {
    throw new ValidationError('Path must be a non-empty string', ErrorCode.INVALID_PATH);
  }

  // Check length limit
  if (inputPath.length > DEFAULT_RESOURCE_LIMITS.maxPathLength) {
    throw new ResourceLimitError(
      'path length',
      DEFAULT_RESOURCE_LIMITS.maxPathLength,
      inputPath.length,
    );
  }

  // Remove null bytes
  const cleanPath = inputPath.replace(/\0/g, '');

  // Check for path traversal
  if (containsPathTraversal(cleanPath)) {
    throw new PathTraversalError(inputPath);
  }

  // Resolve to absolute path
  const resolvedPath = basePath ? resolve(basePath, cleanPath) : resolve(cleanPath);

  // Verify the resolved path doesn't escape the base directory
  if (basePath) {
    const normalizedBase = resolve(basePath);
    if (!resolvedPath.startsWith(normalizedBase)) {
      throw new PathTraversalError(inputPath);
    }
  }

  return resolvedPath;
}

/**
 * Validates a URL string
 */
export function validateUrl(urlString: string): boolean {
  try {
    const url = new URL(urlString);
    // Only allow http and https protocols
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

/**
 * Validates source URL or path
 */
export function validateSourcePath(
  source: string,
): { valid: boolean; isUrl: boolean; error?: string } {
  if (!source || typeof source !== 'string') {
    return { valid: false, isUrl: false, error: 'Source must be a non-empty string' };
  }

  // Check if it's a URL
  if (source.startsWith('http://') || source.startsWith('https://')) {
    if (validateUrl(source)) {
      return { valid: true, isUrl: true };
    }
    return { valid: false, isUrl: true, error: 'Invalid URL format' };
  }

  // It's a file path
  if (containsPathTraversal(source)) {
    return { valid: false, isUrl: false, error: 'Path traversal detected' };
  }

  return { valid: true, isUrl: false };
}

/**
 * Checks file size against limit
 */
export function checkFileSize(
  sizeBytes: number,
  limitBytes: number,
  resourceName: string,
): void {
  if (sizeBytes > limitBytes) {
    throw new ResourceLimitError(resourceName, limitBytes, sizeBytes);
  }
}

/**
 * Checks source count against limit
 */
export function checkSourceCount(
  count: number,
  limit: number = DEFAULT_RESOURCE_LIMITS.maxSources,
): void {
  if (count > limit) {
    throw new ResourceLimitError('source count', limit, count);
  }
}
