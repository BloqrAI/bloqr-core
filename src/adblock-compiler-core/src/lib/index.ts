/**
 * Rules Compiler Library API
 *
 * This module provides the main library entry points for programmatic usage.
 * Use these exports when integrating adblock-compiler-core into your application.
 *
 * @example
 * ```typescript
 * import {
 *   RulesCompiler,
 *   ConfigurationBuilder,
 *   createRulesCompiler,
 *   createConfiguration
 * } from '@bloqr/compiler-core/lib';
 *
 * // Simple usage
 * const compiler = RulesCompiler.create();
 * const result = await compiler.compile({ configPath: 'config.yaml' });
 *
 * // With builder pattern
 * const compiler = RulesCompiler.builder()
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
 * @packageDocumentation
 */

// Main compiler service and builder
export { createRulesCompiler, RulesCompiler, RulesCompilerBuilder } from './rules-compiler.ts';

export type {
  CompileProgressEvent,
  CompileRunOptions,
  RulesCompilerServiceOptions,
} from './rules-compiler.ts';

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
