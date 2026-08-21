/**
 * Bloqr Compiler Library API
 *
 * This module provides the main library entry points for programmatic usage.
 * Use these exports when integrating compiler-core into your application.
 *
 * @example
 * ```typescript
 * import {
 *   BloqrCompiler,
 *   ConfigurationBuilder,
 *   createBloqrCompiler,
 *   createConfiguration
 * } from '@bloqr/compiler-core/lib';
 *
 * // Simple usage
 * const compiler = BloqrCompiler.create();
 * const result = await compiler.compile({ configPath: 'config.yaml' });
 *
 * // With builder pattern
 * const compiler = BloqrCompiler.builder()
 *   .withTimeout(60000)
 *   .withDebug(true)
 *   .build();
 *
 * // Programmatic configuration
 * const config = ConfigurationBuilder.create('My Filters')
 *   .addSource('https://example.com/filters.txt')
 *   .withPreset('basic')
 *   .build();
 *
 * const rules = await compiler.compileFromConfig(config);
 * ```
 *
 * @module lib
 * @packageDocumentation
 */

// Main compiler service and builder
export {
  BloqrCompiler,
  BloqrCompilerBuilder,
  createBloqrCompiler,
  // Deprecated pre-#331/#372 aliases - see bloqr-compiler.ts
  createRulesCompiler,
  RulesCompiler,
  RulesCompilerBuilder,
} from './bloqr-compiler.ts';

export type {
  BloqrCompilerServiceOptions,
  CompileProgressEvent,
  CompileRunOptions,
  // Deprecated pre-#331/#372 alias - see bloqr-compiler.ts
  RulesCompilerServiceOptions,
} from './bloqr-compiler.ts';

// Configuration builder
export {
  AVAILABLE_TRANSFORMATIONS,
  ConfigurationBuilder,
  createConfiguration,
  TRANSFORMATION_DESCRIPTIONS,
} from './configuration-builder.ts';

export type { SourceConfig, SourceType, Transformation } from './configuration-builder.ts';

// Re-export core orchestration types for convenience
export type {
  CompilerResult,
  ConfigurationFormat,
  ExtendedConfiguration,
  Logger,
  PlatformInfo,
  VersionInfo,
} from '../orchestration/types.ts';

// Re-export validation types
export type { ResourceLimits, ValidationResult } from '../orchestration/validation.ts';

// Re-export commonly needed utilities
export { DEFAULT_RESOURCE_LIMITS, validateConfiguration } from '../orchestration/validation.ts';
